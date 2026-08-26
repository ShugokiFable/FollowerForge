using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FollowerForge.Domain;
using FollowerForge.ModManagers;

namespace FollowerForge.Ui;

public sealed record PathsSetupResult(AppUserSelection? Selection, bool ReturnToAutomatic);

public partial class PathsSetupWindow : Window
{
    private readonly EnvironmentSnapshot? _env;
    private PathsSetupState? _state;

    public PathsSetupWindow() : this(null, null) { }

    public PathsSetupWindow(AppUserSelection? current, EnvironmentSnapshot? env)
    {
        _env = env;
        AvaloniaXamlLoader.Load(this);
        if (current is not null)
        {
            Ctl<TextBox>("XvaPathBox").Text = current.XvaSynthRoot ?? "";
            Ctl<TextBox>("OutputPathBox").Text = current.WorkspaceRoot ?? "";
        }
        Ctl<TextBox>("XvaPathBox").TextChanged += (_, _) => ValidateCurrent();
        Ctl<TextBox>("OutputPathBox").TextChanged += (_, _) => ValidateCurrent();
        ValidateCurrent();
    }

    private T Ctl<T>(string name) where T : Control => this.FindControl<T>(name)!;

    private async void OnBrowseXva(object? sender, RoutedEventArgs e)
    {
        var folder = await PickFolder("Choose the xVASynth install folder");
        if (folder is null) return;
        Ctl<TextBox>("XvaPathBox").Text = folder;
        ValidateCurrent();
    }

    private async void OnBrowseOutput(object? sender, RoutedEventArgs e)
    {
        var folder = await PickFolder("Choose where built followers are saved");
        if (folder is null) return;
        Ctl<TextBox>("OutputPathBox").Text = folder;
        ValidateCurrent();
    }

    private async Task<string?> PickFolder(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private void ValidateCurrent()
    {
        _state = PathsSetupController.Validate(
            Ctl<TextBox>("XvaPathBox").Text,
            Ctl<TextBox>("OutputPathBox").Text,
            _env);
        Ctl<TextBlock>("ResolvedSummary").Text = _state.Summary;
        Ctl<TextBlock>("ValidationText").Text = FormatMessages(_state);
        Ctl<Button>("SaveButton").IsEnabled = _state.IsValid;
    }

    private static string FormatMessages(PathsSetupState state)
    {
        if (state.Errors.Count > 0)
            return "Cannot save these paths:" + Environment.NewLine
                   + string.Join(Environment.NewLine, state.Errors.Select(error => "- " + error));
        if (state.Warnings.Count > 0)
            return "Ready, with warnings:" + Environment.NewLine
                   + string.Join(Environment.NewLine, state.Warnings.Select(warning => "- " + warning));
        return "Ready. Empty boxes keep automatic detection.";
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        ValidateCurrent();
        if (_state is not { IsValid: true }) return;
        var xva = string.IsNullOrWhiteSpace(_state.XvaSynthRoot) ? null : _state.XvaSynthRoot;
        var output = string.IsNullOrWhiteSpace(_state.WorkspaceRoot) ? null : _state.WorkspaceRoot;
        Close(new PathsSetupResult(new AppUserSelection(xva, output), ReturnToAutomatic: false));
    }

    private void OnAutomatic(object? sender, RoutedEventArgs e) =>
        Close(new PathsSetupResult(null, ReturnToAutomatic: true));

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
