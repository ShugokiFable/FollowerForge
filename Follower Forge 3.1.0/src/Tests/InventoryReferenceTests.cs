using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// The lore step puts books, keepsakes, food and ingredients into the same InventoryItems list
/// as her gear, but the reference check still only allowed Armor/Weapon — so picking a single
/// book failed the whole build with REFERENCE_WRONG_TYPE.
/// </summary>
public sealed class InventoryReferenceTests : IDisposable
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), "ff_inv_" + Guid.NewGuid().ToString("N"), "c.db");
    private readonly ILogger _log = new LoggerConfiguration().CreateLogger();

    private static IndexedRecord Record(string formKey, IndexedRecordType type) => new()
    {
        FormKey = formKey, Type = type, EditorId = type.ToString(),
        SourcePlugin = formKey.Split(':')[1], WinningPlugin = formKey.Split(':')[1],
    };

    private static FollowerProfile WithItems(params RecordRef[] items) => new()
    {
        Name = "Carrier",
        PluginName = "FF_Carrier.esp",
        Race = new RecordRef("013746:Skyrim.esm"),
        VoiceType = new RecordRef("013ADD:Skyrim.esm"),
        Class = new RecordRef("01BE18:Skyrim.esm"),
        Placement = new PlacementSpec(),
        InventoryItems = items,
    };

    [Theory]
    [InlineData(IndexedRecordType.Book)]
    [InlineData(IndexedRecordType.MiscItem)]
    [InlineData(IndexedRecordType.Ingestible)]
    [InlineData(IndexedRecordType.Ingredient)]
    [InlineData(IndexedRecordType.Armor)]
    [InlineData(IndexedRecordType.Weapon)]
    public void EverythingTheLoreAndGearStepsOffer_IsAcceptedInInventory(IndexedRecordType type)
    {
        const string item = "0ABCDE:Skyrim.esm";
        using var db = new CatalogDb(_dbPath, _log);
        db.ReplaceRecords(
        [
            Record("013746:Skyrim.esm", IndexedRecordType.Race),
            Record("013ADD:Skyrim.esm", IndexedRecordType.VoiceType),
            Record("01BE18:Skyrim.esm", IndexedRecordType.Class),
            Record(item, type),
        ]);

        var report = new ValidationReport();
        FollowerBuilder.ValidateProfileReferences(WithItems(new RecordRef(item)), db, report);

        Assert.DoesNotContain(report.Findings, f => f.Code == "REFERENCE_WRONG_TYPE");
    }

    [Fact]
    public void AnActualNonCarryable_IsStillRejected()
    {
        const string spell = "012FCD:Skyrim.esm";
        using var db = new CatalogDb(_dbPath, _log);
        db.ReplaceRecords(
        [
            Record("013746:Skyrim.esm", IndexedRecordType.Race),
            Record("013ADD:Skyrim.esm", IndexedRecordType.VoiceType),
            Record("01BE18:Skyrim.esm", IndexedRecordType.Class),
            Record(spell, IndexedRecordType.Spell),
        ]);

        var report = new ValidationReport();
        FollowerBuilder.ValidateProfileReferences(WithItems(new RecordRef(spell)), db, report);

        Assert.Contains(report.Findings, f => f.Code == "REFERENCE_WRONG_TYPE");
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_dbPath)!;
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }
}
