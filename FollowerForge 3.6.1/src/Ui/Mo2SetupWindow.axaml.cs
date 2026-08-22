using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FollowerForge.ModManagers;
using Serilog;

namespace FollowerForge.Ui;

public sealed record Mo2SetupResult(Mo2UserSelection? Selection, bool ReturnToAutomatic);

public partial class Mo2SetupWindow : Window
{
    private static readonly ILogger Log = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();
    private readonly Mo2SetupController _controller = new(Log);
    private bool _updatingProfiles;
    private Mo2SetupState? _state;

    public Mo2SetupWindow() : this(null) { }

    public Mo2SetupWindow(Mo2UserSelection? current)
    {
        AvaloniaXamlLoader.Load(this);
        if (current is null) return;

        Ctl<TextBox>("IniPathBox").Text = Path.Combine(current.InstanceRoot, "ModOrganizer.ini");
        InspectAndSelect(current.ProfileName);
    }

    private T Ctl<T>(string name) where T : Control => this.FindControl<T>(name)!;

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the MO2 ModOrganizer.ini",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Mod Organizer 2 settings")
                {
                    Patterns = ["ModOrganizer.ini"],
                },
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path)) return;
        Ctl<TextBox>("IniPathBox").Text = path;
        InspectAndSelect(null);
    }

    private void OnInspect(object? sender, RoutedEventArgs e) => InspectAndSelect(null);

    private void InspectAndSelect(string? preferredProfile)
    {
        var iniPath = Ctl<TextBox>("IniPathBox").Text ?? string.Empty;
        var inspected = _controller.Inspect(iniPath);
        _updatingProfiles = true;
        var profileBox = Ctl<ComboBox>("ProfileBox");
        profileBox.ItemsSource = inspected.Profiles;
        var preferred = inspected.Profiles.FirstOrDefault(name =>
                string.Equals(name, preferredProfile, StringComparison.OrdinalIgnoreCase))
            ?? inspected.Profiles.FirstOrDefault(name =>
                string.Equals(name, inspected.Inspection.SelectedProfile, StringComparison.OrdinalIgnoreCase))
            ?? inspected.Profiles.FirstOrDefault();
        profileBox.SelectedItem = preferred;
        _updatingProfiles = false;
        ValidateCurrent();
    }

    private void OnProfileChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingProfiles) ValidateCurrent();
    }

    private void ValidateCurrent()
    {
        var iniPath = Ctl<TextBox>("IniPathBox").Text ?? string.Empty;
        var profile = Ctl<ComboBox>("ProfileBox").SelectedItem as string;
        _state = _controller.Validate(iniPath, profile);
        Ctl<TextBlock>("ResolvedSummary").Text = _state.Summary;
        Ctl<TextBlock>("ValidationText").Text = FormatMessages(_state);
        Ctl<Button>("UseButton").IsEnabled = _state.IsValid;
    }

    private static string FormatMessages(Mo2SetupState state)
    {
        if (state.Errors.Count > 0)
            return "Cannot use this setup:" + Environment.NewLine
                + string.Join(Environment.NewLine, state.Errors.Select(error => "- " + error));
        if (state.Warnings.Count > 0)
            return "Ready, with warnings:" + Environment.NewLine
                + string.Join(Environment.NewLine, state.Warnings.Select(warning => "- " + warning));
        return state.Selection is null ? "Choose a profile." : "Ready to index this exact MO2 profile.";
    }

    private void OnUse(object? sender, RoutedEventArgs e)
    {
        ValidateCurrent();
        if (_state?.Selection is null) return;
        Close(new Mo2SetupResult(_state.Selection, ReturnToAutomatic: false));
    }

    private void OnAutomatic(object? sender, RoutedEventArgs e) =>
        Close(new Mo2SetupResult(null, ReturnToAutomatic: true));

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
