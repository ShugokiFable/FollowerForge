using nifly;

namespace FollowerForge.FaceGen;

/// <summary>
/// Thin, disposable wrapper over NiflySharp's <see cref="NifFile"/> for FaceGen head meshes.
/// Loads non-destructively (nifly round-trips blocks it does not understand) and exposes the
/// shape/texture operations the dirty-swap needs.
/// </summary>
public sealed class NifHeadFile : IDisposable
{
    private readonly NifFile _nif = new();
    private bool _loaded;

    /// <summary>Diffuse texture slot index (BSShaderTextureSet slot 0 = the face tint for a head).</summary>
    public const uint DiffuseSlot = 0;

    public void Load(string path)
    {
        var rc = _nif.Load(path);
        if (rc != 0) throw new InvalidDataException($"NiflySharp could not load NIF (code {rc}): {path}");
        _loaded = true;
    }

    private void EnsureLoaded()
    {
        if (!_loaded) throw new InvalidOperationException("NIF not loaded");
    }

    public IReadOnlyList<NiShape> Shapes()
    {
        EnsureLoaded();
        var result = new List<NiShape>();
        using var vec = _nif.GetShapes();
        foreach (var s in vec) result.Add(s);
        return result;
    }

    public string ShapeName(NiShape shape) => shape.name?.get() ?? "";

    public string BlockType(NiShape shape) => shape.GetBlockName();

    public ushort VertexCount(NiShape shape) => shape.GetNumVertices();

    /// <summary>BSShaderTextureSet has up to 9 slots (0 diffuse … 8).</summary>
    public const uint SlotCount = 9;

    public string GetSlot(NiShape shape, uint slot) => _nif.GetTexturePathByIndex(shape, slot) ?? "";

    public string GetDiffuse(NiShape shape) => GetSlot(shape, DiffuseSlot);

    public void SetSlot(NiShape shape, uint slot, string path) => _nif.SetTextureSlot(shape, path, slot);

    /// <summary>Every (shape, slot, path) with a non-empty texture reference.</summary>
    public IReadOnlyList<(NiShape Shape, uint Slot, string Path)> AllSlots()
    {
        EnsureLoaded();
        var result = new List<(NiShape, uint, string)>();
        foreach (var shape in Shapes())
        {
            for (uint slot = 0; slot < SlotCount; slot++)
            {
                var p = _nif.GetTexturePathByIndex(shape, slot);
                if (!string.IsNullOrWhiteSpace(p)) result.Add((shape, slot, p.Replace('/', '\\')));
            }
        }
        return result;
    }

    /// <summary>
    /// Vertex positions for a shape. Used to prove the save round-trip does not disturb the
    /// geometry — a high-poly sculpt is exactly the thing a lossy rewrite would quietly soften.
    /// </summary>
    public IReadOnlyList<(float X, float Y, float Z)> Vertices(NiShape shape)
    {
        EnsureLoaded();
        using var verts = new vectorVector3();
        _nif.GetVertsForShape(shape, verts);
        // The Vector3 wrappers are views into the native vector and die with it, so the
        // components have to be copied out before this scope ends.
        return [.. verts.Select(v => (v.x, v.y, v.z))];
    }

    /// <summary>Distinct texture paths across all shapes/slots for resolution checking.</summary>
    public IReadOnlyList<string> AllTexturePaths() =>
        AllSlots().Select(s => s.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Non-destructive save: no optimization, no block reordering — unknown blocks preserved.</summary>
    public void Save(string path)
    {
        EnsureLoaded();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var options = new NifSaveOptions { optimize = false, sortBlocks = false };
        var rc = _nif.Save(path, options);
        if (rc != 0) throw new IOException($"NiflySharp could not save NIF (code {rc}): {path}");
    }

    public void Dispose() => _nif.Dispose();
}
