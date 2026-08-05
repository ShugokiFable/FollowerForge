using Avalonia;
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
    private const double SkillValueEditorWidth = 150;
    private static readonly ILogger Log = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();

    private int _step;
    private EnvironmentSnapshot? _env;
    private LocationLibrary? _library;
    private string? _lastOutputDir;

    // Full lists, kept so the search boxes can filter without touching the database again.
    private IReadOnlyList<PickerItem> _voices = [], _classes = [], _styles = [], _outfits = [];
    private IReadOnlyList<PickerItem> _weapons = [], _spells = [], _perks = [];
    private IReadOnlyList<PickerItem> _armorTorso = [], _armorHead = [], _armorHands = [];
    private IReadOnlyList<PickerItem> _armorFeet = [], _armorShield = [], _armorAccessories = [], _armorOther = [];
    private readonly HashSet<string> _selectedArmor = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedWeapons = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedSpells = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedPerks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _selectedLore = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<PickerItem> _books = [], _miscItems = [], _ingestibles = [], _ingredients = [];
    /// <summary>LocType keywords, for lines that only apply in a kind of place.</summary>
    private IReadOnlyList<PickerItem> _placeKeywords = [];
    private readonly Dictionary<FollowerSkill, NumericUpDown> _skillBoxes = [];
    private readonly List<DialogueLine> _lines = [];
    private readonly List<KinItem> _kin = [];
    private readonly List<LocationItem> _alternateSpawns = [];
    private readonly VoiceModelCatalog _voiceModels = new();

    /// <summary>
    /// How much dialogue each voice type already inherits from installed mods (RDO and anything
    /// else that keys lines to a voice). Null until the scan has been run at least once.
    /// </summary>
    private Dictionary<string, VoiceCoverage>? _coverage;
    private bool _restoringMultiSelection;
    private IReadOnlyList<FaceItem> _faces = [];
    // Races are split: ten vanilla by default, hundreds of custom ones only on request.
    private IReadOnlyList<PickerItem> _vanillaRaces = [], _customRaces = [], _creatureRaces = [];
    private IReadOnlyList<PickerItem> _races
    {
        get
        {
            var races = new List<PickerItem>(_vanillaRaces);
            if (Ctl<CheckBox>("CustomRacesBox").IsChecked == true) races.AddRange(_customRaces);
            if (Ctl<CheckBox>("CreatureRacesBox").IsChecked == true) races.AddRange(_creatureRaces);
            return races;
        }
    }

    /// <summary>
    /// False until the XAML has finished loading. Control events (ComboBox.SelectionChanged in
    /// particular) fire while the tree is still being built, and looking a control up by name
    /// before then throws "Could not find parent name scope".
    /// </summary>
    private bool _ready;

    public WizardWindow()
    {
        AvaloniaXamlLoader.Load(this);
        BuildSkillEditor();
        ApplyStatPreset(FollowerStatPreset.BlankSlate);
        _ready = true;
        UpdateVoiceSynthStatus();
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

            try
            {
                await PrepareCatalogAsync(env);
            }
            catch (Exception ex) when (CatalogDb.IsCacheFailure(ex))
            {
                env.Text = "The catalogue cache was damaged — repairing it automatically…";
                CatalogDb.QuarantineBrokenCache(CatalogBuilder.DefaultDbPath, Log);
                LocationLibraryBuilder.Invalidate();
                await Task.Run(() => new CatalogBuilder(Log).Build(_env!));
                await Task.Run(LoadPickers);
            }

            _library = LocationLibraryBuilder.Load();
            if (_library is null)
            {
                env.Text = "Finding spawn locations…";
                _library = await Task.Run(() => new LocationLibraryBuilder(Log).Build(_env!));
            }
            FillPlaces(null);

            // Which voices already have follower and marriage dialogue is read off the real load
            // order. Without it the build can only say "cannot tell whether this voice can marry",
            // and telling someone to go run a CLI command for that is not an answer.
            if (_coverage is null)
            {
                env.Text = "Reading what your mods already say (one time)…";
                await Task.Run(ScanVoiceCoverage);
            }

            env.Text =$"{_env!.EnabledPluginCount} plugins\n{_library.Locations.Count} known places\n{_faces.Count} exported faces";
            SetStatus("Ready.");
        }
        catch (Exception ex)
        {
            env.Text = "Setup problem";
            SetStatus("Could not read your setup: " + ex.Message);
        }
    }

    private async Task PrepareCatalogAsync(TextBlock env)
    {
        // Rebuild whenever Vortex has deployed since the catalogue was made, otherwise the
        // wizard offers stale content and reports mods you actually have as missing.
        var dbPath = CatalogBuilder.DefaultDbPath;
        var fresh = File.Exists(dbPath) && CatalogBuilder.IsFresh(_env!, dbPath);
        if (!fresh)
        {
            env.Text = File.Exists(dbPath)
                ? "Your mods changed — re-reading them (about a minute)…"
                : "First run — reading your mods (about a minute)…";
            await Task.Run(() => new CatalogBuilder(Log).Build(_env!));
            LocationLibraryBuilder.Invalidate();
        }
        await Task.Run(LoadPickers);
    }

    /// <summary>
    /// Builds the voice-dialogue library from the live load order, then reloads the pickers so the
    /// voice list carries what each voice already says. Failure here is never fatal — the wizard
    /// simply keeps reporting marriage support as unknown.
    /// </summary>
    private void ScanVoiceCoverage()
    {
        try
        {
            var (entries, _) = new LoadOrderBuilder(Log).BuildEntryList(_env!);
            var enabled = entries.Where(e => e.Enabled).Select(e => e.PluginFileName);
            VoiceCoverageScanner.Save(
                new VoiceCoverageScanner(Log).Scan(_env!.GameDataPath, enabled));
            LoadPickers();
        }
        catch (Exception ex)
        {
            Log.Warning("Voice coverage scan failed: {Error}", ex.Message);
        }
    }

    /// <summary>Pulls every list the wizard offers straight out of the catalogue.</summary>
    private void LoadPickers()
    {
        using var db = new CatalogDb(CatalogBuilder.DefaultDbPath, Log);
        const int AllPickerRecords = int.MaxValue;

        static string? Best(IndexedRecord r) =>
            !string.IsNullOrWhiteSpace(r.DisplayName) ? r.DisplayName : r.EditorId;

        // Every row says whether picking it costs the downloader another mod. Ordering stays
        // alphabetical here — people search these lists for a specific thing by name.
        List<PickerItem> Grab(IndexedRecordType type, Func<IndexedRecord, string?>? detail = null) =>
            db.SearchRecords(type, null, AllPickerRecords)
                .Where(r => !string.IsNullOrWhiteSpace(Best(r)))
                .Select(r => new PickerItem(
                    Best(r)!, r.FormKey, detail?.Invoke(r) ?? SourceOf(r),
                    badge: IsBaseGame(r) ? "BASE GAME" : "MOD",
                    badgeKind: IsBaseGame(r) ? "good" : "dim"))
                .OrderBy(p => p.Display, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Dialogue a voice already inherits from installed mods. Loaded before the voice list is
        // built so the labels can carry it; absent until the user runs the scan.
        _coverage = VoiceCoverageScanner.Load()?.Voices
            .ToDictionary(v => v.VoiceFormKey, v => v, StringComparer.OrdinalIgnoreCase);

        // Races are filtered and labelled: creature/child/beast-form records are dropped, and
        // custom races say plainly that they become a requirement for anyone who installs her.
        // Showing all ~190 at once was overwhelming, so custom ones are behind a checkbox.
        var offered = RaceSuitability.Offer(
            db.SearchRecords(IndexedRecordType.Race, null, AllPickerRecords), includeCreatures: true);
        _vanillaRaces = offered.Where(r => r.Class == RaceClass.Vanilla).Select(RaceRow).ToList();
        _customRaces = offered.Where(r => r.Class is not RaceClass.Vanilla and not RaceClass.Creature)
            .Select(RaceRow).ToList();
        // Creatures are kept apart so they can never appear unless deliberately asked for.
        _creatureRaces = offered.Where(r => r.Class == RaceClass.Creature).Select(RaceRow).ToList();
        _classes = Grab(IndexedRecordType.Class);
        _outfits = Grab(IndexedRecordType.Outfit);
        _weapons = Grab(IndexedRecordType.Weapon);
        _spells = Grab(IndexedRecordType.Spell);
        _books = Grab(IndexedRecordType.Book);
        _miscItems = Grab(IndexedRecordType.MiscItem);
        _ingestibles = Grab(IndexedRecordType.Ingestible);
        _ingredients = Grab(IndexedRecordType.Ingredient);
        // Only LocType* keywords are useful here; the rest of the ~2000 are noise.
        _placeKeywords = db.SearchRecords(IndexedRecordType.Keyword, "LocType", AllPickerRecords)
            .Where(r => r.EditorId is { Length: > 0 } e && e.StartsWith("LocType", StringComparison.Ordinal))
            .Select(r => new PickerItem(Friendly(r.EditorId!), r.FormKey, r.EditorId))
            .OrderBy(p => p.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _perks = Grab(IndexedRecordType.Perk);
        var armor = db.SearchRecords(IndexedRecordType.Armor, null, AllPickerRecords)
            .Where(r => !string.IsNullOrWhiteSpace(Best(r)))
            .Select(r => (
                Item: new PickerItem(
                    Best(r)!, r.FormKey, ArmorLabel(r),
                    badge: IsBaseGame(r) ? "BASE GAME" : "MOD",
                    badgeKind: IsBaseGame(r) ? "good" : "dim"),
                Group: ArmorGroup(r)))
            .OrderBy(pair => pair.Item.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _armorTorso = armor.Where(a => a.Group == "Torso").Select(a => a.Item).ToList();
        _armorHead = armor.Where(a => a.Group == "Head").Select(a => a.Item).ToList();
        _armorHands = armor.Where(a => a.Group == "Hands").Select(a => a.Item).ToList();
        _armorFeet = armor.Where(a => a.Group == "Feet").Select(a => a.Item).ToList();
        _armorShield = armor.Where(a => a.Group == "Shield").Select(a => a.Item).ToList();
        _armorAccessories = armor.Where(a => a.Group == "Accessories").Select(a => a.Item).ToList();
        _armorOther = armor.Where(a => a.Group == "Other").Select(a => a.Item).ToList();
        _styles = Grab(IndexedRecordType.CombatStyle, r => CombatTags(r) ?? SourceOf(r));
        // Ordered by how useful a voice actually is, then by name. Alphabetical alone buried the
        // whole SOS pack among ~600 creature voices, which is exactly how it reads in game too.
        _voices = db.SearchRecords(IndexedRecordType.VoiceType, null, AllPickerRecords)
            .Where(r => !string.IsNullOrWhiteSpace(r.EditorId))
            .Where(r => VoiceSuitability.IsAllowed(r.EditorId))
            .Select(r => VoiceItem(r, db))
            .OrderBy(p => p.Tier)
            .ThenBy(p => p.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Usable faces first: the ones that would fail the build sink to the bottom rather than
        // sitting among the good ones looking identical.
        _faces = new CharGenDiscovery(Log).Discover(_env!.GameDataPath)
            .Select(e => new FaceItem(e))
            .OrderBy(f => f.IsUsable ? 0 : 1)
            .ThenBy(f => f.Export.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Fill("RaceList", _races, "Nord");
            Fill("ClassList", _classes, "CombatWarrior1H");
            Fill("OutfitList", _outfits, null);
            Fill("CstyList", _styles, null);
            RefreshVoices();
            Fill("WeaponList", _weapons, null);
            Fill("LoreList", _books, null);
            FillPlaceKeywords();
            Fill("SpellList", _spells, null);
            Fill("PerkList", _perks, null);
            Fill("ArmorTorsoList", _armorTorso, null);
            Fill("ArmorHeadList", _armorHead, null);
            Fill("ArmorHandsList", _armorHands, null);
            Fill("ArmorFeetList", _armorFeet, null);
            Fill("ArmorShieldList", _armorShield, null);
            Fill("ArmorAccessoriesList", _armorAccessories, null);
            Fill("ArmorOtherList", _armorOther, null);
            Ctl<ListBox>("FaceList").ItemsSource = _faces;
        });
    }

    private static string SourceOf(IndexedRecord r) =>
        r.SourceMod is { Length: > 0 } mod ? ModNames.Pretty(mod) : r.WinningPlugin;

    /// <summary>
    /// True when the winning version of this record comes from the game itself, so choosing it
    /// adds no requirement for anyone who installs her. Judged on the WINNING plugin: a vanilla
    /// sword a mod overrides is that mod's sword now.
    /// </summary>
    private static bool IsBaseGame(IndexedRecord r) =>
        VanillaMasters.Contains(r.WinningPlugin);

    private static readonly HashSet<string> VanillaMasters = new(StringComparer.OrdinalIgnoreCase)
    {
        "Skyrim.esm", "Update.esm", "Dawnguard.esm", "HearthFires.esm", "Dragonborn.esm",
    };

    /// <summary>
    /// A race row says what it costs the downloader. Vanilla is free; anything else becomes a
    /// hard requirement for everyone who installs her, and a creature can never have a face.
    /// </summary>
    private static PickerItem RaceRow(RaceOption r) => new(
        r.Name, r.FormKey, r.Note, (int)r.Class,
        r.Class switch
        {
            RaceClass.Vanilla => "VANILLA",
            RaceClass.CustomPlayable => "NEEDS A MOD",
            RaceClass.Creature => "CREATURE",
            _ => "MOD RACE",
        },
        r.Class switch
        {
            RaceClass.Vanilla => "good",
            RaceClass.Creature => "warn",
            _ => "dim",
        });

    private static string ArmorGroup(IndexedRecord r)
    {
        if (r.DetailJson is null) return "Other";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(r.DetailJson);
            return doc.RootElement.TryGetProperty("SlotGroup", out var group)
                ? group.GetString() ?? "Other"
                : "Other";
        }
        catch (System.Text.Json.JsonException) { return "Other"; }
    }

    private static string ArmorLabel(IndexedRecord r)
    {
        var source = SourceOf(r);
        if (r.DetailJson is null) return $"Other • slots unknown • {source}";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(r.DetailJson);
            var root = doc.RootElement;
            var group = root.TryGetProperty("SlotGroup", out var g) ? g.GetString() ?? "Other" : "Other";
            var type = root.TryGetProperty("ArmorType", out var t) ? t.GetString() ?? "Unknown" : "Unknown";
            var slots = root.TryGetProperty("BipedSlots", out var s)
                ? string.Join("+", s.EnumerateArray().Select(v => v.GetString()).Where(v => v is not null))
                : "unknown slots";
            return $"{group} • {type} • {slots} • {source}";
        }
        catch (System.Text.Json.JsonException) { return $"Other • slots invalid • {source}"; }
    }

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

    /// <summary>
    /// One voice as the user sees it: its tier, a chip, and a sentence about what she gets.
    ///
    /// Whether a voice pack's files are really installed is checked here rather than at index
    /// time, because the asset index is built after the records and so is always empty when the
    /// classifier runs. That left every SOS voice reading "not confirmed on disk" even with all
    /// its .fuz files present.
    /// </summary>
    private PickerItem VoiceItem(IndexedRecord r, CatalogDb db)
    {
        var capability = VoiceRanking.CapabilityOf(CapabilityJson(r));
        var tier = VoiceRanking.TierOf(capability);
        var source = SourceOf(r);

        var what = tier switch
        {
            VoiceTier.Vanilla => "Every recruit, trade and wait line — nothing extra needed",
            VoiceTier.VoicePack => VoiceRanking.VoiceFolders(r.EditorId!).Any(db.AssetPathPrefixExists)
                ? "Simply Open Source Voice Pack — voice files installed"
                : "Simply Open Source Voice Pack — listed, but its voice files are NOT on disk",
            VoiceTier.NoFollowerLines => $"{source} — no follower dialogue, she would be silent",
            _ => $"{source} — allows generic dialogue, unverified",
        };

        return new PickerItem(
            r.EditorId!, r.FormKey, Join(what, CoverageLabel(r.FormKey)),
            (int)tier, VoiceRanking.Badge(tier), VoiceRanking.BadgeKind(tier));
    }

    private static string? CapabilityJson(IndexedRecord r)
    {
        if (r.DetailJson is null) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(r.DetailJson);
            return doc.RootElement.TryGetProperty("Capability", out var c) ? c.GetString() : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
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

    private void RefilterMany(
        string boxName, string listName, IReadOnlyList<PickerItem> all,
        HashSet<string> remembered)
    {
        var list = Ctl<ListBox>(listName);
        var visible = Filter(all, Ctl<TextBox>(boxName).Text);
        _restoringMultiSelection = true;
        try
        {
            list.ItemsSource = visible;
            if (list.SelectedItems is { } selected)
                foreach (var item in visible.Where(i => remembered.Contains(i.FormKey)))
                    selected.Add(item);
        }
        finally { _restoringMultiSelection = false; }
    }

    private void UpdateRememberedSelection(HashSet<string> remembered, SelectionChangedEventArgs e)
    {
        if (_restoringMultiSelection) return;
        foreach (var item in e.RemovedItems.OfType<PickerItem>())
            remembered.Remove(item.FormKey);
        foreach (var item in e.AddedItems.OfType<PickerItem>())
            remembered.Add(item.FormKey);
    }

    private void OnRaceSearch(object? s, RoutedEventArgs e) => Refilter("RaceSearch", "RaceList", _races);

    // ---------- voices ----------

    /// <summary>
    /// The voices on offer. 598 of the 1,018 on a real load order are creature or unique voices
    /// with no follower dialogue at all; showing them by default is what buried the SOS pack.
    /// </summary>
    private IReadOnlyList<PickerItem> VoiceSource() =>
        Ctl<ComboBox>("VoiceScopeBox").SelectedIndex == 1
            ? _voices
            : _voices.Where(v => VoiceRanking.IsFollowerReady((VoiceTier)v.Tier)).ToList();

    private void OnVoiceSearch(object? s, RoutedEventArgs e) => RefreshVoices();

    private void OnVoiceScopeChanged(object? s, SelectionChangedEventArgs e)
    {
        if (_ready) RefreshVoices();
    }

    private void RefreshVoices()
    {
        var source = VoiceSource();
        Refilter("VoiceSearch", "VoiceList", source);

        var hidden = _voices.Count - source.Count;
        Ctl<TextBlock>("VoiceCountLine").Text = hidden > 0
            ? $"{source.Count:N0} voices she can use  ·  {hidden:N0} creature and unique voices hidden"
            : $"{source.Count:N0} voices";
    }

    // ---------- books and belongings ----------

    private IReadOnlyList<PickerItem> LoreSource() => Ctl<ComboBox>("LoreKindBox").SelectedIndex switch
    {
        1 => _miscItems,
        2 => _ingestibles,
        3 => _ingredients,
        _ => _books,
    };

    private void OnLoreKindChanged(object? s, SelectionChangedEventArgs e)
    {
        if (_ready) RefilterMany("LoreSearch", "LoreList", LoreSource(), _selectedLore);
    }

    private void OnLoreSearch(object? s, RoutedEventArgs e) =>
        RefilterMany("LoreSearch", "LoreList", LoreSource(), _selectedLore);

    private void OnLoreSelectionChanged(object? s, SelectionChangedEventArgs e) =>
        UpdateRememberedSelection(_selectedLore, e);

    /// <summary>"LocTypeAnimalDen" reads badly in a menu; "Animal Den" does not.</summary>
    private static string Friendly(string keywordEditorId)
    {
        var bare = keywordEditorId["LocType".Length..];
        var spaced = string.Concat(bare.Select((c, i) =>
            i > 0 && char.IsUpper(c) && !char.IsUpper(bare[i - 1]) ? " " + c : c.ToString()));
        return spaced.Length == 0 ? keywordEditorId : spaced;
    }

    private void FillPlaceKeywords()
    {
        var box = Ctl<ComboBox>("LinePlaceBox");
        var items = new List<object> { "Anywhere" };
        items.AddRange(_placeKeywords);
        box.ItemsSource = items;
        box.SelectedIndex = 0;
    }

    private LineContext BuildLineContext() => new()
    {
        LocationKeyword = Ctl<ComboBox>("LinePlaceBox").SelectedItem is PickerItem place
            ? new RecordRef(place.FormKey)
            : null,
        Time = (TimeOfDay)Math.Max(0, Ctl<ComboBox>("LineTimeBox").SelectedIndex),
    };

    // ---------- evolution (experimental) ----------

    private void OnEvolveToggled(object? s, RoutedEventArgs e)
    {
        if (!_ready) return;
        var on = Ctl<CheckBox>("EvolveBox").IsChecked == true;
        Ctl<Grid>("EvolveOptions").IsEnabled = on;

        // Spell out what she will actually be like at the start, because the temperament box
        // above stops applying the moment this is switched on.
        Ctl<TextBlock>("EvolveNote").Text = on
            ? "She will start Cowardly and run from danger — the temperament above no longer " +
              "applies. Her confidence, combat skills, health, stamina and magicka rise at each " +
              "phase. Her phase is stored in a global you can read or change from the console."
            : "";
    }

    private EvolutionSpec BuildEvolution()
    {
        if (Ctl<CheckBox>("EvolveBox").IsChecked != true) return new EvolutionSpec();
        return new EvolutionSpec
        {
            Enabled = true,
            Phases = (int)(Ctl<NumericUpDown>("EvolvePhases").Value ?? 3),
            CombatsPerPhase = (int)(Ctl<NumericUpDown>("EvolveCombats").Value ?? 25),
            StartConfidence = 0,
            EndConfidence = (byte)Math.Clamp(Ctl<ComboBox>("EvolveEndBox").SelectedIndex, 0, 4),
        };
    }

    // ---------- alternate spawn points ----------

    private void OnAddAlternateSpawn(object? s, RoutedEventArgs e)
    {
        if (Ctl<ListBox>("PlaceList").SelectedItem is not LocationItem picked)
        {
            ShowSpawnError("Pick a place from the list above first.");
            return;
        }
        if (_alternateSpawns.Count >= 4)
        {
            ShowSpawnError("Four places is the most she can choose between.");
            return;
        }
        if (_alternateSpawns.Any(x => x.Location.Id == picked.Location.Id))
        {
            ShowSpawnError($"{picked.Location.Display} is already in the list.");
            return;
        }
        if (!picked.Location.Placeable)
        {
            ShowSpawnError($"{picked.Location.Display} is an outdoor grid cell and cannot hold a marker.");
            return;
        }
        ShowSpawnError(null);
        _alternateSpawns.Add(picked);
        RefreshAlternateSpawns();
    }

    private void OnRemoveAlternateSpawn(object? s, RoutedEventArgs e)
    {
        if (Ctl<ListBox>("AlternateSpawnList").SelectedItem is not LocationItem item) return;
        _alternateSpawns.Remove(item);
        RefreshAlternateSpawns();
    }

    private void RefreshAlternateSpawns() =>
        Ctl<ListBox>("AlternateSpawnList").ItemsSource = _alternateSpawns.ToList();

    private void ShowSpawnError(string? message)
    {
        var block = Ctl<TextBlock>("SpawnError");
        block.Text = message ?? "";
        block.IsVisible = message is not null;
    }

    private void OnE2AToggled(object? s, RoutedEventArgs e)
    {
        if (!_ready) return;
        var on = Ctl<CheckBox>("E2ABox").IsChecked == true;
        Ctl<StackPanel>("E2AOptions").IsVisible = on;
        Ctl<TextBlock>("E2ANote").Text = on
            ? _alternateSpawns.Count == 0
                ? "Add at least one place above — otherwise her hostile form has nowhere to be found, "
                  + "and she could never be beaten or recruited."
                : $"Her hostile form waits at one of {_alternateSpawns.Count} place(s). The place chosen "
                  + "in the list above is where she appears once summoned."
            : "";
    }

    private EnemyToAllySpec BuildEnemyToAlly()
    {
        if (Ctl<CheckBox>("E2ABox").IsChecked != true) return new EnemyToAllySpec();
        return new EnemyToAllySpec
        {
            Enabled = true,
            Company = (HostileCompany)Math.Max(0, Ctl<ComboBox>("E2ACompanyBox").SelectedIndex),
            LocationIds = _alternateSpawns.Select(x => x.Location.Id).ToList(),
        };
    }

    // ---------- transformation (experimental) ----------

    private void OnTransformKindChanged(object? s, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        var custom = Ctl<ComboBox>("TransformKindBox").SelectedIndex == 2;
        Ctl<Grid>("TransformCustom").IsVisible = custom;
        if (custom && Ctl<ListBox>("TransformRaceList").ItemsSource is null)
        {
            // All races here, not the follower-suitable subset — a beast form is the whole point.
            Fill("TransformRaceList", [.. _vanillaRaces, .. _customRaces], null);
            Fill("TransformSpellList", _spells, null);
        }
    }

    private void OnTransformRaceSearch(object? s, RoutedEventArgs e) =>
        Refilter("TransformRaceSearch", "TransformRaceList", [.. _vanillaRaces, .. _customRaces]);

    private void OnTransformSpellSearch(object? s, RoutedEventArgs e) =>
        Refilter("TransformSpellSearch", "TransformSpellList", _spells);

    private TransformSpec BuildTransformation()
    {
        var kind = Ctl<ComboBox>("TransformKindBox").SelectedIndex switch
        {
            1 => TransformKind.Werewolf,
            2 => TransformKind.Custom,
            _ => TransformKind.None,
        };
        if (kind == TransformKind.None) return new TransformSpec();

        var race = Picked("TransformRaceList");
        var spell = Picked("TransformSpellList");
        return new TransformSpec
        {
            Kind = kind,
            BeastRace = kind == TransformKind.Custom && race is not null ? new RecordRef(race.FormKey) : null,
            OnTransformSpell = kind == TransformKind.Custom && spell is not null ? new RecordRef(spell.FormKey) : null,
            RevertOutOfCombat = Ctl<CheckBox>("TransformRevertBox").IsChecked == true,
        };
    }

    private string TransformSummary()
    {
        var spec = BuildTransformation();
        if (!spec.IsUsable)
        {
            return Ctl<ComboBox>("TransformKindBox").SelectedIndex == 2
                ? "EXPERIMENTAL — custom chosen but no race or spell picked, so nothing will happen"
                : "(none)";
        }
        var what = spec.Kind == TransformKind.Werewolf
            ? "werewolf"
            : string.Join(" + ", new[]
            {
                Picked("TransformRaceList")?.Display,
                Picked("TransformSpellList")?.Display,
            }.Where(x => x is not null));
        return $"EXPERIMENTAL — turns into {what} in combat"
             + (spec.RevertOutOfCombat ? ", reverts after" : ", stays that way");
    }

    // ---------- relationships with other people ----------

    /// <summary>
    /// NPCs are the biggest table in the catalogue, so this searches on demand instead of filling
    /// a list with thousands of names nobody will scroll through.
    /// </summary>
    private void OnKinSearch(object? s, RoutedEventArgs e)
    {
        if (!_ready) return;
        var text = (Ctl<TextBox>("KinSearch").Text ?? "").Trim();
        if (text.Length < 2)
        {
            Ctl<ListBox>("KinCandidates").ItemsSource = null;
            return;
        }

        using var db = new CatalogDb(CatalogBuilder.DefaultDbPath, Log);
        Ctl<ListBox>("KinCandidates").ItemsSource = db
            .SearchRecords(IndexedRecordType.Npc, text, 200)
            .Where(r => !string.IsNullOrWhiteSpace(r.DisplayName))
            .Select(r => new PickerItem(r.DisplayName!, r.FormKey, SourceOf(r)))
            .OrderBy(p => p.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void OnAddKin(object? s, RoutedEventArgs e)
    {
        if (Ctl<ListBox>("KinCandidates").SelectedItem is not PickerItem picked)
        {
            ShowKinError("Search for someone and select them from the list first.");
            return;
        }
        if (_kin.Any(k => k.Relationship.Npc.FormKey.Equals(picked.FormKey, StringComparison.OrdinalIgnoreCase)))
        {
            ShowKinError($"She already has a relationship with {picked.Display}.");
            return;
        }
        ShowKinError(null);

        _kin.Add(new KinItem(
            new NpcRelationship
            {
                Npc = new RecordRef(picked.FormKey),
                Rank = (RelationshipRank)Math.Max(0, Ctl<ComboBox>("KinRankBox").SelectedIndex),
            },
            picked.Display));
        RefreshKinList();
    }

    private void OnRemoveKin(object? s, RoutedEventArgs e)
    {
        if (Ctl<ListBox>("KinList").SelectedItem is not KinItem item) return;
        _kin.Remove(item);
        RefreshKinList();
    }

    private void RefreshKinList() => Ctl<ListBox>("KinList").ItemsSource = _kin.ToList();

    private void ShowKinError(string? message)
    {
        var block = Ctl<TextBlock>("KinError");
        block.Text = message ?? "";
        block.IsVisible = message is not null;
    }

    // ---------- custom lines ----------

    private void OnVoiceSelected(object? s, SelectionChangedEventArgs e)
    {
        if (_ready) UpdateVoiceSynthStatus();
    }

    /// <summary>
    /// Says up front whether these lines can actually be spoken in the chosen voice, rather than
    /// letting the user write a dozen of them and discover at build time that they will be silent.
    /// </summary>
    private void UpdateVoiceSynthStatus()
    {
        var status = Ctl<TextBlock>("VoiceSynthStatus");
        var voice = Picked("VoiceList")?.Display;   // for voice types the display name IS the EditorID

        if (!_voiceModels.Installed)
        {
            status.Text = "xVASynth was not found, so custom lines would be silent subtitles. " +
                          "Install xVASynth (with its lip_fuz plugin) to have them spoken aloud.";
            return;
        }
        if (!_voiceModels.CanMakeFuz)
        {
            status.Text = "xVASynth is installed but its lip_fuz plugin is missing — lines could be " +
                          "spoken but her mouth would not move. Enable lip_fuz in xVASynth.";
            return;
        }
        status.Text = voice is null
            ? $"xVASynth is ready with {_voiceModels.Models.Count} voice models. Pick a voice to check it."
            : _voiceModels.CanSpeak(voice)
                ? $"Ready — “{voice}” can be spoken with lip sync."
                : $"xVASynth has no model for “{voice}”, so lines in this voice would be silent. " +
                  "Pick a different voice, or download that voice model in xVASynth.";
    }

    private void OnLineTriggerChanged(object? s, SelectionChangedEventArgs e)
    {
        // Only a player-facing topic needs a menu entry; the rest are spoken unprompted.
        if (_ready)
            Ctl<TextBox>("LinePromptBox").IsVisible = Ctl<ComboBox>("LineTriggerBox").SelectedIndex == 3;
    }

    private void OnAddLine(object? s, RoutedEventArgs e)
    {
        var text = (Ctl<TextBox>("LineTextBox").Text ?? "").Trim();
        var trigger = (DialogueTrigger)Math.Max(0, Ctl<ComboBox>("LineTriggerBox").SelectedIndex);
        var prompt = (Ctl<TextBox>("LinePromptBox").Text ?? "").Trim();

        if (text.Length == 0) { ShowLineError("Type what she says first."); return; }
        if (trigger == DialogueTrigger.PlayerTopic && prompt.Length == 0)
        {
            ShowLineError("A topic needs the menu entry the player clicks, or it cannot be selected.");
            return;
        }
        ShowLineError(null);

        var emotion = (LineEmotion)Math.Max(0, Ctl<ComboBox>("LineEmotionBox").SelectedIndex);
        _lines.Add(new DialogueLine
        {
            Text = text,
            Trigger = trigger,
            Prompt = trigger == DialogueTrigger.PlayerTopic ? prompt : null,
            Emotion = emotion,
            // A deliberately chosen emotion should read on her face; neutral stays mid-range.
            EmotionValue = emotion == LineEmotion.Neutral ? 50u : 75u,
            Context = BuildLineContext(),
        });

        Ctl<TextBox>("LineTextBox").Text = "";
        RefreshLineList();
    }

    private void OnRemoveLine(object? s, RoutedEventArgs e)
    {
        if (Ctl<ListBox>("LineList").SelectedItem is not LineItem item) return;
        _lines.Remove(item.Line);
        RefreshLineList();
    }

    private void RefreshLineList() =>
        Ctl<ListBox>("LineList").ItemsSource = _lines.Select(l => new LineItem(l)).ToList();

    private void ShowLineError(string? message)
    {
        var block = Ctl<TextBlock>("LineError");
        block.Text = message ?? "";
        block.IsVisible = message is not null;
    }
    private void OnClassSearch(object? s, RoutedEventArgs e) => Refilter("ClassSearch", "ClassList", _classes);
    private void OnCstySearch(object? s, RoutedEventArgs e) => Refilter("CstySearch", "CstyList", _styles);
    private void OnOutfitSearch(object? s, RoutedEventArgs e) => Refilter("OutfitSearch", "OutfitList", _outfits);
    private void OnArmorSearch(object? s, RoutedEventArgs e)
    {
        RefilterMany("ArmorSearch", "ArmorTorsoList", _armorTorso, _selectedArmor);
        RefilterMany("ArmorSearch", "ArmorHeadList", _armorHead, _selectedArmor);
        RefilterMany("ArmorSearch", "ArmorHandsList", _armorHands, _selectedArmor);
        RefilterMany("ArmorSearch", "ArmorFeetList", _armorFeet, _selectedArmor);
        RefilterMany("ArmorSearch", "ArmorShieldList", _armorShield, _selectedArmor);
        RefilterMany("ArmorSearch", "ArmorAccessoriesList", _armorAccessories, _selectedArmor);
        RefilterMany("ArmorSearch", "ArmorOtherList", _armorOther, _selectedArmor);
    }
    private void OnWeaponSearch(object? s, RoutedEventArgs e) =>
        RefilterMany("WeaponSearch", "WeaponList", _weapons, _selectedWeapons);
    private void OnSpellSearch(object? s, RoutedEventArgs e) =>
        RefilterMany("SpellSearch", "SpellList", _spells, _selectedSpells);
    private void OnPerkSearch(object? s, RoutedEventArgs e) =>
        RefilterMany("PerkSearch", "PerkList", _perks, _selectedPerks);
    private void OnPlaceSearch(object? s, RoutedEventArgs e) => FillPlaces(Ctl<TextBox>("PlaceSearch").Text);

    private void OnArmorSelectionChanged(object? s, SelectionChangedEventArgs e) =>
        UpdateRememberedSelection(_selectedArmor, e);
    private void OnWeaponSelectionChanged(object? s, SelectionChangedEventArgs e) =>
        UpdateRememberedSelection(_selectedWeapons, e);
    private void OnSpellSelectionChanged(object? s, SelectionChangedEventArgs e) =>
        UpdateRememberedSelection(_selectedSpells, e);
    private void OnPerkSelectionChanged(object? s, SelectionChangedEventArgs e) =>
        UpdateRememberedSelection(_selectedPerks, e);

    private void OnNameTyped(object? s, RoutedEventArgs e) => SyncPluginName();

    /// <summary>The hub name and the declaration only matter when copying assets.</summary>
    private void OnHubModeChanged(object? s, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        Ctl<StackPanel>("OwnHubPanel").IsVisible = Ctl<ComboBox>("HubModeBox").SelectedIndex == 2;
    }

    private void OnStatsModeChanged(object? s, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        Ctl<StackPanel>("CustomStatsPanel").IsVisible =
            Ctl<ComboBox>("StatsModeBox").SelectedIndex == 1;
    }

    private void OnApplyStatPreset(object? s, RoutedEventArgs e)
    {
        var index = Math.Clamp(
            Ctl<ComboBox>("StatPresetBox").SelectedIndex,
            0,
            Enum.GetValues<FollowerStatPreset>().Length - 1);
        ApplyStatPreset((FollowerStatPreset)index);
        Ctl<ComboBox>("StatsModeBox").SelectedIndex = 1;
        Ctl<StackPanel>("CustomStatsPanel").IsVisible = true;
    }

    private void OnCustomRacesToggled(object? s, RoutedEventArgs e)
    {
        if (!_ready || _vanillaRaces.Count == 0) return;   // still loading
        Refilter("RaceSearch", "RaceList", _races);
    }

    /// <summary>Say immediately why a face cannot be used, rather than at build time.</summary>
    private void OnFaceSelected(object? s, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (Ctl<ListBox>("FaceList").SelectedItem is not FaceItem face) return;
        SetStatus(face.Blocker is { } why
            ? $"'{face.Export.Name}' cannot be used — {why}."
            : $"'{face.Export.Name}' is ready to build.");
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

    /// <summary>
    /// "+3,914 lines from RDO, OOD, +6 more" — what she says for free with this voice, before a
    /// single custom line is written. Silent when the scan has never been run.
    /// </summary>
    private string? CoverageLabel(string voiceFormKey)
    {
        if (_coverage is null || !_coverage.TryGetValue(voiceFormKey, out var c) || c.TotalLines == 0)
            return null;
        var top = c.Contributions
            .Where(x => !x.Plugin.Equals("Skyrim.esm", StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .Select(x => Path.GetFileNameWithoutExtension(x.Plugin))
            .ToList();
        var rest = c.Contributions.Count - top.Count;
        var who = top.Count == 0 ? "vanilla" : string.Join(", ", top) + (rest > 0 ? $", +{rest} more" : "");
        return $"+{c.TotalLines:N0} lines from {who}";
    }

    private static string Join(string a, string? b) => b is null ? a : $"{a}  ·  {b}";

    private static IReadOnlyList<RecordRef> RecordRefs(HashSet<string> remembered) =>
        remembered.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Select(k => new RecordRef(k))
            .ToList();

    private string Summary()
    {
        SyncPluginName();
        var place = (Ctl<ListBox>("PlaceList").SelectedItem as LocationItem)?.Location;
        var face = (Ctl<ListBox>("FaceList").SelectedItem as FaceItem)?.Export;
        return $"""
            Name      {Ctl<TextBox>("NameBox").Text}   ({Ctl<TextBox>("PluginBox").Text})
            Race      {Picked("RaceList")?.Display ?? "(default Nord)"}{(Ctl<CheckBox>("VampireBox").IsChecked == true ? "   (vampire)" : "")}
            Face      {face?.Name ?? "(plain default face)"}
            Voice     {Picked("VoiceList")?.Display ?? "(default)"}
            Lines     {LinesSummary()}
            Class     {Picked("ClassList")?.Display ?? "(default warrior)"}
            Combat    {Picked("CstyList")?.Display ?? "(race default)"}
            Level     {(Ctl<ComboBox>("LevelModeBox").SelectedIndex == 0 ? "scales with player" : $"fixed at {LevelValue("FixedLevelBox", 20)}")}
            Stats     {StatsSummary()}
            Equipment {_selectedArmor.Count} armor/accessory piece(s), {_selectedWeapons.Count} weapon(s)
            Carries   {(_selectedLore.Count == 0 ? "(nothing personal)" : $"{_selectedLore.Count} book(s)/belonging(s)")}
            Magic     {_selectedSpells.Count} spell(s), {_selectedPerks.Count} perk(s)
            Legacy    {Picked("OutfitList")?.Display ?? "(none — using actual equipment)"}
            Waits at  {place?.Display ?? "(Whiterun, outside)"}
            Routine   {RoutineSummary()}
            Starts    {(Ctl<CheckBox>("E2ABox").IsChecked == true ? $"as an ENEMY at {_alternateSpawns.Count} possible place(s) — beat her to recruit her" : _alternateSpawns.Count == 0 ? "always at the place above" : $"at random among {_alternateSpawns.Count} place(s): " + string.Join(", ", _alternateSpawns.Select(x => x.Location.Display)))}
            Regards   you as her {(RelationshipRank)Math.Max(0, Ctl<ComboBox>("RelationshipBox").SelectedIndex)}
            Knows     {(_kin.Count == 0 ? "(nobody else)" : string.Join(", ", _kin.Select(k => $"{k.DisplayName} ({k.Relationship.Rank})")))}
            Growth    {EvolutionSummary()}
            Form      {TransformSummary()}
            """;
    }

    private string EvolutionSummary()
    {
        var spec = BuildEvolution();
        return spec.IsUsable
            ? $"EXPERIMENTAL — {spec.Phases} phases, {spec.CombatsPerPhase} fights each, "
              + $"cowardly to {(Confidence)spec.EndConfidence} (adds a script)"
            : "(none — no script, her values never change)";
    }

    /// <summary>Mirrors the game's confidence ranks for display only.</summary>
    private enum Confidence { Cowardly, Cautious, Average, Brave, Foolhardy }

    private string RoutineSummary()
    {
        var idle = (IdleBehavior)Math.Max(0, Ctl<ComboBox>("IdleBox").SelectedIndex);
        var sleeps = Ctl<CheckBox>("SleepsBox").IsChecked == true;
        var what = idle switch
        {
            IdleBehavior.StaysPut => "keeps to her spot",
            IdleBehavior.WandersNearby => "uses the room she is in",
            IdleBehavior.SettlesWhereverSheIs => "settles wherever she is",
            _ => "game default",
        };
        return $"{what}, {(sleeps ? "sleeps at night" : "never sleeps")}";
    }

    /// <summary>
    /// States plainly whether the custom lines will be heard. "3 custom lines" reads as success
    /// even when every one of them is about to ship silent, so the silent case says so.
    /// </summary>
    private string LinesSummary()
    {
        if (_lines.Count == 0) return "(none — only her voice type's stock dialogue)";
        var voice = Picked("VoiceList")?.Display;
        var spoken = Ctl<CheckBox>("SynthesizeBox").IsChecked == true
                     && _voiceModels.CanMakeFuz && _voiceModels.CanSpeak(voice);
        return $"{_lines.Count} custom line(s), {(spoken ? "spoken with lip sync" : "SILENT subtitles")}";
    }

    private FollowerProfile BuildProfile()
    {
        SyncPluginName();
        var name = (Ctl<TextBox>("NameBox").Text ?? "Follower").Trim();
        var place = (Ctl<ListBox>("PlaceList").SelectedItem as LocationItem)?.Location;
        var face = (Ctl<ListBox>("FaceList").SelectedItem as FaceItem)?.Export;
        var csty = Picked("CstyList");
        var legacyOutfit = Picked("OutfitList");

        var mortality = Ctl<ComboBox>("MortalBox").SelectedIndex;   // 0 protected, 1 essential, 2 mortal
        // The game's own confidence scale, used directly: 0 cowardly … 4 foolhardy.
        var confidence = (byte)Math.Clamp(Ctl<ComboBox>("TemperBox").SelectedIndex, 0, 4);
        var scalesWithPlayer = Ctl<ComboBox>("LevelModeBox").SelectedIndex == 0;

        return new FollowerProfile
        {
            Name = name,
            PluginName = Ctl<TextBox>("PluginBox").Text!,
            Female = Ctl<ComboBox>("SexBox").SelectedIndex == 0,
            Race = new RecordRef(Picked("RaceList")?.FormKey ?? VanillaForms.NordRace.ToString()),
            VoiceType = new RecordRef(Picked("VoiceList")?.FormKey ?? VanillaForms.FemaleEvenTonedVoice.ToString()),
            Class = new RecordRef(Picked("ClassList")?.FormKey ?? VanillaForms.CombatWarrior1HClass.ToString()),
            Outfit = legacyOutfit is null ? null : new RecordRef(legacyOutfit.FormKey),
            CombatStyle = csty is null ? null : new CombatStyleChoice
            {
                Style = new RecordRef(csty.FormKey),
                CloneIntoPlugin = Ctl<CheckBox>("CloneCstyBox").IsChecked == true,
            },
            Placement = place is not null
                ? new PlacementSpec
                {
                    LocationId = place.Id,
                    AlternateLocationIds = _alternateSpawns.Select(x => x.Location.Id).ToList(),
                }
                : new PlacementSpec { Cell = new RecordRef(VanillaForms.WhiterunWorldPersistentCell.ToString()) },
            IsVampire = Ctl<CheckBox>("VampireBox").IsChecked == true,
            Appearance = new AppearanceSpec { CharGenExportName = face?.Name },
            Behavior = new BehaviorSpec
            {
                Idle = (IdleBehavior)Math.Max(0, Ctl<ComboBox>("IdleBox").SelectedIndex),
                SleepsAtNight = Ctl<CheckBox>("SleepsBox").IsChecked == true,
                Relationship = (RelationshipRank)Math.Max(0, Ctl<ComboBox>("RelationshipBox").SelectedIndex),
                OtherRelationships = _kin.Select(k => k.Relationship).ToList(),
            },
            Evolution = BuildEvolution(),
            Transformation = BuildTransformation(),
            EnemyToAlly = BuildEnemyToAlly(),
            Dialogue = new DialogueSpec
            {
                Lines = _lines.ToList(),
                Synthesize = Ctl<CheckBox>("SynthesizeBox").IsChecked == true,
            },
            Hub = Ctl<ComboBox>("HubModeBox").SelectedIndex switch
            {
                1 => HubMode.FreeHubs,
                2 => HubMode.OwnHub,
                _ => HubMode.ReferenceInstalled,
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
            Level = new LevelScaling
            {
                ScaleWithPlayer = scalesWithPlayer,
                MinLevel = (short)LevelValue("MinLevelBox", 10),
                MaxLevel = (short)LevelValue("MaxLevelBox", 0),
                FixedLevel = (short)LevelValue("FixedLevelBox", 20),
            },
            EquippedArmor = RecordRefs(_selectedArmor),
            InventoryItems = [.. RecordRefs(_selectedArmor), .. RecordRefs(_selectedWeapons),
                              .. RecordRefs(_selectedLore)],
            Spells = RecordRefs(_selectedSpells),
            Perks = RecordRefs(_selectedPerks),
            Ai = new AiValues
            {
                // Only the most reckless setting makes her pick fights; a follower otherwise
                // defends rather than starts them.
                Aggression = confidence == 4 ? (byte)1 : (byte)0,
                Confidence = confidence,
                Assistance = 2,
            },
            Stats = BuildStats(),
        };
    }

    private int LevelValue(string controlName, int fallback) =>
        Ctl<NumericUpDown>(controlName).Value is { } value
            ? Math.Clamp((int)value, 0, short.MaxValue)
            : fallback;

    private void BuildSkillEditor()
    {
        var groups = new[]
        {
            (Title: "Combat",
                Skills: new[]
                {
                    FollowerSkill.OneHanded, FollowerSkill.TwoHanded, FollowerSkill.Archery,
                    FollowerSkill.Block, FollowerSkill.Smithing, FollowerSkill.HeavyArmor,
                    FollowerSkill.LightArmor,
                }),
            (Title: "Magic",
                Skills: new[]
                {
                    FollowerSkill.Alteration, FollowerSkill.Conjuration, FollowerSkill.Destruction,
                    FollowerSkill.Illusion, FollowerSkill.Restoration, FollowerSkill.Enchanting,
                }),
            (Title: "Stealth & utility",
                Skills: new[]
                {
                    FollowerSkill.Sneak, FollowerSkill.Lockpicking, FollowerSkill.Pickpocket,
                    FollowerSkill.Alchemy, FollowerSkill.Speech,
                }),
        };

        var editor = Ctl<Grid>("SkillEditorGrid");
        for (var column = 0; column < groups.Length; column++)
        {
            var panel = new StackPanel { Spacing = 5 };
            Grid.SetColumn(panel, column);

            var heading = new TextBlock { Text = groups[column].Title, Margin = new Thickness(0, 0, 0, 3) };
            heading.Classes.Add("label");
            panel.Children.Add(heading);

            foreach (var skill in groups[column].Skills)
            {
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions($"*,{SkillValueEditorWidth}"),
                    ColumnSpacing = 8,
                };
                row.Children.Add(new TextBlock
                {
                    Text = SkillLabel(skill),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                });

                var box = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 15,
                    FormatString = "0",
                    MinWidth = SkillValueEditorWidth,
                };
                Grid.SetColumn(box, 1);
                row.Children.Add(box);
                panel.Children.Add(row);
                _skillBoxes.Add(skill, box);
            }

            editor.Children.Add(panel);
        }
    }

    private static string SkillLabel(FollowerSkill skill) => skill switch
    {
        FollowerSkill.OneHanded => "One-Handed",
        FollowerSkill.TwoHanded => "Two-Handed",
        FollowerSkill.HeavyArmor => "Heavy Armor",
        FollowerSkill.LightArmor => "Light Armor",
        _ => System.Text.RegularExpressions.Regex.Replace(skill.ToString(), "([a-z])([A-Z])", "$1 $2"),
    };

    private void ApplyStatPreset(FollowerStatPreset preset)
    {
        var stats = FollowerStats.FromPreset(preset);
        foreach (var skill in Enum.GetValues<FollowerSkill>())
            _skillBoxes[skill].Value = stats.GetSkill(skill);
        Ctl<NumericUpDown>("HealthStatBox").Value = stats.Health;
        Ctl<NumericUpDown>("MagickaStatBox").Value = stats.Magicka;
        Ctl<NumericUpDown>("StaminaStatBox").Value = stats.Stamina;
    }

    private FollowerStats BuildStats()
    {
        if (Ctl<ComboBox>("StatsModeBox").SelectedIndex != 1)
            return new FollowerStats();

        return new FollowerStats
        {
            Mode = FollowerStatsMode.Custom,
            Skills = Enum.GetValues<FollowerSkill>()
                .ToDictionary(skill => skill, skill => SkillValue(_skillBoxes[skill])),
            Health = PrimaryStatValue("HealthStatBox", 100),
            Magicka = PrimaryStatValue("MagickaStatBox", 100),
            Stamina = PrimaryStatValue("StaminaStatBox", 100),
        };
    }

    private string StatsSummary()
    {
        if (Ctl<ComboBox>("StatsModeBox").SelectedIndex != 1)
            return "automatic from class (recommended)";

        var strongest = Enum.GetValues<FollowerSkill>()
            .Select(skill => (Skill: skill, Value: SkillValue(_skillBoxes[skill])))
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Skill)
            .Take(3)
            .Select(pair => $"{SkillLabel(pair.Skill)} {pair.Value}");
        return $"custom: {string.Join(", ", strongest)}; " +
               $"H/M/S {PrimaryStatValue("HealthStatBox", 100)}/" +
               $"{PrimaryStatValue("MagickaStatBox", 100)}/" +
               $"{PrimaryStatValue("StaminaStatBox", 100)}";
    }

    private static byte SkillValue(NumericUpDown box) =>
        box.Value is { } value ? (byte)Math.Clamp((int)value, 0, 100) : (byte)15;

    private ushort PrimaryStatValue(string controlName, ushort fallback) =>
        Ctl<NumericUpDown>(controlName).Value is { } value
            ? (ushort)Math.Clamp((int)value, 0, ushort.MaxValue)
            : fallback;

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
            sb.AppendLine(result.Success
                ? "DONE — she is ready to install."
                : "BUILD STOPPED — Follower Forge found errors that would make the plugin unsafe.");
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

            var errors = result.Validation.Findings
                .Where(f => f.Severity == ValidationSeverity.Error)
                .ToList();
            var warnings = result.Validation.Findings
                .Where(f => f.Severity == ValidationSeverity.Warning)
                .ToList();
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Must be fixed:");
                foreach (var f in errors) sb.AppendLine($"  • {f.Message} [{f.Code}]");
            }
            if (warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings to review:");
                foreach (var f in warnings) sb.AppendLine($"  • {f.Message} [{f.Code}]");

                if (warnings.Any(f => f.Code == "FACEGEN_TEX_MISSING"))
                {
                    sb.AppendLine();
                    sb.AppendLine("  Those textures are baked into the face you exported. Either install the");
                    sb.AppendLine("  mod that provides them, or load the preset in RaceMenu and press F5 again");
                    sb.AppendLine("  with your current mods so the face points at files you actually have.");
                }
            }
            if (errors.Count == 0 && warnings.Count == 0)
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
