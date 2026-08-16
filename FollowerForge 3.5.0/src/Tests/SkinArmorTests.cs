using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using Xunit;

namespace FollowerForge.Tests;

/// <summary>
/// Pinning a body is opt-in and has a cost the user must be told about: it turns the skin's
/// plugin into a hard requirement and stops OBody reshaping her.
/// </summary>
public class SkinArmorTests
{
    [Fact]
    public void PinnedSkin_WarnsThatItBecomesARequirement()
    {
        var report = new ValidationReport();
        FollowerBuilder.ReportPinnedBody(new RecordRef("00081A:Fauna.esp"), report);

        var note = Assert.Single(report.Findings, f => f.Code == "BODY_PINNED");
        Assert.Equal(ValidationSeverity.Info, note.Severity);
        Assert.Contains("00081A:Fauna.esp", note.Message);
    }

    [Fact]
    public void NoSkin_SaysNothing_SoTheOBodyDefaultStaysQuiet()
    {
        var report = new ValidationReport();
        FollowerBuilder.ReportPinnedBody(null, report);
        Assert.DoesNotContain(report.Findings, f => f.Code == "BODY_PINNED");
    }
}
