namespace FollowerForge.Ui;

public enum DensePickerFamily
{
    Focus,
    Race,
    Voice,
    Class,
    Armor,
    Location,
}

public static class FocusRouting
{
    public static DensePickerFamily DefaultSurface(WorkspaceSection section, ExperienceMode mode)
    {
        if (mode != ExperienceMode.Expert) return DensePickerFamily.Focus;
        return section switch
        {
            WorkspaceSection.Appearance => DensePickerFamily.Race,
            WorkspaceSection.VoiceDialogue => DensePickerFamily.Voice,
            WorkspaceSection.CombatSkillsTransformation => DensePickerFamily.Class,
            WorkspaceSection.Loadout => DensePickerFamily.Armor,
            WorkspaceSection.PlacementRoutines => DensePickerFamily.Location,
            _ => DensePickerFamily.Focus,
        };
    }
}
