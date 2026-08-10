using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using FollowerForge.SkyrimRecords;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Serilog;

namespace FollowerForge.Tests;

/// <summary>
/// Reproduces the user-facing MO2 failure: masters such as SOSVoices.esm live only inside
/// enabled MO2 mod folders, never under bare Steam Data. Build must expand masters from the
/// hardlink plugin view, not GameDataPath alone.
/// </summary>
public sealed class Mo2MasterRootTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "ff_mo2_master_" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _log = new LoggerConfiguration().CreateLogger();

    public Mo2MasterRootTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    [Fact]
    public void PluginIsInstalled_FindsModOnlyMasterOutsideSteamData()
    {
        var env = BuildMo2Fixture(modOnlyPlugin: "SOSVoices.esm");

        Assert.False(File.Exists(Path.Combine(env.GameDataPath, "SOSVoices.esm")));
        Assert.True(Mo2PathResolver.PluginIsInstalled(env, "SOSVoices.esm"));
        Assert.False(Mo2PathResolver.PluginIsInstalled(env, "MissingMaster.esm"));
    }

    [Fact]
    public void EnsurePluginReadRoot_LinksModOnlyMasterIntoPluginView()
    {
        var env = BuildMo2Fixture(modOnlyPlugin: "SOSVoices.esm");

        var root = FollowerBuilder.EnsurePluginReadRoot(env, _log);

        Assert.Equal(env.PluginDataPath, root);
        Assert.NotEqual(env.GameDataPath, root);
        Assert.True(
            File.Exists(Path.Combine(root, "SOSVoices.esm")),
            "MO2 plugin view must expose SOSVoices.esm via hardlink/copy from the mod folder.");
    }

    [Fact]
    public void PluginWriter_ExpandMasterChain_SucceedsFromMo2PluginView()
    {
        var env = BuildMo2Fixture(modOnlyPlugin: "SOSVoices.esm", writeRealHeader: true);
        var masterRoot = FollowerBuilder.EnsurePluginReadRoot(env, _log);

        var gear = ModKey.FromFileName("Gear.esp");
        var sos = ModKey.FromFileName("SOSVoices.esm");
        WriteHeaderOnly(Path.Combine(env.StagingPath, "VoiceMod"), sos);
        // Gear lives only in the mod folder and depends on SOSVoices.
        WriteHeaderOnly(Path.Combine(env.StagingPath, "VoiceMod"), gear, sos);
        // Register Gear in load order files, then rebuild the hardlink view.
        AppendEnabledPlugin(env, "Gear.esp");
        masterRoot = FollowerBuilder.EnsurePluginReadRoot(env, _log);
        Assert.True(File.Exists(Path.Combine(masterRoot, "Gear.esp")));
        Assert.True(File.Exists(Path.Combine(masterRoot, "SOSVoices.esm")));

        var output = new SkyrimMod(ModKey.FromFileName("Follower.esp"), VanillaForms.Release)
        {
            IsSmallMaster = true,
        };
        var npc = output.Npcs.AddNew("FF_Mo2Master_NPC");
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

        var badDir = Path.Combine(_root, "out-steam");
        var goodDir = Path.Combine(_root, "out-mo2");
        Directory.CreateDirectory(badDir);
        Directory.CreateDirectory(goodDir);
        // Mutagen requires the output filename to match the mod's ModKey (Follower.esp).
        var badPath = Path.Combine(badDir, "Follower.esp");
        var outPath = Path.Combine(goodDir, "Follower.esp");
        var writer = new PluginWriter(_log);

        // This is the exact failure mode from 3.2.7 when masterRoot was GameDataPath:
        // mod-only plugins are invisible under bare Steam Data.
        var steamOnly = env.GameDataPath;
        var steamFail = Assert.ThrowsAny<Exception>(() =>
            writer.Write(output, badPath, [gear], steamOnly));
        Assert.Contains("is not installed", steamFail.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            steamFail.Message.Contains("Gear.esp", StringComparison.OrdinalIgnoreCase)
            || steamFail.Message.Contains("SOSVoices.esm", StringComparison.OrdinalIgnoreCase),
            steamFail.Message);

        writer.Write(output, outPath, [sos, gear], masterRoot);
        var reopened = writer.Reopen(outPath);
        Assert.Contains("SOSVoices.esm", reopened.Masters, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Gear.esp", reopened.Masters, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PluginReadRoot_Property_IsPluginDataPathForMo2()
    {
        var env = new EnvironmentSnapshot
        {
            Manager = ModManagerKind.Mo2,
            ManagerLabel = "Mod Organizer 2",
            GameRootPath = Path.Combine(_root, "game"),
            GameDataPath = Path.Combine(_root, "game", "Data"),
            PluginDataPath = Path.Combine(_root, "view"),
            InstancePath = _root,
            StagingPath = Path.Combine(_root, "mods"),
            ProfilesPath = Path.Combine(_root, "profiles"),
            RuntimePluginsTxtPath = Path.Combine(_root, "runtime.txt"),
        };
        Assert.Equal(env.PluginDataPath, env.PluginReadRoot);

        var vortex = env with
        {
            Manager = ModManagerKind.Vortex,
            ManagerLabel = "Vortex",
            PluginDataPath = env.GameDataPath,
        };
        Assert.Equal(vortex.GameDataPath, vortex.PluginReadRoot);
    }

    private EnvironmentSnapshot BuildMo2Fixture(string modOnlyPlugin, bool writeRealHeader = false)
    {
        var game = Path.Combine(_root, "game");
        var data = Path.Combine(game, "Data");
        var mods = Path.Combine(_root, "mods");
        var voiceMod = Path.Combine(mods, "VoiceMod");
        var profiles = Path.Combine(_root, "profiles");
        var profile = Path.Combine(profiles, "Default");
        var view = Path.Combine(_root, "view");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(voiceMod);
        Directory.CreateDirectory(profile);

        // Steam Data deliberately lacks the mod-only master.
        if (writeRealHeader)
            WriteHeaderOnly(voiceMod, ModKey.FromFileName(modOnlyPlugin));
        else
            File.WriteAllText(Path.Combine(voiceMod, modOnlyPlugin), "placeholder-plugin");

        File.WriteAllText(Path.Combine(profile, "modlist.txt"), "+VoiceMod\r\n");
        File.WriteAllText(Path.Combine(profile, "plugins.txt"), $"*{modOnlyPlugin}\r\n");
        File.WriteAllText(Path.Combine(profile, "loadorder.txt"), $"{modOnlyPlugin}\r\n");

        return new EnvironmentSnapshot
        {
            Manager = ModManagerKind.Mo2,
            ManagerLabel = "Mod Organizer 2",
            GameRootPath = game,
            GameDataPath = data,
            PluginDataPath = view,
            InstancePath = _root,
            StagingPath = mods,
            ProfilesPath = profiles,
            RuntimePluginsTxtPath = Path.Combine(_root, "runtime-plugins.txt"),
            ActiveProfileId = "Default",
            ActiveProfileReason = "test",
            Mo2ModPriority = ["VoiceMod"],
        };
    }

    private void AppendEnabledPlugin(EnvironmentSnapshot env, string pluginFileName)
    {
        var profile = Path.Combine(env.ProfilesPath, env.ActiveProfileId!);
        File.AppendAllText(Path.Combine(profile, "plugins.txt"), $"*{pluginFileName}\r\n");
        File.AppendAllText(Path.Combine(profile, "loadorder.txt"), $"{pluginFileName}\r\n");
    }

    private static void WriteHeaderOnly(string directory, ModKey modKey, params ModKey[] masters)
    {
        Directory.CreateDirectory(directory);
        var mod = new SkyrimMod(modKey, VanillaForms.Release);
        foreach (var master in masters)
            mod.ModHeader.MasterReferences.Add(new MasterReference { Master = master });
        mod.WriteToBinary(
            Path.Combine(directory, modKey.FileName.String),
            new BinaryWriteParameters { MastersListContent = MastersListContentOption.NoCheck });
    }
}
