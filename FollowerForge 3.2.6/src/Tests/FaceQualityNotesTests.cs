using FollowerForge.Domain;

namespace FollowerForge.Tests;

public sealed class FaceQualityNotesTests
{
    [Fact]
    public void QualityNotes_DoesNotWarnAboutMissingSavePcFaceAlone()
    {
        var export = new CharGenExport
        {
            Name = "Healthy",
            NifPath = Path.GetTempFileName(),
            TintDdsPath = Path.GetTempFileName(),
            SavePcFacePath = null,
            PresetAppearance = new AppearanceSpec
            {
                SliderCount = 0,
                SculptedVertices = 12,
            },
            HeadShapeCount = 3,
        };
        try
        {
            File.WriteAllText(export.NifPath, "nif");
            File.WriteAllText(export.TintDdsPath!, "dds");
            Assert.Null(export.Blocker);
            Assert.Empty(export.QualityNotes);
            Assert.DoesNotContain(
                export.QualityNotes,
                n => n.Contains("SavePCFace", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(export.NifPath); } catch { }
            try { File.Delete(export.TintDdsPath!); } catch { }
        }
    }

    [Fact]
    public void Blocker_FiresWhenHeadShapeCountIsZero()
    {
        var nif = Path.GetTempFileName();
        try
        {
            File.WriteAllText(nif, "empty");
            var export = new CharGenExport
            {
                Name = "Broken",
                NifPath = nif,
                PresetAppearance = new AppearanceSpec { SliderCount = 0, SculptedVertices = 1 },
                HeadShapeCount = 0,
            };
            Assert.NotNull(export.Blocker);
            Assert.Contains("no shapes", export.Blocker!, StringComparison.OrdinalIgnoreCase);
            Assert.False(export.IsUsable);
        }
        finally
        {
            try { File.Delete(nif); } catch { }
        }
    }

    [Fact]
    public void Blocker_DoesNotFireWhenShapeCountUnknown()
    {
        var nif = Path.GetTempFileName();
        try
        {
            File.WriteAllText(nif, "not-a-nif");
            var export = new CharGenExport
            {
                Name = "Unknown",
                NifPath = nif,
                TintDdsPath = Path.GetTempFileName(),
                PresetAppearance = new AppearanceSpec { SliderCount = 0, SculptedVertices = 1 },
                HeadShapeCount = -1,
            };
            File.WriteAllText(export.TintDdsPath!, "dds");
            Assert.Null(export.Blocker);
            Assert.Contains(export.QualityNotes, n => n.Contains("could not verify", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(nif); } catch { }
        }
    }

    [Fact]
    public void ExportHeadWithEmptyJslotSculpt_IsNotWarnedWhenMeshHasShapes()
    {
        // FaceForge / Export Head: jslot often has many sliders + empty sculpt.data, but the
        // .nif already carries baked geometry (user's Xija case).
        var nif = Path.GetTempFileName();
        var dds = Path.GetTempFileName();
        try
        {
            File.WriteAllText(nif, "nif");
            File.WriteAllText(dds, "dds");
            var export = new CharGenExport
            {
                Name = "Xija",
                NifPath = nif,
                TintDdsPath = dds,
                PresetAppearance = new AppearanceSpec { SliderCount = 59, SculptedVertices = 0 },
                HeadShapeCount = 12,
            };
            Assert.Null(export.Blocker);
            Assert.DoesNotContain(
                export.QualityNotes,
                n => n.Contains("sliders", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(nif); } catch { }
            try { File.Delete(dds); } catch { }
        }
    }

    [Fact]
    public void SliderOnlyWithoutMeshShapes_StillWarns()
    {
        var nif = Path.GetTempFileName();
        try
        {
            File.WriteAllText(nif, "nif");
            var export = new CharGenExport
            {
                Name = "SliderOnly",
                NifPath = nif,
                TintDdsPath = Path.GetTempFileName(),
                PresetAppearance = new AppearanceSpec { SliderCount = 20, SculptedVertices = 0 },
                HeadShapeCount = 0,
            };
            File.WriteAllText(export.TintDdsPath!, "dds");
            // HeadShapeCount == 0 is a hard blocker; quality note also mentions sliders.
            Assert.NotNull(export.Blocker);
            Assert.Contains(export.QualityNotes, n => n.Contains("sliders", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(nif); } catch { }
        }
    }
}
