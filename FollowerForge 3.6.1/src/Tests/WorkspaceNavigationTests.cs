using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class WorkspaceNavigationTests
{
    [Fact]
    public void Number_shortcuts_map_to_studio_and_the_seven_stable_categories()
    {
        var navigator = new WorkspaceNavigator();
        var expected = new[]
        {
            WorkspaceSection.Studio,
            WorkspaceSection.IdentityProgression,
            WorkspaceSection.Appearance,
            WorkspaceSection.VoiceDialogue,
            WorkspaceSection.CombatSkillsTransformation,
            WorkspaceSection.Loadout,
            WorkspaceSection.PlacementRoutines,
            WorkspaceSection.ReviewValidationBuild,
        };

        for (var shortcut = 0; shortcut <= 7; shortcut++)
        {
            Assert.True(navigator.OpenShortcut(shortcut));
            Assert.Equal(expected[shortcut], navigator.Current);
        }
    }

    [Fact]
    public void Invalid_shortcut_does_not_change_the_current_section()
    {
        var navigator = new WorkspaceNavigator();
        navigator.Open(WorkspaceSection.Loadout);

        Assert.False(navigator.OpenShortcut(8));
        Assert.False(navigator.OpenShortcut(-1));
        Assert.Equal(WorkspaceSection.Loadout, navigator.Current);
    }

    [Fact]
    public void Back_returns_to_the_previous_section_without_touching_shared_draft_state()
    {
        var navigator = new WorkspaceNavigator();
        var draft = new Dictionary<string, string> { ["name"] = "Aria" };
        navigator.Open(WorkspaceSection.Appearance);
        navigator.Open(WorkspaceSection.Loadout);

        Assert.True(navigator.Back());
        Assert.Equal(WorkspaceSection.Appearance, navigator.Current);
        Assert.Equal("Aria", draft["name"]);
        Assert.True(navigator.Back());
        Assert.Equal(WorkspaceSection.Studio, navigator.Current);
        Assert.False(navigator.Back());
    }
}
