using System.Text.Json;
using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.FaceGen;

/// <summary>
/// FaceGen dirty-swap: copies a RaceMenu CharGen head export into the follower's FaceGen
/// slots and rewrites the head's tint texture path to the new facetint location. Non-destructive
/// (unknown NIF blocks preserved, no optimization). Falls back to a Creation Kit handoff report
/// when conversion cannot be done safely.
/// </summary>
public sealed class FaceGenSwapper(ILogger log)
{
    /// <summary>Resolves a Data-relative texture path against the asset index (loose + BSA).</summary>
    public delegate (bool Resolved, string? Container) TextureResolver(string relPath);

    public sealed record Request(
        string SourceNifPath,
        string? SourceDdsPath,
        string PluginName,          // e.g. "FF_AriaForge.esp" (folder name, with extension)
        uint NpcFormId,             // e.g. 0x800
        string DataRoot,            // staging Data root to write meshes/ + textures/
        string ActorEditorId,
        string NpcFormKey);

    public FaceGenResult Swap(Request req, TextureResolver? resolver = null)
    {
        var findings = new List<ValidationFinding>();
        var formIdHex = req.NpcFormId.ToString("X8");

        var faceGeomRel = Path.Combine("meshes", "actors", "character", "facegendata", "facegeom",
            req.PluginName, formIdHex + ".nif");
        var faceTintRel = Path.Combine("textures", "actors", "character", "facegendata", "facetint",
            req.PluginName, formIdHex + ".dds");
        var faceGeomAbs = Path.Combine(req.DataRoot, faceGeomRel);
        var faceTintAbs = Path.Combine(req.DataRoot, faceTintRel);
        // Path stored inside the NIF is Data-relative with backslashes and no leading slash.
        var faceTintNifPath = faceTintRel.Replace('/', '\\');

        if (!File.Exists(req.SourceNifPath))
            return Handoff(req, "Source CharGen NIF not found: " + req.SourceNifPath, faceGeomRel, faceTintRel, findings);

        try
        {
            using var head = new NifHeadFile();
            head.Load(req.SourceNifPath);

            var shapes = head.Shapes();
            if (shapes.Count == 0)
                return Handoff(req, "NIF contains no shapes (not a valid head export)", faceGeomRel, faceTintRel, findings);

            var shapeNames = shapes.Select(head.ShapeName).ToList();

            // Head geometry sanity: a real head export has a high-vertex head shape.
            var maxVerts = shapes.Max(head.VertexCount);
            if (maxVerts < 100)
                findings.Add(new ValidationFinding(ValidationSeverity.Warning, "FACEGEN_LOW_VERTS",
                    $"Highest shape vertex count is {maxVerts}; this may not be a head mesh"));

            // Locate the tint reference. RaceMenu exports the head with the face tint in a
            // higher texture slot pointing at the CharGen export DDS (…\SKSE\Plugins\CharGen\<name>.dds).
            // Prefer an exact match to the source DDS filename; fall back to any \CharGen\ tint path.
            var sourceDdsName = req.SourceDdsPath is not null
                ? Path.GetFileName(req.SourceDdsPath)
                : null;
            var tintSlots = head.AllSlots().Where(s =>
                (sourceDdsName is not null &&
                 s.Path.EndsWith("\\" + sourceDdsName, StringComparison.OrdinalIgnoreCase))
                || s.Path.Contains(@"\chargen\", StringComparison.OrdinalIgnoreCase)).ToList();

            if (tintSlots.Count == 0)
            {
                // Fall back to the head shape's tint slot (7) when it can be identified by name.
                var headShape = shapes.FirstOrDefault(s =>
                    head.ShapeName(s).Contains("head", StringComparison.OrdinalIgnoreCase)
                    && head.VertexCount(s) > 1000);
                if (headShape is null)
                    return Handoff(req,
                        "Could not locate the face-tint texture reference in the export; unsafe to guess.",
                        faceGeomRel, faceTintRel, findings);
                tintSlots = [(headShape, 7u, head.GetSlot(headShape, 7u))];
                findings.Add(new ValidationFinding(ValidationSeverity.Warning, "FACEGEN_TINT_FALLBACK",
                    $"Tint reference not found by path; rewriting slot 7 of head shape '{head.ShapeName(headShape)}'."));
            }

            foreach (var (shape, slot, oldPath) in tintSlots)
            {
                head.SetSlot(shape, slot, faceTintNifPath);
                log.Information("FaceGen: shape '{Shape}' slot {Slot} tint {Old} -> {New}",
                    head.ShapeName(shape), slot, oldPath, faceTintNifPath);
            }

            // Write the FaceGeom NIF (non-destructive).
            head.Save(faceGeomAbs);

            // Copy the tint DDS into place.
            if (req.SourceDdsPath is not null && File.Exists(req.SourceDdsPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(faceTintAbs)!);
                File.Copy(req.SourceDdsPath, faceTintAbs, overwrite: true);
            }
            else
            {
                findings.Add(new ValidationFinding(ValidationSeverity.Error, "FACEGEN_NO_TINT",
                    "No source tint DDS supplied; the follower will have a grey/untinted face"));
            }

            // Reopen + validate the generated NIF.
            var textures = new List<TextureResolution>();
            using (var check = new NifHeadFile())
            {
                check.Load(faceGeomAbs);
                var reopenShapes = check.Shapes();
                if (reopenShapes.Count != shapes.Count)
                    findings.Add(new ValidationFinding(ValidationSeverity.Error, "FACEGEN_SHAPE_MISMATCH",
                        $"Reopened NIF has {reopenShapes.Count} shapes, expected {shapes.Count}"));

                var tintOk = check.AllSlots().Any(s =>
                    s.Path.Equals(faceTintNifPath, StringComparison.OrdinalIgnoreCase));
                if (!tintOk)
                    findings.Add(new ValidationFinding(ValidationSeverity.Error, "FACEGEN_TINT_NOT_SET",
                        "Tint path was not persisted into the saved NIF"));

                // Resolve every referenced texture path.
                foreach (var tex in check.AllTexturePaths())
                {
                    if (tex.Equals(faceTintNifPath, StringComparison.OrdinalIgnoreCase))
                    {
                        textures.Add(new TextureResolution(tex, true, "generated (this package)"));
                        continue;
                    }
                    var (resolved, container) = resolver?.Invoke(tex) ?? (true, "not checked");
                    textures.Add(new TextureResolution(tex, resolved, container));
                    if (!resolved)
                        findings.Add(new ValidationFinding(ValidationSeverity.Warning, "FACEGEN_TEX_MISSING",
                            $"Referenced texture not found in the load order: {tex}", tex));
                }
            }

            var hasError = findings.Any(f => f.Severity == ValidationSeverity.Error);
            log.Information("FaceGen dirty-swap for {Plugin} {FormId}: {Status}",
                req.PluginName, formIdHex, hasError ? "completed with errors" : "OK");

            return new FaceGenResult
            {
                Success = !hasError,
                FaceGeomPath = faceGeomRel,
                FaceTintPath = faceTintRel,
                ShapeNames = shapeNames,
                Textures = textures,
                Findings = findings,
            };
        }
        catch (Exception ex)
        {
            log.Warning("FaceGen swap failed for {Plugin}: {Error}", req.PluginName, ex.Message);
            return Handoff(req, "FaceGen conversion threw: " + ex.Message, faceGeomRel, faceTintRel, findings);
        }
    }

    private FaceGenResult Handoff(Request req, string reason, string faceGeomRel, string faceTintRel,
        List<ValidationFinding> findings)
    {
        var handoff = new CkHandoff(
            req.PluginName, req.ActorEditorId, req.NpcFormKey, faceGeomRel, faceTintRel, reason,
            [
                $"Open {req.PluginName} in the Creation Kit.",
                $"Select the actor {req.ActorEditorId} and press Ctrl+F4 to generate FaceGen.",
                $"Confirm the mesh appears at {faceGeomRel} and tint at {faceTintRel}.",
                "Optionally overwrite the generated FaceGeom/FaceTint with your RaceMenu export (dirty swap).",
            ]);
        var handoffPath = Path.Combine(req.DataRoot, "..", "ck-handoff-report.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(handoffPath))!);
            File.WriteAllText(handoffPath, JsonSerializer.Serialize(handoff, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            log.Warning("Could not write CK handoff report: {Error}", ex.Message);
        }
        findings.Add(new ValidationFinding(ValidationSeverity.Warning, "FACEGEN_CK_HANDOFF", reason));
        log.Warning("FaceGen unsafe for {Plugin}; wrote CK handoff. Reason: {Reason}", req.PluginName, reason);
        return new FaceGenResult
        {
            Success = false,
            NeedsCreationKit = true,
            FaceGeomPath = faceGeomRel,
            FaceTintPath = faceTintRel,
            CkHandoffPath = Path.GetFullPath(handoffPath),
            Findings = findings,
        };
    }
}
