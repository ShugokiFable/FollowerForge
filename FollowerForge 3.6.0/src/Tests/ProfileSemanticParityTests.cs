using System.Text.Json;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.Ui;

namespace FollowerForge.Tests;

public sealed class ProfileSemanticParityTests
{
    [Fact]
    public void Navigation_theme_experience_and_deck_state_do_not_change_profile_json()
    {
        var profile = new FollowerProfile
        {
            Name = "Aela Test",
            PluginName = "FF_AelaTest.esp",
            EditorIdPrefix = "FFAela",
            Race = new RecordRef("00013746:Skyrim.esm"),
            VoiceType = new RecordRef("00013ADD:Skyrim.esm"),
            Class = new RecordRef("00013176:Skyrim.esm"),
            CombatStyle = new CombatStyleChoice { Style = new RecordRef("000A01D5:Skyrim.esm"), CloneIntoPlugin = true },
            Female = true,
            Protected = true,
            Marriageable = true,
            EquippedArmor = [new RecordRef("00012E49:Skyrim.esm")],
            InventoryItems = [new RecordRef("000139B5:Skyrim.esm"), new RecordRef("00034C5D:Skyrim.esm", 2)],
            Ammo = [new RecordRef("0001397D:Skyrim.esm", 100)],
            Spells = [new RecordRef("00012FCD:Skyrim.esm")],
            Perks = [new RecordRef("000BABE4:Skyrim.esm")],
            Placement = new PlacementSpec
            {
                LocationId = "whiterun-bannered-mare",
                AlternateLocationIds = ["riften-bee-and-barb"],
            },
            Appearance = new AppearanceSpec { CharGenExportName = "Aela Test", Weight = 65 },
            Level = new LevelScaling { ScaleWithPlayer = true, MinLevel = 10, MaxLevel = 80 },
            BuildTimestampUnix = 1_700_000_000,
        };
        var before = JsonSerializer.Serialize(profile, ProfileIo.Options);

        var navigator = new WorkspaceNavigator();
        navigator.Open(WorkspaceSection.Appearance);
        navigator.Open(WorkspaceSection.Loadout);
        navigator.Back();
        _ = FocusRouting.DefaultSurface(WorkspaceSection.Appearance, ExperienceMode.Expert);
        var preferences = UiPreferences.Default with { Theme = UiTheme.ForgeTeal, Experience = ExperienceMode.Expert };
        Assert.Equal(UiTheme.ForgeTeal, preferences.Theme);
        var deck = new ExpertDeckSession("armor",
            [new DeckRecord("00012E49:Skyrim.esm", "Iron Armor", null, null, "ArmorIronCuirass", new object())],
            DeckSelectionMode.Multi,
            ["00012E49:Skyrim.esm"]);
        deck.Filter("iron");
        deck.Cancel();

        var after = JsonSerializer.Serialize(profile, ProfileIo.Options);
        Assert.Equal(before, after);
    }
}
