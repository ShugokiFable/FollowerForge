using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.Ui;

public partial class MainWindow : Window
{
    private static readonly ILogger Log = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();
    private EnvironmentSnapshot? _env;

    public MainWindow() => AvaloniaXamlLoader.Load(this);

    private async void OnDiscover(object? sender, RoutedEventArgs e)
    {
        var summary = this.FindControl<TextBlock>("EnvSummary")!;
        summary.Text = "Discovering…";
        try
        {
            _env = await Task.Run(() => new VortexDiscovery(Log).Discover());
            summary.Text = $"{_env.ActiveProfileId} · {_env.EnabledPluginCount} plugins · " +
                           $"{_env.StagingModCount} staging mods · {_env.GameRootPath}";
        }
        catch (Exception ex)
        {
            summary.Text = "Error: " + ex.Message;
        }
    }

    private async void OnIndex(object? sender, RoutedEventArgs e)
    {
        if (!EnsureEnv()) return;
        var summary = this.FindControl<TextBlock>("EnvSummary")!;
        summary.Text = "Indexing modpack (this takes ~40s)…";
        try
        {
            var s = await Task.Run(() => new CatalogBuilder(Log).Build(_env!));
            summary.Text = $"Indexed {s.Records} records / {s.Assets} assets in {s.Elapsed:mm\\:ss}.";
        }
        catch (Exception ex) { summary.Text = "Index error: " + ex.Message; }
    }

    private async void OnSearch(object? sender, RoutedEventArgs e)
    {
        var grid = this.FindControl<DataGrid>("ResultsGrid")!;
        var typeText = ((ComboBoxItem)this.FindControl<ComboBox>("TypeBox")!.SelectedItem!).Content?.ToString();
        var text = this.FindControl<TextBox>("SearchText")!.Text;
        var dbPath = CatalogBuilder.DefaultDbPath;
        if (!File.Exists(dbPath)) { grid.ItemsSource = new[] { Row("(run Index modpack first)") }; return; }

        try
        {
            var results = await Task.Run(() =>
            {
                using var db = new CatalogDb(dbPath, Log);
                var type = Enum.TryParse<IndexedRecordType>(typeText, out var t) ? t : (IndexedRecordType?)null;
                return db.SearchRecords(type, text, 200);
            });
            grid.ItemsSource = results;
        }
        catch (Exception ex) { grid.ItemsSource = new[] { Row("Error: " + ex.Message) }; }
    }

    private async void OnBuild(object? sender, RoutedEventArgs e)
    {
        if (!EnsureEnv()) return;
        var log = this.FindControl<TextBox>("BuildLog")!;
        var profilePath = this.FindControl<TextBox>("ProfilePath")!.Text;
        if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
        {
            log.Text = "Set a valid profile JSON path first (use Sample profile…).";
            return;
        }
        var face = this.FindControl<TextBox>("FaceName")!.Text;
        var zip = this.FindControl<CheckBox>("ZipBox")!.IsChecked == true;
        var det = this.FindControl<CheckBox>("DetBox")!.IsChecked == true;
        log.Text = "Building…";

        try
        {
            var text = await Task.Run(() => RunBuild(profilePath!, face, zip, det));
            log.Text = text;
        }
        catch (Exception ex) { log.Text = "Build error: " + ex.Message; }
    }

    private string RunBuild(string profilePath, string? face, bool zip, bool det)
    {
        var profile = ProfileIo.Load(profilePath);
        if (!string.IsNullOrWhiteSpace(face))
            profile = profile with { Appearance = profile.Appearance with { CharGenExportName = face } };

        var workspace = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FollowerForge", "workspace");
        var dbPath = CatalogBuilder.DefaultDbPath;
        CatalogDb? catalog = File.Exists(dbPath) ? new CatalogDb(dbPath, Log) : null;
        try
        {
            using var placement = new PlacementResolver(Log);
            var ws = placement.ResolveDefaultWorldspace(_env!, profile);
            var result = new FollowerBuilder(Log).Build(profile, _env!, workspace, ws, catalog);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Follower : {result.Manifest.Name}");
            sb.AppendLine($"Plugin   : {result.Manifest.PluginName}  NPC {result.Manifest.NpcFormKey}");
            sb.AppendLine($"Masters  : {string.Join(", ", result.Manifest.Masters)}");
            sb.AppendLine($"FaceGen  : {(result.Manifest.HasFaceGen ? "yes" : "no")}");
            sb.AppendLine($"Output   : {result.OutputDirectory}");
            foreach (var f in result.Validation.Findings)
                sb.AppendLine($"  [{f.Severity}] {f.Code}: {f.Message}");
            if (result.Success && zip)
                sb.AppendLine("Package  : " + new VortexPackager(Log)
                    .Package(result.OutputDirectory, Path.GetFileNameWithoutExtension(profile.PluginName)));
            if (result.Success && det)
            {
                var d = new DeterminismVerifier(Log).Verify(profile, _env!, ws, catalog);
                sb.AppendLine("Rebuild  : " + (d.Identical ? "DETERMINISTIC (byte-identical)" : "NON-DETERMINISTIC!"));
            }
            sb.AppendLine(result.Success ? "BUILD OK (published)" : "BUILD FAILED (not published)");
            return sb.ToString();
        }
        finally { catalog?.Dispose(); }
    }

    private void OnSampleProfile(object? sender, RoutedEventArgs e)
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FollowerForge", "follower-profile.sample.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ProfileIo.Save(path, new FollowerProfile
        {
            Name = "Aria Forge",
            PluginName = "FF_AriaForge.esp",
            Race = new RecordRef("013746:Skyrim.esm"),
            VoiceType = new RecordRef("013ADD:Skyrim.esm"),
            Class = new RecordRef("013176:Skyrim.esm"),
            Outfit = new RecordRef("01DC10:Skyrim.esm"),
            Placement = new PlacementSpec { Cell = new RecordRef("01A270:Skyrim.esm") },
            Protected = true,
        });
        this.FindControl<TextBox>("ProfilePath")!.Text = path;
        this.FindControl<TextBox>("BuildLog")!.Text = "Sample profile written to:\n" + path;
    }

    private async void OnDetect(object? sender, RoutedEventArgs e)
    {
        if (!EnsureEnv()) return;
        var log = this.FindControl<TextBox>("DetectLog")!;
        log.Text = "Detecting…";
        try
        {
            var text = await Task.Run(() =>
            {
                var (entries, _) = new LoadOrderBuilder(Log).BuildEntryList(_env!);
                var enabled = entries.Where(x => x.Enabled).Select(x => x.PluginFileName).ToList();
                var dbPath = CatalogBuilder.DefaultDbPath;
                using var db = File.Exists(dbPath) ? new CatalogDb(dbPath, Log) : null;
                Func<string, bool> assetExists = db is null ? _ => false : db.AssetPathPrefixExists;
                var report = Detection.Detect(enabled, assetExists);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Frameworks:");
                foreach (var f in report.Frameworks) sb.AppendLine($"  {f.Framework} [{f.Plugin}]");
                if (report.Frameworks.Count == 0) sb.AppendLine("  (none)");
                sb.AppendLine("Body systems:");
                foreach (var b in report.BodySystems) sb.AppendLine($"  {b.System} ({b.Evidence})");
                sb.AppendLine(report.Guidance);
                return sb.ToString();
            });
            log.Text = text;
        }
        catch (Exception ex) { log.Text = "Detect error: " + ex.Message; }
    }

    private bool EnsureEnv()
    {
        if (_env is not null) return true;
        Dispatcher.UIThread.Post(() =>
            this.FindControl<TextBlock>("EnvSummary")!.Text = "Click 'Discover environment' first.");
        return false;
    }

    private static object Row(string message) => new IndexedRecord
    {
        FormKey = "", Type = IndexedRecordType.Npc, DisplayName = message,
        SourcePlugin = "", WinningPlugin = "",
    };
}
