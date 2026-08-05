using FollowerForge.Domain;
using FollowerForge.SkyrimRecords;

namespace FollowerForge.Tests;

/// <summary>
/// Non-humanoid followers. The distinction that matters is head data, measured on real mods:
///   HSF Baby Dragon   BabyDragonRaceFire              headData=false  -> creature
///   Steadfast Maiden  AA00SteadfastMachineMaidenRace  headData=true   -> ordinary custom race
/// A dwarven mech is not a special case; a dragon is.
/// </summary>
public sealed class CreatureRaceTests
{
    private static IndexedRecord Race(string editorId, bool head, bool playable = false) => new()
    {
        FormKey = "000800:Test.esp",
        Type = IndexedRecordType.Race,
        EditorId = editorId,
        DisplayName = editorId,
        SourcePlugin = "Test.esp",
        WinningPlugin = "Test.esp",
        DetailJson = $$"""
            {"PlayableFlag":{{(playable ? "true" : "false")}},
             "HasMaleHeadData":{{(head ? "true" : "false")}},
             "HasFemaleHeadData":{{(head ? "true" : "false")}}}
            """,
    };

    [Fact]
    public void NoHeadData_IsAcreatureRatherThanSimplyRejected()
    {
        // Previously this was "Unsuitable" and vanished, which made creature followers like the
        // Baby Dragon impossible to build at all.
        Assert.Equal(RaceClass.Creature, RaceSuitability.Classify(Race("BabyDragonRaceFire", head: false)).Class);
    }

    [Fact]
    public void AMechWithHeadDataIsJustACustomRace()
    {
        var option = RaceSuitability.Classify(Race("AA00SteadfastMachineMaidenRace", head: true));
        Assert.NotEqual(RaceClass.Creature, option.Class);
        Assert.NotEqual(RaceClass.Unsuitable, option.Class);
    }

    [Fact]
    public void ChildRacesStayExcludedEvenWhenCreaturesAreAskedFor()
    {
        var races = new[] { Race("NordRaceChild", head: true), Race("BabyDragonRaceFire", head: false) };

        var offered = RaceSuitability.Offer(races, includeCreatures: true);

        Assert.DoesNotContain(offered, r => r.Name.Contains("Child", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(offered, r => r.Class == RaceClass.Creature);
    }

    [Fact]
    public void CreaturesAreHiddenUnlessAskedFor()
    {
        var races = new[] { Race("BabyDragonRaceFire", head: false) };

        Assert.Empty(RaceSuitability.Offer(races));
        Assert.Single(RaceSuitability.Offer(races, includeCreatures: true));
    }

    [Fact]
    public void CreatureNotesSayWhatIsLost()
    {
        var note = RaceSuitability.Classify(Race("BabyDragonRaceFire", head: false)).Note;
        Assert.Contains("no face", note, StringComparison.OrdinalIgnoreCase);
    }
}
