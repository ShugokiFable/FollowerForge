namespace FollowerForge.Ui;

public enum ReadinessLevel
{
    Complete,
    NeedsAttention,
    /// <summary>
    /// Something is actually broken — a manager/catalogue failure or a build that reported
    /// must-fix findings. Work the user simply has not done yet is NeedsAttention, not this:
    /// a red badge on an untouched draft says the app is broken when nothing is.
    /// </summary>
    Error,
    InProgress,
    /// <summary>Nothing to do here unless the user wants to. Renders as a quiet chip.</summary>
    Optional,
}

public sealed record CategoryReadiness(
    WorkspaceSection Section,
    ReadinessLevel Level,
    string Status,
    string Summary,
    string Action);

public sealed record WorkspaceDraftSummary(
    bool EnvironmentReady,
    bool CatalogueReady,
    bool IsIndexing,
    string Name,
    string PluginName,
    bool HasRace,
    bool HasFace,
    bool HasVoice,
    int CustomLineCount,
    bool HasClass,
    bool HasCombatStyle,
    int ArmorCount,
    int WeaponCount,
    int SpellCount,
    int PerkCount,
    bool HasPlacement,
    bool HasBlockingBuildError);

public static class WorkspaceReadiness
{
    public static IReadOnlyList<CategoryReadiness> Evaluate(WorkspaceDraftSummary draft)
    {
        var identity = string.IsNullOrWhiteSpace(draft.Name)
            ? Item(WorkspaceSection.IdentityProgression, ReadinessLevel.NeedsAttention,
                "Follower name is required.", "Add a follower name")
            : Item(WorkspaceSection.IdentityProgression, ReadinessLevel.Complete,
                $"{draft.Name} · {draft.PluginName}", "Review identity");

        CategoryReadiness appearance;
        if (draft.IsIndexing)
            appearance = Item(WorkspaceSection.Appearance, ReadinessLevel.InProgress,
                "Installed records and faces are still being indexed.", "Wait for indexing");
        else if (!draft.EnvironmentReady || !draft.CatalogueReady)
            appearance = Item(WorkspaceSection.Appearance, ReadinessLevel.Error,
                "Installed appearance records are unavailable until setup is ready.", "Open setup and retry");
        else if (!draft.HasRace)
            appearance = Item(WorkspaceSection.Appearance, ReadinessLevel.NeedsAttention,
                "Choose a race; a face export is optional.", "Choose a race");
        else
            appearance = Item(WorkspaceSection.Appearance, ReadinessLevel.Complete,
                draft.HasFace ? "Race and RaceMenu face selected." : "Race selected · default face will be used.",
                "Review appearance");

        var voice = draft.HasVoice
            ? Item(WorkspaceSection.VoiceDialogue, ReadinessLevel.Complete,
                draft.CustomLineCount == 0 ? "Voice selected · custom lines optional." : $"Voice selected · {draft.CustomLineCount} custom line(s).",
                "Review voice")
            : Item(WorkspaceSection.VoiceDialogue, ReadinessLevel.NeedsAttention,
                "Choose a follower-ready voice.", "Choose a voice");

        var combat = draft.HasClass
            ? Item(WorkspaceSection.CombatSkillsTransformation, ReadinessLevel.Complete,
                draft.HasCombatStyle ? "Class and combat style selected." : "Class selected · race-default combat style.",
                "Review combat")
            : Item(WorkspaceSection.CombatSkillsTransformation, ReadinessLevel.NeedsAttention,
                "Choose a class for predictable skills and progression.", "Choose a class");

        var loadoutCount = draft.ArmorCount + draft.WeaponCount + draft.SpellCount + draft.PerkCount;
        var loadout = loadoutCount == 0
            ? Item(WorkspaceSection.Loadout, ReadinessLevel.Optional,
                "All loadout choices are optional; Skyrim defaults remain available.", "Review loadout")
            : Item(WorkspaceSection.Loadout, ReadinessLevel.Complete,
                $"{loadoutCount} loadout choice(s) selected.", "Review loadout");

        var placement = draft.HasPlacement
            ? Item(WorkspaceSection.PlacementRoutines, ReadinessLevel.Complete,
                "Starting location selected.", "Review placement")
            : Item(WorkspaceSection.PlacementRoutines, ReadinessLevel.NeedsAttention,
                "Choose where the follower starts, or keep the Whiterun fallback.", "Choose a location");

        // Review reports the state of everything else. A real failure (setup broken, or a build
        // that came back with must-fix findings) is an Error; work still outstanding is not.
        var others = new[] { identity, appearance, voice, combat, placement };
        var review = draft.HasBlockingBuildError
            ? Item(WorkspaceSection.ReviewValidationBuild, ReadinessLevel.Error,
                "The last build reported must-fix issues.", "Review must-fix issues")
            : others.Any(item => item.Level == ReadinessLevel.Error)
                ? Item(WorkspaceSection.ReviewValidationBuild, ReadinessLevel.Error,
                    "Setup problems must be fixed before building.", "Review must-fix issues")
                : others.Any(item => item.Level is ReadinessLevel.NeedsAttention or ReadinessLevel.InProgress)
                    ? Item(WorkspaceSection.ReviewValidationBuild, ReadinessLevel.NeedsAttention,
                        "Finish the highlighted categories, then build.", "See what is left")
                    : Item(WorkspaceSection.ReviewValidationBuild, ReadinessLevel.Complete,
                        "Ready for final validation and build review.", "Review and build");

        return [identity, appearance, voice, combat, loadout, placement, review];
    }

    public static CategoryReadiness NextRecommended(IReadOnlyList<CategoryReadiness> items)
    {
        foreach (var level in new[] { ReadinessLevel.Error, ReadinessLevel.NeedsAttention, ReadinessLevel.InProgress })
        {
            var match = items.FirstOrDefault(item => item.Level == level && item.Section != WorkspaceSection.ReviewValidationBuild);
            if (match is not null) return match;
        }

        return items.FirstOrDefault(item => item.Section == WorkspaceSection.ReviewValidationBuild)
               ?? Item(WorkspaceSection.ReviewValidationBuild, ReadinessLevel.Complete,
                   "Ready for review.", "Review and build");
    }

    private static CategoryReadiness Item(
        WorkspaceSection section,
        ReadinessLevel level,
        string summary,
        string action) => new(section, level, Status(level), summary, action);

    private static string Status(ReadinessLevel level) => level switch
    {
        ReadinessLevel.Complete => "Complete",
        ReadinessLevel.NeedsAttention => "Needs attention",
        ReadinessLevel.Error => "Error",
        ReadinessLevel.InProgress => "In progress",
        ReadinessLevel.Optional => "Optional",
        _ => level.ToString(),
    };
}
