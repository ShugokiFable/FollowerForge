using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Serilog;

namespace FollowerForge.Ui;

public sealed class App : Application
{
    private static readonly ILogger Log = new LoggerConfiguration().MinimumLevel.Warning().CreateLogger();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var preferences = UiPreferencesStore.Load(warning: message => Log.Warning("{Warning}", message));
            ThemeResources.Apply(this, preferences.Theme);
            desktop.MainWindow = new WizardWindow(preferences);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
