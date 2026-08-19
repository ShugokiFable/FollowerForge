using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class FocusRoutingTests
{
    [Theory]
    [InlineData(WorkspaceSection.Studio)]
    [InlineData(WorkspaceSection.IdentityProgression)]
    [InlineData(WorkspaceSection.Appearance)]
    [InlineData(WorkspaceSection.VoiceDialogue)]
    [InlineData(WorkspaceSection.CombatSkillsTransformation)]
    [InlineData(WorkspaceSection.Loadout)]
    [InlineData(WorkspaceSection.PlacementRoutines)]
    [InlineData(WorkspaceSection.ReviewValidationBuild)]
    public void Guided_mode_always_enters_the_focus_surface(WorkspaceSection section) =>
        Assert.Equal(DensePickerFamily.Focus, FocusRouting.DefaultSurface(section, ExperienceMode.Guided));

    [Theory]
    [InlineData(WorkspaceSection.Appearance, DensePickerFamily.Race)]
    [InlineData(WorkspaceSection.VoiceDialogue, DensePickerFamily.Voice)]
    [InlineData(WorkspaceSection.CombatSkillsTransformation, DensePickerFamily.Class)]
    [InlineData(WorkspaceSection.Loadout, DensePickerFamily.Armor)]
    [InlineData(WorkspaceSection.PlacementRoutines, DensePickerFamily.Location)]
    public void Expert_mode_enters_each_dense_category_primary_deck(
        WorkspaceSection section,
        DensePickerFamily expected) =>
        Assert.Equal(expected, FocusRouting.DefaultSurface(section, ExperienceMode.Expert));

    [Theory]
    [InlineData(WorkspaceSection.Studio)]
    [InlineData(WorkspaceSection.IdentityProgression)]
    [InlineData(WorkspaceSection.ReviewValidationBuild)]
    public void Expert_mode_keeps_non_catalogue_sections_on_focus(WorkspaceSection section) =>
        Assert.Equal(DensePickerFamily.Focus, FocusRouting.DefaultSurface(section, ExperienceMode.Expert));
}
