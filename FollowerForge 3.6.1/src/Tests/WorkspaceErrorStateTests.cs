using FollowerForge.Domain;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class WorkspaceErrorStateTests
{
    [Fact]
    public void Manager_failure_keeps_recovery_actions_visible()
    {
        var state = WorkspaceErrorStates.ManagerUnavailable("Mod Organizer 2", "instance was not found");

        Assert.Contains("instance was not found", state);
        Assert.Contains("MO2 setup", state);
        Assert.Contains("switch manager", state);
        Assert.Contains("Paths", state);
    }

    [Fact]
    public void Indexing_failure_names_the_operation_and_offers_retry_without_claiming_game_absence()
    {
        var state = WorkspaceErrorStates.IndexingFailed("C:\\mods\\catalogue.db", "database is locked");

        Assert.Contains("C:\\mods\\catalogue.db", state);
        Assert.Contains("database is locked", state);
        Assert.Contains("retry", state, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not in Skyrim", state, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_severities_map_to_the_three_review_groups()
    {
        var findings = new[]
        {
            new ValidationFinding(ValidationSeverity.Error, "E", "Unsafe reference"),
            new ValidationFinding(ValidationSeverity.Warning, "W", "Check redistribution"),
            new ValidationFinding(ValidationSeverity.Info, "I", "Master chain verified"),
        };

        var groups = ReviewFindingGroups.From(findings);

        Assert.Equal("Unsafe reference [E]", Assert.Single(groups.MustFix));
        Assert.Equal("Check redistribution [W]", Assert.Single(groups.CheckBeforeBuilding));
        Assert.Equal("Master chain verified [I]", Assert.Single(groups.Information));
    }

    [Fact]
    public void Deck_empty_state_describes_the_filter_not_the_installed_game()
    {
        var deck = new ExpertDeckSession("races", [], DeckSelectionMode.Single, []);
        var message = deck.EmptyStateMessage("snow elf");

        Assert.Contains("current filters", message);
        Assert.DoesNotContain("not installed", message, StringComparison.OrdinalIgnoreCase);
    }
}
