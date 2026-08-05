using Mutagen.Bethesda.Plugins;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Naming and container rules for shipped dialogue, both read out of a working mod
/// (Laci Living Doll, 1,674 voice files) rather than assumed. They differ from each other in a
/// way that is easy to get wrong and fails silently: a wrong voice filename produces a follower
/// who moves her lips to nothing.
/// </summary>
public static class VoiceFileNaming
{
    /// <summary>
    /// The FormID as it appears in a voice filename: the record's LOCAL id, master index zeroed,
    /// padded to eight hex digits. Verified: INFO 0700082B ships as "…_0000082B_1.fuz".
    /// Using the full file FormID here is the classic cause of silent custom dialogue.
    /// </summary>
    public static string VoiceFormId(FormKey info) => $"00{info.ID & 0xFFFFFF:X6}";

    /// <summary>Quest EditorIDs are cut to this many characters in voice filenames.</summary>
    public const int QuestNameLimit = 10;

    /// <summary>Topic EditorIDs are cut to this many characters in voice filenames.</summary>
    public const int TopicNameLimit = 15;

    /// <summary>
    /// "&lt;quest, first 10&gt;_&lt;topic, first 15&gt;_&lt;FormID&gt;_&lt;response&gt;".
    ///
    /// THE TRUNCATION IS NOT OPTIONAL and it is the whole reason custom dialogue can look
    /// perfect and still be silent: the engine builds the filename from the cut names, so a
    /// file written with full EditorIDs is simply never found. The failure is doubly confusing
    /// because the line still "plays" — you get the subtitle and a generic mouth animation of
    /// default length, which reads as "the lip sync is broken" rather than "the file is missing".
    ///
    /// Derived by consensus, not assumption: every installed plugin shipping loose voice files
    /// was checked against its own records, searching all truncation lengths for the one that
    /// reproduces its real filenames. 79 of 114 plugins land on exactly 10/15 — including
    /// Skyrim.esm itself, which is the engine's own ground truth. The rest have EditorIDs too
    /// short to distinguish 10/15 from a smaller cut, or ship a mix of both forms.
    ///
    /// Truncation cannot collide: the INFO FormID in the middle of the name is already unique.
    /// </summary>
    public static string FileStem(string questEditorId, string topicEditorId, FormKey info, int responseIndex = 1)
        => $"{Cut(questEditorId, QuestNameLimit)}_{Cut(topicEditorId, TopicNameLimit)}"
         + $"_{VoiceFormId(info)}_{responseIndex}";

    private static string Cut(string value, int limit) =>
        value.Length <= limit ? value : value[..limit];

    /// <summary>Data-relative folder the game looks in: sound\voice\&lt;plugin&gt;\&lt;voice type&gt;\.</summary>
    public static string VoiceFolder(string pluginFileName, string voiceTypeEditorId)
        => Path.Combine("sound", "voice", pluginFileName, voiceTypeEditorId);

    public static string FuzPath(string pluginFileName, string voiceTypeEditorId,
        string questEditorId, string topicEditorId, FormKey info, int responseIndex = 1)
        => Path.Combine(VoiceFolder(pluginFileName, voiceTypeEditorId),
            FileStem(questEditorId, topicEditorId, info, responseIndex) + ".fuz");
}

/// <summary>
/// Writes the .seq file that makes Start Game Enabled quests run in an existing save.
///
/// Format verified byte-for-byte against Laci's abcd_laci.seq: no header, no signature, just a
/// flat array of little-endian uint32 FormIDs — and here the FULL file FormID including the
/// plugin's own master index (07000801, …), unlike voice filenames which zero that index.
/// A dialogue quest missing from the SEQ simply never starts, with no error.
/// </summary>
public static class SeqWriter
{
    /// <param name="startGameEnabledQuests">Only quests flagged Start Game Enabled belong here.</param>
    /// <param name="pluginMasterIndex">
    /// The plugin's own index in its master list — i.e. its master COUNT, since new records get
    /// the next index after the masters. Mutagen's FormKey.ID carries only the 24-bit local id,
    /// so writing it raw yields 00000801 where the game expects 07000801 and the quest never
    /// starts. Verified against Laci (7 masters, SEQ entries 070008xx).
    ///
    /// This holds for light plugins too, which is not obvious: an ESPFE's records live at runtime
    /// FormIDs of the form FE-xxx-yyy, so it would be reasonable to expect the SEQ to use those.
    /// It does not. Surveying every shipped SEQ in this install — 926 quest FormIDs across 215
    /// plugins, 182 of them ESL-flagged — exactly zero use the FE form, and the master-count
    /// index matches in every file whose SEQ is not simply stale. The engine remaps SEQ FormIDs
    /// through the plugin's master table just like any other FormID stored inside it.
    /// </param>
    public static byte[] Build(IEnumerable<FormKey> startGameEnabledQuests, byte pluginMasterIndex)
    {
        var ids = startGameEnabledQuests
            .Select(q => ((uint)pluginMasterIndex << 24) | (q.ID & 0x00FFFFFF))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var bytes = new byte[ids.Count * 4];
        for (var i = 0; i < ids.Count; i++)
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 4, 4), ids[i]);
        return bytes;
    }

    /// <summary>Writes Data-relative SEQ\&lt;plugin without extension&gt;.seq under a package root.</summary>
    public static string Write(string packageRoot, string pluginFileName,
        IEnumerable<FormKey> quests, byte pluginMasterIndex)
    {
        var rel = Path.Combine("SEQ", Path.GetFileNameWithoutExtension(pluginFileName) + ".seq");
        var full = Path.Combine(packageRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, Build(quests, pluginMasterIndex));
        return rel;
    }
}
