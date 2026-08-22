using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.Tests;

public sealed class RaceSuitabilityTests
{
    private static IndexedRecord Race(string editorId, string? name = null, string plugin = "Skyrim.esm",
        bool playable = true, bool head = true) => new()
        {
            FormKey = $"{Math.Abs(editorId.GetHashCode()) % 0xFFFFFF:X6}:{plugin}",
            Type = IndexedRecordType.Race,
            EditorId = editorId,
            DisplayName = name,
            SourcePlugin = plugin,
            WinningPlugin = plugin,
            DetailJson = $$"""
                {"PlayableFlag":{{(playable ? "true" : "false")}},
                 "HasMaleHeadData":{{(head ? "true" : "false")}},
                 "HasFemaleHeadData":{{(head ? "true" : "false")}}}
                """,
        };

    [Fact]
    public void VanillaRaces_AreOfferedWithFriendlyNamesAndNoRequirement()
    {
        var opt = RaceSuitability.Classify(Race("NordRace", "Nord"));
        Assert.Equal(RaceClass.Vanilla, opt.Class);
        Assert.Equal("Nord", opt.Name);
        Assert.Equal("NordRace", opt.EditorId);
        Assert.Contains("no extra mods", opt.Note);
    }

    [Theory]
    [InlineData("WerewolfBeastRace")]
    [InlineData("DLC1VampireBeastRace")]
    [InlineData("DremoraRace")]
    [InlineData("DwarvenSpiderRace")]
    [InlineData("ElkRace")]
    public void CreatureRaces_AreNeverOfferedByDefault(string editorId)
    {
        // Vanilla/mod creatures are not flagged playable. Blocklist still classifies them as
        // Creature when playable=false so they stay hidden unless the user asks for creatures.
        var race = Race(editorId, playable: false);
        Assert.Equal(RaceClass.Creature, RaceSuitability.Classify(race).Class);
        Assert.Empty(RaceSuitability.Offer([race]));
    }

    [Fact]
    public void PlayableAnthroRace_WithHorseInEditorId_IsNotForcedToCreature()
    {
        // BDHorseRace / similar: real FaceGen head data + playable flag, unlucky name.
        var race = Race("BDHorseRace", "BD Horse", "BD_Ungulate.esp", playable: true, head: true);
        var opt = RaceSuitability.Classify(race);
        Assert.Equal(RaceClass.CustomPlayable, opt.Class);
        Assert.Contains(RaceSuitability.Offer([race]), r => r.FormKey == race.FormKey);
    }

    [Theory]
    [InlineData("NordRaceChild")]
    [InlineData("ImperialRaceChild")]
    public void ChildRaces_AreNeverOfferedAtAll(string editorId)
    {
        Assert.Equal(RaceClass.Unsuitable, RaceSuitability.Classify(Race(editorId)).Class);
        Assert.Empty(RaceSuitability.Offer([Race(editorId)], includeCreatures: true));
    }

    [Fact]
    public void CustomRace_SaysPlainlyThatItBecomesARequirement()
    {
        var opt = RaceSuitability.Classify(
            Race("KapotunRace", "Ka Po' Tun", "DWKaPoTunRace.esp"));
        Assert.Equal(RaceClass.CustomPlayable, opt.Class);
        Assert.Contains("everyone needs", opt.Note);
        Assert.Contains("DWKaPoTunRace.esp", opt.Note);
    }

    [Fact]
    public void RaceWithoutHeadParts_IsACreature_BecauseNoFaceCanEverBeBuilt()
    {
        var opt = RaceSuitability.Classify(Race("SomeHeadlessRace", plugin: "Weird.esp", head: false));
        Assert.Equal(RaceClass.Creature, opt.Class);
        Assert.Empty(RaceSuitability.Offer([Race("SomeHeadlessRace", plugin: "Weird.esp", head: false)]));
    }

    [Fact]
    public void Offer_PutsVanillaFirstAndDropsUnsuitable()
    {
        var offered = RaceSuitability.Offer([
            Race("DremoraRace", playable: false),
            Race("KapotunRace", "Ka Po' Tun", "DWKaPoTunRace.esp"),
            Race("NordRace", "Nord"),
        ]);

        Assert.Equal(2, offered.Count);
        Assert.Equal(RaceClass.Vanilla, offered[0].Class);
        Assert.DoesNotContain(offered, r => r.Name.Contains("Dremora"));
    }

    /// <summary>
    /// Against the real catalogue: the ten playable races must survive the filter and the
    /// obvious creature races must not. Skipped when no catalogue has been built.
    /// </summary>
    [Fact]
    public void RealCatalogue_OffersVanillaRacesAndFiltersCreatures()
    {
        var dbPath = CatalogBuilder.DefaultDbPath;
        if (!File.Exists(dbPath)) return;

        // The catalogue is the user's real cache; a damaged one is the app's problem to repair
        // (CatalogDb.QuarantineBrokenCache), not a reason to fail the suite.
        IReadOnlyList<IndexedRecord> all;
        try
        {
            using var db = new CatalogDb(dbPath, new LoggerConfiguration().CreateLogger());
            all = db.SearchRecords(IndexedRecordType.Race, null, 20000);
        }
        catch (Exception ex) when (CatalogDb.IsCacheFailure(ex)) { return; }
        if (all.Count == 0) return;

        var offered = RaceSuitability.Offer(all);
        Assert.True(offered.Count < all.Count, "the filter should remove creature/child races");

        foreach (var expected in new[] { "Nord", "Breton", "Khajiit", "Argonian" })
            Assert.Contains(offered, r => r.Name == expected && r.Class == RaceClass.Vanilla);

        Assert.DoesNotContain(offered, r => r.Name.Contains("Werewolf", StringComparison.OrdinalIgnoreCase));
    }
}
