using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FollowerForge.AssetIndex;
using FollowerForge.BuildPipeline;
using FollowerForge.Domain;
using FollowerForge.ModManagers;
using FollowerForge.SkyrimRecords;
using Serilog;

namespace FollowerForge.Ui;

/// <summary>
/// The seven-step follower wizard. Every choice is made from the user's own installed content
/// by name — no FormKeys, no JSON, no Creation Kit.
/// </summary>
public partial class WizardWindow : Window
{
    private const int StepCount = 7;
    private static readonly ILogger Log = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();

    private int _step;
    private EnvironmentSnapshot? _env;
    private LocationLibrary? _library;
    private string? _lastOutputDir;

    // Full lists, kept so the search boxes can filter without touching the database again.
    private IReadOnlyList<PickerItem> _voices = [], _classes = [], _styles = [], _outfits = [];
    private IReadOnlyList<PickerItem> _weapons = [], _spells = [];
    private IReadOnlyList<FaceItem> _faces = [];
    // Races are split: ten vanilla by default, hundreds of custom ones only on request.
    private IReadOnlyList<PickerItem> _vanillaRaces = [], _customRaces = [];
    private IReadOnlyList<PickerItem> _races => Ctl<CheckBox>("CustomRacesBox").IsChecked == true
        ? [.. _vanillaRaces, .. _customRaces]
        : _vanillaRaces;

    /// <summary>
    /// False until the XAML has finished loading. Control events (ComboBox.SelectionChanged in
    /// particular) fire while the tree is still being built, and looking a control up by name
    /// before then throws "Could not find parent name scope".
    /// </summary>
    private bool _ready;

    public WizardWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _ready = true;
        ShowStep(0);
        _ = LoadEverythingAsync();
    }

    private T Ctl<T>(string name) where T : Control => this.FindControl<T>(name)!;

    // ---------- start-up ----------

    private async Task LoadEverythingAsync()
    {
        var env = Ctl<TextBlock>("EnvLine");
        try
        {
            env.Text = "Looking at your setup…";
            _env = await Task.Run(() => new VortexDiscovery(Log).Discover());

            // Rebuild whenever Vortex has deployed since the catalogue was made, otherwise the
            // wizard offers stale content and reports mods you actually have as "missing".
            var dbPath = CatalogBuilder.DefaultDbPath;
            var fresh = File.Exists(dbPath) && CatalogBuilder.IsFresh(_env!, dbPath);
            if (!fresh)
            {
                env.Text = File.Exists(dbPath)
                    ? "Your mods changed — re-reading them (about a minute)…"
                    : "First run — reading your mods (about a minute)…";
                await Task.Run(() => new CatalogBuilder(Log).Build(_env!));
                // Spawn points come from the same plugins, so they are stale too.
                LocationLibraryBuilder.Invalidate();
            }

            await Task.Run(LoadPickers);

            _library = LocationLibraryBuilder.Load();
            if (_library is null)
            {
                env.Text = "Finding spawn locations…";
                _library = await Task.Run(() => new LocationLibraryBuilder(Log).Build(_env!));
            }
            FillPlaces(null);

            env.Text = $"{_env!.EnabledPluginCount} plugins\n{_library.Locations.Count} known places\n{_faces.Count} exported faces";
            SetStatus("Ready.");
        }
        catch (Exception ex)
        {
            env.Text = "Setup problem";
            SetStatus("Could not read your setup: " + ex.Message);
        }
    }

    /// <summary>Pulls every list the wizard offers straight out of the catalogue.</summary>
    private void LoadPickers()
    {
        using var db = new CatalogDb(CatalogBuilder.DefaultDbPath, Log);

        static string? Best(IndexedRecord r) =>
            !string.IsNullOrWhiteSpace(r.DisplayName) ? r.DisplayName : r.EditorId;

        List<PickerItem> Grab(IndexedRecordType type, Func<IndexedRecord, string?>? detail = null) =>
            db.SearchRecords(type, null, 20000)
                .Where(r => !string.IsNullOrWhiteSpace(Best(r)))
                .Select(r => new PickerItem(Best(r)!, r.FormKey, detail?.Invoke(r) ?? SourceOf(r)))
                .OrderBy(p => p.Display, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Races are filtered and labelled: creature/child/beast-form records are dropped, and
        // custom races say plainly that they become a requirement for anyone who installs her.
        // Showing all ~190 at once was overwhelming, so custom ones are behind a checkbox.
        var offered = RaceSuitability.Offer(db.SearchRecords(IndexedRecordType.Race, null, 20000));
        _vanillaRaces = offered.Where(r => r.Class == RaceClass.Vanilla)
            .Select(r => new PickerItem(r.Name, r.FormKey, r.Note)).ToList();
        _customRaces = offered.Where(r => r.Class != RaceClass.Vanilla)
            .Select(r => new PickerItem(r.Name, r.FormKey, r.Note)).ToList();
        _classes = Grab(IndexedRecordType.Class);
        _outfits = Grab(IndexedRecordType.Outfit);
        _weapons = Grab(IndexedRecordType.Weapon);
        _spells = Grab(IndexedRecordType.Spell);
        _styles = Grab(IndexedRecordType.CombatStyle, r => CombatTags(r) ?? SourceOf(r));
        _voices = db.SearchRecords(IndexedRecordType.VoiceType, null, 20000)
            .Where(r => !string.IsNullOrWhiteSpace(r.EditorId))
            .Select(r => new PickerItem(r.EditorId!, r.FormKey, VoiceLabel(r)))
            .OrderBy(p => p.Detail is not null && p.Detail.StartsWith("FULL") ? 0 : 1)
            .ThenBy(p => p.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _faces = new CharGenDiscovery(Log).Discover(_env!.GameDataPath).Select(e => new FaceItem(e)).ToList();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Fill("RaceList", _races, "Nord");
            Fill("ClassList", _classes, "CombatWarrior1H");
            Fill("OutfitList", _outfits, "Farm");
            Fill("CstyList", _styles, null);
            Fill("VoiceList", _voices, null);
            Fill("WeaponList", _weapons, null);
            Fill("SpellList", _spells, null);
            Ctl<ListBox>("FaceList").ItemsSource = _faces;
        });
    }

    private static string SourceOf(IndexedRecord r) => r.SourceMod ?? r.WinningPlugin;

    /// <summary>Turns the stored combat-style analysis into words a player understands.</summary>
    private static string? CombatTags(IndexedRecord r)
    {
        if (r.DetailJson is null) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(r.DetailJson);
            if (!doc.RootElement.TryGetProperty("Tags", out var tags)) return null;
            var words = tags.EnumerateArray().Select(t => t.GetString()).Where(t => t is not null);
            return string.Join(", ", words);
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>Marks how usable a voice is for an actual follower.</summary>
    private static string VoiceLabel(IndexedRecord r)
    {
        var source = SourceOf(r);
        if (r.DetailJson is null) return source;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(r.DetailJson);
            var cap = doc.RootElement.TryGetProperty("Capability", out var c) ? c.GetString() : null;
            return cap switch
            {
                "FullyCapable" => "FULL FOLLOWER — every recruit/trade/wait line",
                "ResourceIntegrated" => "SOS PACK — lines supplied by the voice pack",
                "NonFollowerCapable" => "no follower lines (she would be silent)",
                _ => "unverified — test it in game",
            };
        }
        catch (System.Text.Json.JsonException) { return source; }
    }

    private void Fill(string listName, IReadOnlyList<PickerItem> items, string? preselect)
    {
        var list = Ctl<ListBox>(listName);
        list.ItemsSource = items;
        if (preselect is not null)
            list.SelectedItem = items.FirstOrDefault(i => i.Display.Contains(preselect, StringComparison.OrdinalIgnoreCase));
    }

    private void FillPlaces(string? query)
    {
        if (_library is null) return;
        var items = LocationLibraryBuilder.Search(_library, query, 300)
            .Where(l => l.Placeable)
            .Select(l => new LocationItem(l))
            .ToList();
        Ctl<ListBox>("PlaceList").ItemsSource = items;
    }

    // ---------- searching ----------

    private static IReadOnlyList<PickerItem> Filter(IReadOnlyList<PickerItem> all, string? q) =>
        string.IsNullOrWhiteSpace(q)
            ? all
            : all.Where(i => i.Display.Contains(q, StringComparison.OrdinalIgnoreCase)
                             || (i.Detail?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

    private void Refilter(string boxName, string listName, IReadOnlyList<PickerItem> all)
    {
        var list = Ctl<ListBox>(listName);
        var keep = list.SelectedItem;
        list.ItemsSource = Filter(all, Ctl<TextBox>(boxName).Text);
        if (keep is not null) list.SelectedItem = keep;
    }

    private void OnRaceSearch(object? s, RoutedEventArgs e) => Refilter("RaceSearch", "RaceList", _races);
    private void OnVoiceSearch(object? s, RoutedEventArgs e) => Refilter("VoiceSearch", "VoiceList", _voices);
    private void OnClassSearch(object? s, RoutedEventArgs e) => Refilter("ClassSearch", "ClassList", _classes);
    private void OnCstySearch(object? s, RoutedEventArgs e) => Refilter("CstySearch", "CstyList", _styles);
    private void OnOutfitSearch(object? s, RoutedEventArgs e) => Refilter("OutfitSearch", "OutfitList", _outfits);
    private void OnWeaponSearch(object? s, RoutedEventArgs e) => Refilter("WeaponSearch", "WeaponList", _weapons);
    private void OnSpellSearch(object? s, RoutedEventArgs e) => Refilter("SpellSearch", "SpellList", _spells);
    private void OnPlaceSearch(object? s, RoutedEventArgs e) => FillPlaces(Ctl<TextBox>("PlaceSearch").Text);

    private void OnNameTyped(object? s, RoutedEventArgs e) => SyncPluginName();

    /// <summary>The hub name and the declaration only matter when copying assets.</summary>
    private void OnHubModeChanged(object? s, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        Ctl<StackPanel>("OwnHubPanel").IsVisible = Ctl<ComboBox>("HubModeBox").SelectedIndex == 2;
    }

    private void OnCustomRacesToggled(object? s, RoutedEventArgs e)
    {
        if (!_ready || _vanillaRaces.Count == 0) return;   // still loading
        Refilter("RaceSearch", "RaceList", _races);
    }

    private void OnFaceSearch(object? s, RoutedEventArgs e)
    {
        var q = Ctl<TextBox>("FaceSearch").Text;
        Ctl<ListBox>("FaceList").ItemsSource = string.IsNullOrWhiteSpace(q)
            ? _faces
            : _faces.Where(f => f.Export.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // ---------- navigation ----------

    /// <summary>The rail doubles as navigation — people expect to click the step they want.</summary>
    private void OnStepClicked(object? s, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (!_ready || s is not Control { Tag: { } tag }) return;
        if (int.TryParse(tag.ToString(), out var step)) ShowStep(step);
    }

    private void OnBack(object? s, RoutedEventArgs e) => ShowStep(_step - 1);

    private void OnNext(object? s, RoutedEventArgs e)
    {
        if (_step == 0 && string.IsNullOrWhiteSpace(Ctl<TextBox>("NameBox").Text))
        {
            SetStatus("Give her a name first.");
            return;
        }
        ShowStep(_step + 1);
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, StepCount - 1);
        for (var i = 0; i < StepCount; i++)
        {
            // Pages are a mix of StackPanel and Grid; both are Panels.
            Ctl<Panel>($"Page{i}").IsVisible = i == _step;
            var tab = Ctl<TextBlock>($"Step{i}");
            tab.Classes.Set("active", i == _step);
        }
        Ctl<Button>("BackButton").IsEnabled = _step > 0;
        Ctl<Button>("NextButton").IsEnabled = _step < StepCount - 1;

        if (_step == 0) SyncPluginName();
        if (_step == StepCount - 1) Ctl<TextBlock>("SummaryText").Text = Summary();
        SetStatus($"Step {_step + 1} of {StepCount}");
    }

    private void SyncPluginName()
    {
        var name = Ctl<TextBox>("NameBox").Text ?? "";
        var safe = new string(name.Where(char.IsLetterOrDigit).ToArray());
        Ctl<TextBox>("PluginBox").Text = safe.Length == 0 ? "" : $"FF_{safe}.esp";
    }

    // ---------- building ----------

    private PickerItem? Picked(string listName) => Ctl<ListBox>(listName).SelectedItem as PickerItem;

    /// <summary>Multi-select lists (weapons, spells) — empty when nothing was chosen.</summary>
    private IReadOnlyList<RecordRef> PickedMany(string listName) =>
        (Ctl<ListBox>(listName).SelectedItems ?? new List<object>())
            .OfType<PickerItem>()
            .Select(p => new RecordRef(p.FormKey))
            .ToList();

    private string Summary()
    {
        SyncPluginName();
        var place = (Ctl<ListBox>("PlaceList").SelectedItem as LocationItem)?.Location;
        var face = (Ctl<ListBox>("FaceList").SelectedItem as FaceItem)?.Export;
        return $"""
            Name      {Ctl<TextBox>("NameBox").Text}   ({Ctl<TextBox>("PluginBox").Text})
            Race      {Picked("RaceList")?.Display ?? "(default Nord)"}
            Face      {face?.Name ?? "(plain default face)"}
            Voice     {Picked("VoiceList")?.Display ?? "(default)"}
            Class     {Picked("ClassList")?.Display ?? "(default warrior)"}
            Combat    {Picked("CstyList")?.Display ?? "(race default)"}
            Outfit    {Picked("OutfitList")?.Display ?? "(farm clothes)"}
            Waits at  {place?.Display ?? "(Whiterun, outside)"}
            """;
    }

    private FollowerProfile BuildProfile()
    {
        SyncPluginName();
        var name = (Ctl<TextBox>("NameBox").Text ?? "Follower").Trim();
        var place = (Ctl<ListBox>("PlaceList").SelectedItem as LocationItem)?.Location;
        var face = (Ctl<ListBox>("FaceList").SelectedItem as FaceItem)?.Export;
        var csty = Picked("CstyList");

        var mortality = Ctl<ComboBox>("MortalBox").SelectedIndex;   // 0 protected, 1 essential, 2 mortal
        var temper = Ctl<ComboBox>("TemperBox").SelectedIndex;      // 0 cautious, 1 balanced, 2 fearless

        return new FollowerProfile
        {
            Name = name,
            PluginName = Ctl<TextBox>("PluginBox").Text!,
            Female = Ctl<ComboBox>("SexBox").SelectedIndex == 0,
            Race = new RecordRef(Picked("RaceList")?.FormKey ?? VanillaForms.NordRace.ToString()),
            VoiceType = new RecordRef(Picked("VoiceList")?.FormKey ?? VanillaForms.FemaleEvenTonedVoice.ToString()),
            Class = new RecordRef(Picked("ClassList")?.FormKey ?? VanillaForms.CombatWarrior1HClass.ToString()),
            Outfit = new RecordRef(Picked("OutfitList")?.FormKey ?? VanillaForms.FarmClothesOutfit.ToString()),
            CombatStyle = csty is null ? null : new CombatStyleChoice
            {
                Style = new RecordRef(csty.FormKey),
                CloneIntoPlugin = Ctl<CheckBox>("CloneCstyBox").IsChecked == true,
            },
            Placement = place is not null
                ? new PlacementSpec { LocationId = place.Id }
                : new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
            Appearance = new AppearanceSpec { CharGenExportName = face?.Name },
            Hub = Ctl<ComboBox>("HubModeBox").SelectedIndex switch
            {
                1 => HubMode.ReferenceInstalled,
                2 => HubMode.OwnHub,
                _ => HubMode.FreeHubs,
            },
            OwnHubPrefix = Ctl<TextBox>("HubPrefixBox").Text,
            // Only ever set from the user ticking the box themselves.
            RedistributionPermission = Ctl<CheckBox>("HubPermissionBox").IsChecked == true
                ? "The author confirmed in Follower Forge that they checked each source mod's " +
                  "permissions and may redistribute these files."
                : null,
            Protected = mortality == 0,
            Essential = mortality == 1,
            Marriageable = Ctl<CheckBox>("MarriageBox").IsChecked == true,
            InventoryItems = PickedMany("WeaponList"),
            Spells = PickedMany("SpellList"),
            Ai = new AiValues
            {
                Aggression = temper == 2 ? (byte)1 : (byte)0,
                Confidence = temper switch { 0 => 1, 2 => 4, _ => 3 },
                Assistance = 2,
            },
        };
    }

    private async void OnBuild(object? s, RoutedEventArgs e)
    {
        var log = Ctl<TextBox>("BuildLog");
        if (_env is null) { log.Text = "Still reading your setup — try again in a moment."; return; }
        if (string.IsNullOrWhiteSpace(Ctl<TextBox>("NameBox").Text)) { log.Text = "She needs a name (step 1)."; return; }

        Ctl<Button>("BuildButton").IsEnabled = false;
        log.Text = "Building…";
        var profile = BuildProfile();
        var wantZip = Ctl<CheckBox>("ZipBox").IsChecked == true;

        try
        {
            var (text, dir) = await Task.Run(() => RunBuild(profile, wantZip));
            log.Text = text;
            _lastOutputDir = dir;
            Ctl<Button>("OpenFolderButton").IsEnabled = dir is not null;
        }
        catch (Exception ex)
        {
            log.Text = "Build failed: " + ex.Message;
        }
        finally
        {
            Ctl<Button>("BuildButton").IsEnabled = true;
        }
    }

    private (string Text, string? Dir) RunBuild(FollowerProfile profile, bool zip)
    {
        var workspace = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FollowerForge", "workspace");
        var dbPath = CatalogBuilder.DefaultDbPath;
        CatalogDb? catalog = File.Exists(dbPath) ? new CatalogDb(dbPath, Log) : null;
        try
        {
            var result = new FollowerBuilder(Log).Build(profile, _env!, workspace, location: null, catalog);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(result.Success ? "DONE — she is ready to install." : "NOT BUILT — see below.");
            sb.AppendLine();
            sb.AppendLine($"Plugin   : {result.Manifest.PluginName}");
            sb.AppendLine($"Requires : {string.Join(", ", result.Manifest.Masters)}");
            sb.AppendLine($"Face     : {(result.Manifest.HasFaceGen ? "custom (from your RaceMenu export)" : "default")}");
            sb.AppendLine($"Folder   : {result.OutputDirectory}");

            if (result.Success && zip)
            {
                var zipPath = new VortexPackager(Log)
                    .Package(result.OutputDirectory, Path.GetFileNameWithoutExtension(profile.PluginName));
                sb.AppendLine($"Zip      : {zipPath}");
            }

            var sharing = result.Validation.Findings
                .Where(f => f.Code.StartsWith("SHARING_", StringComparison.Ordinal))
                .ToList();
            if (sharing.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("If you share her:");
                foreach (var f in sharing) sb.AppendLine($"  • {f.Message}");
            }

            var problems = result.Validation.Findings
                .Where(f => f.Severity is ValidationSeverity.Warning or ValidationSeverity.Error)
                .ToList();
            if (problems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Worth a look:");
                foreach (var f in problems) sb.AppendLine($"  • {f.Message}");

                if (problems.Any(f => f.Code == "FACEGEN_TEX_MISSING"))
                {
                    sb.AppendLine();
                    sb.AppendLine("  Those textures are baked into the face you exported. Either install the");
                    sb.AppendLine("  mod that provides them, or load the preset in RaceMenu and press F5 again");
                    sb.AppendLine("  with your current mods so the face points at files you actually have.");
                }
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("No problems found.");
            }
            sb.AppendLine();
            sb.AppendLine("Install the folder (or zip) with Vortex, then find her at the place you chose.");
            return (sb.ToString(), result.Success ? result.OutputDirectory : null);
        }
        finally { catalog?.Dispose(); }
    }

    private void OnOpenFolder(object? s, RoutedEventArgs e)
    {
        if (_lastOutputDir is null) return;
        if (!Directory.Exists(_lastOutputDir))
        {
            SetStatus("That folder is gone — build her again.");
            return;
        }
        // Launch Explorer with the folder as an argument. Shell-executing the directory path
        // itself is what produced the "Location is not available" dialog.
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{_lastOutputDir.TrimEnd(Path.DirectorySeparatorChar)}\"",
            UseShellExecute = true,
        });
    }

    private void SetStatus(string text) => Ctl<TextBlock>("StatusLine").Text = text;
}
