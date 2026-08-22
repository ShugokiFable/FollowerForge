namespace FollowerForge.Ui;

public enum WorkspaceSection
{
    Studio = 0,
    IdentityProgression = 1,
    Appearance = 2,
    VoiceDialogue = 3,
    CombatSkillsTransformation = 4,
    Loadout = 5,
    PlacementRoutines = 6,
    ReviewValidationBuild = 7,
}

public sealed class WorkspaceNavigator
{
    private readonly Stack<WorkspaceSection> _history = [];

    public WorkspaceSection Current { get; private set; } = WorkspaceSection.Studio;

    public void Open(WorkspaceSection section)
    {
        if (section == Current) return;
        _history.Push(Current);
        Current = section;
    }

    public bool OpenShortcut(int shortcut)
    {
        if (shortcut is < 0 or > 7) return false;
        Open((WorkspaceSection)shortcut);
        return true;
    }

    public bool Back()
    {
        if (_history.Count == 0) return false;
        Current = _history.Pop();
        return true;
    }

    public void ClearHistory() => _history.Clear();
}

public static class WorkspaceSectionNames
{
    public static string DisplayName(this WorkspaceSection section) => section switch
    {
        WorkspaceSection.Studio => "Studio",
        WorkspaceSection.IdentityProgression => "Identity & progression",
        WorkspaceSection.Appearance => "Appearance",
        WorkspaceSection.VoiceDialogue => "Voice & dialogue",
        WorkspaceSection.CombatSkillsTransformation => "Combat, skills & transformation",
        WorkspaceSection.Loadout => "Loadout",
        WorkspaceSection.PlacementRoutines => "Placement & routines",
        WorkspaceSection.ReviewValidationBuild => "Review, validation & build",
        _ => section.ToString(),
    };
}
