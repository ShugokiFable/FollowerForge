using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Serilog;

namespace FollowerForge.Tests;

public sealed class PluginWriterMasterTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "ff_master_chain_" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _log = new LoggerConfiguration().CreateLogger();

    [Fact]
    public void Write_ExpandsTransitiveMastersAndUsesTheRealLoadOrder()
    {
        Directory.CreateDirectory(_root);
        var baseB = ModKey.FromFileName("Z_BaseB.esm");
        var baseA = ModKey.FromFileName("A_BaseA.esm");
        var gear = ModKey.FromFileName("Gear.esp");
        WriteHeaderOnly(baseB);
        WriteHeaderOnly(baseA);
        WriteHeaderOnly(gear, baseB, baseA);

        var output = new SkyrimMod(ModKey.FromFileName("Follower.esp"), VanillaForms.Release)
        {
            IsSmallMaster = true,
        };
        var npc = output.Npcs.AddNew("FF_MasterTest_NPC");
        npc.Items =
        [
            new ContainerEntry
            {
                Item = new ContainerItem
                {
                    Item = new FormKey(gear, 0x800).ToLink<IItemGetter>(),
                    Count = 1,
                },
            },
        ];

        var path = Path.Combine(_root, "Follower.esp");
        var expectedOrder = new[] { baseB, baseA, gear };
        var writer = new PluginWriter(_log);
        writer.Write(output, path, expectedOrder, _root);

        Assert.Equal(
            expectedOrder.Select(m => m.FileName.String),
            writer.Reopen(path).Masters);
    }

    [Fact]
    public void Write_RejectsAMissingTransitiveMaster()
    {
        Directory.CreateDirectory(_root);
        var missing = ModKey.FromFileName("Missing.esm");
        var gear = ModKey.FromFileName("Gear.esp");
        WriteHeaderOnly(gear, missing);

        var output = new SkyrimMod(ModKey.FromFileName("Follower.esp"), VanillaForms.Release);
        var npc = output.Npcs.AddNew("FF_MissingMaster_NPC");
        npc.Items =
        [
            new ContainerEntry
            {
                Item = new ContainerItem
                {
                    Item = new FormKey(gear, 0x800).ToLink<IItemGetter>(),
                    Count = 1,
                },
            },
        ];

        var path = Path.Combine(_root, "Follower.esp");
        var error = Assert.Throws<FileNotFoundException>(
            () => new PluginWriter(_log).Write(output, path, [gear], _root));
        Assert.Contains("Missing.esm", error.Message);
    }

    private void WriteHeaderOnly(ModKey modKey, params ModKey[] masters)
    {
        var mod = new SkyrimMod(modKey, VanillaForms.Release);
        foreach (var master in masters)
            mod.ModHeader.MasterReferences.Add(new MasterReference { Master = master });
        mod.WriteToBinary(
            Path.Combine(_root, modKey.FileName.String),
            new BinaryWriteParameters { MastersListContent = MastersListContentOption.NoCheck });
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temp cleanup is best effort.
        }
    }
}
