using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FollowerForge.Ui;

namespace FollowerForge.Tools;

/// <summary>
/// Renders the real WizardWindow headlessly (Skia, real fonts) and saves PNG frames so the
/// UI can be reviewed without a human clicking through the app. Not part of the solution or
/// the shipped package. Usage: UiScreenshots [outDir] [pumpSeconds]
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "shots");
        var pumpSeconds = args.Length > 1 ? double.Parse(args[1]) : 12;
        Directory.CreateDirectory(outDir);

        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        foreach (var theme in Enum.GetValues<UiTheme>())
        {
            try
            {
                RenderTheme(outDir, pumpSeconds, theme);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL {theme}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine("done");
        return 0;
    }

    private static void RenderTheme(string outDir, double pumpSeconds, UiTheme theme)
    {
        var prefs = UiPreferences.Default with { Theme = theme };
        var window = new WizardWindow(prefs);
        window.Show();
        Pump(pumpSeconds);

        Save(window, Path.Combine(outDir, $"{theme}-1-studio.png"));

        // Appearance category with representative picker rows so every chip class is visible.
        window.FindControl<ScrollViewer>("StudioPage")!.IsVisible = false;
        window.FindControl<ScrollViewer>("Page1")!.IsVisible = true;
        var raceList = window.FindControl<ListBox>("RaceList");
        if (raceList is not null)
        {
            raceList.ItemsSource = new List<PickerItem>
            {
                new("Nord", "00013743:Skyrim.esm", badge: "VANILLA", badgeKind: "good"),
                new("Breton", "00013744:Skyrim.esm", badge: "VANILLA", badgeKind: "good"),
                new("Dremora", "000131EF:Skyrim.esm", badge: "CAUTION", badgeKind: "warn"),
                new("Afflicted", "0A0F0C11:SomeMod.esp", badge: "BLOCKED", badgeKind: "bad"),
                new("Fox Race", "0B001234:FoxPeople.esp", badge: "PLAYABLE", badgeKind: "ok"),
                new("Legacy Row", "0C005678:Old.esp", badge: null, badgeKind: null),
            };
        }
        Pump(1.5);
        Save(window, Path.Combine(outDir, $"{theme}-2-appearance.png"));

        // Expert Deck overlay exactly as it layers over the main window.
        window.FindControl<Border>("DeckOverlay")!.IsVisible = true;
        Pump(1.5);
        Save(window, Path.Combine(outDir, $"{theme}-3-deck.png"));

        // Loadout category at the user's 2560×1440 — the reported "wasted space" repro:
        // pages must stretch to fill the star column and lists must use the tall window.
        window.FindControl<Border>("DeckOverlay")!.IsVisible = false;
        window.FindControl<ScrollViewer>("Page1")!.IsVisible = false;
        window.FindControl<ScrollViewer>("Page4")!.IsVisible = true;
        window.Width = 2560;
        window.Height = 1440;
        foreach (var listName in new[] { "ArmorTorsoList", "ArmorHeadList", "WeaponList", "AmmoList" })
        {
            var list = window.FindControl<ListBox>(listName);
            if (list is not null)
            {
                list.ItemsSource = LoadoutRows();
            }
        }

        Pump(1); // let the freshly shown page build its visual tree before searching it
        var weaponsExpander = window.GetVisualDescendants().OfType<Expander>()
            .FirstOrDefault(e => e.Header as string == "Weapons and ammunition");
        if (weaponsExpander is not null)
        {
            weaponsExpander.IsExpanded = true;
        }

        Pump(2);
        Save(window, Path.Combine(outDir, $"{theme}-4-loadout-1440p.png"));

        window.Close();
        Console.WriteLine($"rendered {theme}");
    }

    private static List<PickerItem> LoadoutRows() => new()
    {
        new("Sea Queen's Raider Bodysuit", "00000D69:SeaQueensRaider.esp", badge: "MOD", badgeKind: "ok"),
        new("0000_milfactory_skinnaked_athletic", "00000812:milfactory asset hub.esp", badge: "MOD", badgeKind: "ok"),
        new("0000_milfactory_skinnaked_chubby", "00000823:milfactory asset hub.esp", badge: "MOD", badgeKind: "ok"),
        new("Abyss Fang", "0003E830:Aquarium.esp", badge: "MOD", badgeKind: "ok"),
        new("Akaviri Arrow", "005C41BB:RigmorCyrodiil.esm", badge: "MOD", badgeKind: "ok"),
        new("Alessian Arrow", "06BC3B85:SussexHound.esm", badge: "MOD", badgeKind: "ok"),
        new("Abomination", "0000BF0D:Ultra Weapons.esp", badge: "MOD", badgeKind: "ok"),
        new("1_centaurweaponNOLOOT", "00000832:MoreToDoHarthstoneIsles.esp", badge: "MOD", badgeKind: "ok"),
        new("0_AmmoCannon", "0000DD48:TheAbyssalSea.esp", badge: "MOD", badgeKind: "ok"),
        new("0_SUMMONBullet", "0001692A:TheAbyssalSea.esp", badge: "MOD", badgeKind: "ok"),
        new("Dwarven Bolt", "00013743:Dawnguard.esm", badge: "VANILLA", badgeKind: "good"),
        new("Iron Sword", "00012EB7:Skyrim.esm", badge: "VANILLA", badgeKind: "good"),
    };

    private static void Pump(double seconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
            Thread.Sleep(40);
        }
    }

    private static void Save(Window window, string path)
    {
        var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            Console.WriteLine($"no frame for {path}");
            return;
        }

        using var stream = File.Create(path);
        frame.Save(stream);
    }
}
