namespace FollowerForge.Ui;

public sealed record FieldMigrationEntry(
    string ControlName,
    WorkspaceSection Section,
    string Destination,
    string Contract);

/// <summary>
/// Explicit map from every named 3.5.0 control to its single 3.6.0 editing or presentation
/// surface. Keeping the names stable is what preserves BuildProfile and all existing handlers.
/// </summary>
public static class FieldMigrationLedger
{
    public static IReadOnlyList<FieldMigrationEntry> Entries { get; } = Build();

    private static IReadOnlyList<FieldMigrationEntry> Build()
    {
        var entries = new List<FieldMigrationEntry>();
        void Add(WorkspaceSection section, string destination, string contract, params string[] controls) =>
            entries.AddRange(controls.Select(control => new FieldMigrationEntry(control, section, destination, contract)));

        Add(WorkspaceSection.Studio, "Workspace shell and category rail",
            "Navigation, environment recovery, status, and forward/back behavior remain available.",
            "Step0", "Step1", "Step2", "Step3", "Step4", "Step5", "Step6",
            "EnvLine", "ManagerSwitchButton", "Mo2SetupButton", "PathsSetupButton", "StatusLine", "BackButton", "NextButton");

        Add(WorkspaceSection.IdentityProgression, "Identity Focus cards and Advanced progression",
            "Existing identity, relationship, leveling, and kin values feed the unchanged profile builder.",
            "Page0", "Page0Title", "NameBox", "PluginBox", "SexBox", "MortalBox", "MarriageBox",
            "RegardsYouLabel", "RelationshipBox", "LevelModeBox", "MinLevelBox", "MaxLevelBox", "FixedLevelBox",
            "KinSectionLabel", "KinHintText", "KinSearch", "KinRankBox", "AddKinButton", "KinCandidates",
            "KinError", "KinPeopleLabel", "KinList");

        Add(WorkspaceSection.Appearance, "Appearance Focus cards and Race/Face Expert Decks",
            "Existing face, race, vampire, and body inputs remain the single editable source.",
            "Page1", "Page1Title", "Page1Hint", "FaceSearch", "FaceList", "VampireBox", "CustomRacesBox",
            "CreatureRacesBox", "RaceSearch", "RaceList");

        Add(WorkspaceSection.VoiceDialogue, "Voice Focus cards and Voice Expert Deck",
            "Voice selection, dialogue, synthesis, context, and line ordering retain their existing contracts.",
            "Page2", "Page2Title", "VoiceSearch", "VoiceScopeBox", "VoiceCountLine", "VoiceList",
            "CustomLinesHint", "VoiceSynthStatus", "LineTriggerBox", "LinePromptBox", "LineTextBox",
            "LineEmotionBox", "AddLineButton", "LinePlaceBox", "LineTimeBox", "LineError", "HerLinesLabel",
            "LineList", "SynthesizeBox");

        Add(WorkspaceSection.CombatSkillsTransformation, "Combat Focus cards, Advanced panels, and Expert Decks",
            "Class, style, AI, evolution, transformation, stats, and skill values feed the unchanged profile builder.",
            "Page3", "Page3Title", "ClassSearch", "ClassList", "CstySearch", "CstyList", "CloneCstyBox",
            "TemperBox", "TemperHint", "EvolveTitle", "EvolveHint", "EvolveBox", "EvolveOptions", "EvolvePhases",
            "EvolveCombats", "EvolveEndBox", "EvolveNote", "TransformHint", "TransformKindBox", "TransformCustom",
            "TransformRaceSearch", "TransformRaceList", "TransformSpellSearch", "TransformSpellList", "TransformRevertBox",
            "StatsModeBox", "CustomStatsPanel", "StatPresetBox", "HealthStatBox", "MagickaStatBox", "StaminaStatBox",
            "SkillEditorGrid");

        Add(WorkspaceSection.Loadout, "Loadout Focus cards and family-specific Expert Decks",
            "All remembered multi-selections, quantities, legacy outfit, body, spell, and perk values remain canonical.",
            "Page4", "Page4Title", "Page4Hint", "ArmorSearch", "ArmorTorsoList", "ArmorHeadList", "ArmorHandsList",
            "ArmorFeetList", "ArmorShieldList", "ArmorAccessoriesList", "ArmorOtherList", "WeaponSearch", "WeaponList",
            "AmmoHint", "AmmoCountBox", "AmmoSearch", "AmmoList", "LoreHint", "LoreKindBox", "LoreCountBox",
            "LoreSearch", "LoreList", "SpellsLabel", "PerksLabel", "SpellSearch", "PerkSearch", "SpellList",
            "PerkList", "OutfitSearch", "OutfitList", "BodyHint", "SkinSearch", "SkinList");

        Add(WorkspaceSection.PlacementRoutines, "Placement Focus cards and Location Expert Deck",
            "Primary and alternate locations, sandbox routine, sleep, and Enemy-to-Ally choices remain canonical.",
            "Page5", "Page5Title", "PlaceSearch", "PlaceList", "IdleLabel", "IdleBox", "SleepsBox", "AlternateLabel",
            "AlternateHint", "SpawnError", "AlternateSpawnList", "E2AHint", "E2ABox", "E2AOptions", "E2ACompanyBox",
            "E2ANote");

        Add(WorkspaceSection.ReviewValidationBuild, "Review dossier, grouped findings, and build actions",
            "Summary-only surfaces do not edit the draft; asset strategy and build actions preserve existing behavior.",
            "Page6", "Page6Title", "SummaryText", "AssetsLabel", "HubModeBox", "OwnHubPanel", "HubPrefixBox",
            "HubPermissionBox", "BuildButton", "ZipBox", "OpenFolderButton", "BuildLog");

        return entries;
    }
}
