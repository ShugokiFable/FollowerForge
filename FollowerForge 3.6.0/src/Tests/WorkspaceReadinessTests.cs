using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class WorkspaceReadinessTests
{
    [Fact]
    public void Missing_name_is_an_identity_error_and_the_first_recommendation()
    {
        var result = WorkspaceReadiness.Evaluate(ReadyDraft() with { Name = "" });

        var identity = Assert.Single(result, x => x.Section == WorkspaceSection.IdentityProgression);
        Assert.Equal(ReadinessLevel.Error, identity.Level);
        Assert.Contains("name", identity.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkspaceSection.IdentityProgression, WorkspaceReadiness.NextRecommended(result).Section);
    }

    [Fact]
    public void Indexing_is_in_progress_and_an_unavailable_environment_is_actionable()
    {
        var indexing = WorkspaceReadiness.Evaluate(ReadyDraft() with { IsIndexing = true });
        Assert.Equal(ReadinessLevel.InProgress,
            Assert.Single(indexing, x => x.Section == WorkspaceSection.Appearance).Level);

        var unavailable = WorkspaceReadiness.Evaluate(ReadyDraft() with
        {
            EnvironmentReady = false,
            CatalogueReady = false,
        });
        var appearance = Assert.Single(unavailable, x => x.Section == WorkspaceSection.Appearance);
        Assert.Equal(ReadinessLevel.Error, appearance.Level);
        Assert.Contains("setup", appearance.Action, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Optional_empty_loadout_is_information_not_a_build_blocker()
    {
        var result = WorkspaceReadiness.Evaluate(ReadyDraft() with
        {
            ArmorCount = 0,
            WeaponCount = 0,
            SpellCount = 0,
            PerkCount = 0,
        });

        var loadout = Assert.Single(result, x => x.Section == WorkspaceSection.Loadout);
        Assert.Equal(ReadinessLevel.Complete, loadout.Level);
        Assert.Contains("optional", loadout.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Recommendation_prioritizes_errors_then_attention_then_review()
    {
        var error = new CategoryReadiness(
            WorkspaceSection.Appearance, ReadinessLevel.Error, "Error", "Face unavailable", "Fix appearance");
        var attention = new CategoryReadiness(
            WorkspaceSection.VoiceDialogue, ReadinessLevel.NeedsAttention, "Needs attention", "Choose voice", "Choose voice");
        var complete = new CategoryReadiness(
            WorkspaceSection.IdentityProgression, ReadinessLevel.Complete, "Complete", "Ready", "Review");

        Assert.Equal(WorkspaceSection.Appearance,
            WorkspaceReadiness.NextRecommended([attention, complete, error]).Section);
        Assert.Equal(WorkspaceSection.VoiceDialogue,
            WorkspaceReadiness.NextRecommended([complete, attention]).Section);

        var allComplete = WorkspaceReadiness.Evaluate(ReadyDraft());
        Assert.Equal(WorkspaceSection.ReviewValidationBuild,
            WorkspaceReadiness.NextRecommended(allComplete).Section);
    }

    private static WorkspaceDraftSummary ReadyDraft() => new(
        EnvironmentReady: true,
        CatalogueReady: true,
        IsIndexing: false,
        Name: "Aria",
        PluginName: "FF_Aria.esp",
        HasRace: true,
        HasFace: true,
        HasVoice: true,
        CustomLineCount: 0,
        HasClass: true,
        HasCombatStyle: true,
        ArmorCount: 2,
        WeaponCount: 1,
        SpellCount: 0,
        PerkCount: 0,
        HasPlacement: true,
        HasBlockingBuildError: false);
}
