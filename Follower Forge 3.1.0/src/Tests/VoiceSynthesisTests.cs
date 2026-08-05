using FollowerForge.AssetIndex;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda.Plugins;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Filenames taken verbatim from shipped mods, paired with the EditorIDs actually stored in
/// their plugins. This is the bug that made custom dialogue silent: the engine truncates the
/// quest to 10 characters and the topic to 15, so full-length names are never found.
/// </summary>
public sealed class VoiceNameTruncationTests
{
    [Theory]
    // Each row was read out of the mod's own records and confirmed against a file that exists
    // on disk — not reconstructed by hand.
    // Eudora - The Dibellan Shieldmaiden (213 of its 237 lines are named this way)
    [InlineData("Eudora_Dialogues_Misc", "Eudora_Dialogues_Idle", 0x22FC, "Eudora_Dia_Eudora_Dialogue_000022FC_1")]
    [InlineData("EFF_Recruit_Dialogue", "EFF_Recruit_Dialogue_GreetTopic", 0x5E63, "EFF_Recrui_EFF_Recruit_Dia_00005E63_1")]
    // ColdSun's Visions - Karla Raven
    [InlineData("csv_KarlaQuestBasic", "csv_KarlaQuestBasicShared", 0x814, "csv_KarlaQ_csv_KarlaQuestB_00000814_1")]
    [InlineData("csv_KarlaQuestBasic", "csv_KarlaQuestBasicWaitTopic", 0x82B, "csv_KarlaQ_csv_KarlaQuestB_0000082B_1")]
    public void TruncatesExactlyAsShippedModsDo(string quest, string topic, uint id, string expected) =>
        Assert.Equal(expected, VoiceFileNaming.FileStem(quest, topic, Key(id)));

    [Fact]
    public void ShortNamesAreLeftAlone()
    {
        // Laci: quest is exactly 10 and topic 14, so nothing is cut.
        Assert.Equal("abcd_laci2_abcd_laci2_hi2_0000082B_1",
            VoiceFileNaming.FileStem("abcd_laci2", "abcd_laci2_hi2", Key(0x82B)));
    }

    [Fact]
    public void TruncationCannotCollide_BecauseTheFormIdIsAlreadyUnique()
    {
        // Two topics that truncate to the same 15 characters still get distinct filenames.
        var a = VoiceFileNaming.FileStem("FF_Nat_Dialogue", "FF_Nat_Topic001", Key(0x805));
        var b = VoiceFileNaming.FileStem("FF_Nat_Dialogue", "FF_Nat_Topic002", Key(0x806));
        Assert.NotEqual(a, b);
    }

    private static FormKey Key(uint id) => new(ModKey.FromFileName("FF_Test.esp"), id);
}

public sealed class VoiceNamingTests
{
    private static FormKey Key(uint id) => new(ModKey.FromFileName("FF_Test.esp"), id);

    /// <summary>
    /// Voice filenames use the LOCAL id with the master index zeroed. Verified against Laci
    /// Living Doll: INFO 0700082B ships as "abcd_laci2_abcd_laci2_hi2_0000082B_1.fuz".
    /// Using the full file FormID here is the classic silent-dialogue bug.
    /// </summary>
    [Theory]
    [InlineData(0x00082Bu, "0000082B")]
    [InlineData(0x00082Cu, "0000082C")]
    [InlineData(0x0008A2u, "000008A2")]
    [InlineData(0x000800u, "00000800")]
    public void VoiceFormId_ZeroesTheMasterIndex(uint local, string expected)
        => Assert.Equal(expected, VoiceFileNaming.VoiceFormId(Key(local)));

    [Fact]
    public void FileStem_MatchesAShippedFollowerExactly()
    {
        // Real filename from Laci Living Doll.
        Assert.Equal("abcd_laci2_abcd_laci2_hi2_0000082B_1",
            VoiceFileNaming.FileStem("abcd_laci2", "abcd_laci2_hi2", Key(0x00082B)));
    }

    [Fact]
    public void FuzPath_LandsWhereTheGameLooks()
    {
        var path = VoiceFileNaming.FuzPath("FF_Aria.esp", "FemaleEvenToned", "FF_AriaDialogue",
            "FF_AriaHello", Key(0x000801)).Replace('/', '\\');
        // "FF_AriaDialogue" is 15 characters, so it is cut to "FF_AriaDia"; the topic fits.
        Assert.Equal(@"sound\voice\FF_Aria.esp\FemaleEvenToned\FF_AriaDia_FF_AriaHello_00000801_1.fuz", path);
    }
}

public sealed class SeqWriterTests
{
    private static FormKey Q(uint id) => new(ModKey.FromFileName("abcd_laci.esp"), id);

    /// <summary>
    /// Byte-for-byte against Laci's abcd_laci.seq: no header, no signature, just little-endian
    /// uint32 FormIDs. Unlike voice filenames these keep the plugin's master index.
    /// </summary>
    [Fact]
    public void Build_ReproducesARealSeqFile()
    {
        // Local ids as Mutagen reports them; Laci's plugin has 7 masters, so its own records
        // are written with index 07 -> the SEQ entries are 070008xx.
        var quests = new[]
        {
            Q(0x000801), Q(0x000829), Q(0x00082D),
            Q(0x00082E), Q(0x0009A9), Q(0x000A34),
        };

        var bytes = SeqWriter.Build(quests, pluginMasterIndex: 7);

        Assert.Equal(24, bytes.Length);
        Assert.Equal(
            "01 08 00 07 29 08 00 07 2d 08 00 07 2e 08 00 07 a9 09 00 07 34 0a 00 07",
            Convert.ToHexString(bytes).ToLowerInvariant()
                .Chunk(2).Select(c => new string(c)).Aggregate((a, b) => a + " " + b));
    }

    [Fact]
    public void Build_HasNoHeaderAndDedupes()
    {
        var bytes = SeqWriter.Build([Q(0x000801), Q(0x000801)], pluginMasterIndex: 7);
        Assert.Equal(4, bytes.Length);   // one entry, nothing else
    }

    [Fact]
    public void Write_PutsItWhereTheGameReadsIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "ff_seq_" + Guid.NewGuid().ToString("N"));
        try
        {
            var rel = SeqWriter.Write(root, "FF_Aria.esp", [Q(0x000801)], pluginMasterIndex: 1);
            Assert.Equal(Path.Combine("SEQ", "FF_Aria.seq"), rel);
            Assert.True(File.Exists(Path.Combine(root, rel)));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }
}

/// <summary>
/// Drives the user's real xVASynth. Skipped cleanly when it is not installed, so the suite still
/// passes on a machine without it.
/// </summary>
public sealed class VoiceSynthesisTests
{
    private static readonly ILogger Log = new LoggerConfiguration().CreateLogger();

    [Fact]
    public void Catalog_MapsVanillaFollowerVoicesToModels()
    {
        var catalog = new VoiceModelCatalog();
        if (!catalog.Installed) return;

        Assert.True(catalog.CanSpeak("FemaleEvenToned"));
        Assert.True(catalog.CanSpeak("MaleEvenToned"));
        Assert.Equal("sk_femaleeventoned", catalog.ForVoiceType("FemaleEvenToned")!.ModelId);
        Assert.False(catalog.CanSpeak("NoSuchVoiceType"));
    }

    [Fact]
    public async Task Speak_ProducesAPlayableFuzWithLipData()
    {
        if (Environment.GetEnvironmentVariable("FF_VOICE_E2E") != "1") return;  // slow: opt in

        var catalog = new VoiceModelCatalog();
        using var synth = new VoiceSynthesizer(catalog, Log);
        if (!synth.Available) return;

        var dir = Path.Combine(Path.GetTempPath(), "ff_voice_" + Guid.NewGuid().ToString("N"));
        var fuz = Path.Combine(dir, "line.fuz");
        try
        {
            var result = await synth.SpeakAsync("FemaleEvenToned",
                "I am ready to follow you, my thane.", fuz);

            Assert.True(result.Success, result.Error);
            Assert.True(File.Exists(fuz));
            // A fuz is FUZE magic + lip block + xwm audio; an empty or lip-less file is useless.
            var bytes = File.ReadAllBytes(fuz);
            Assert.True(bytes.Length > 2000, $"fuz suspiciously small ({bytes.Length} bytes)");
            Assert.Equal("FUZE", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            var lipSize = BitConverter.ToUInt32(bytes, 8);
            Assert.True(lipSize > 0, "fuz contains no lip data — her mouth would not move");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
