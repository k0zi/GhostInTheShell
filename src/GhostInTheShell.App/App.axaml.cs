using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using GhostInTheShell.App.Services;
using GhostInTheShell.App.ViewModels;
using GhostInTheShell.App.Views;
using GhostInTheShell.Core;
using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Localization;
using GhostInTheShell.Podman;
using KD.Avalonia.Rice;
using KD.Avalonia.Rice.Theming;
using Microsoft.Extensions.DependencyInjection;

namespace GhostInTheShell.App;

public partial class App : Application
{
    private readonly CancellationTokenSource _lifetime = new();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection()
                .AddSingleton(_ => CatalogLoader.Load())
                .AddSingleton<IVmProvider, PodmanProvider>(sp => new PodmanProvider(sp.GetRequiredService<Catalog>()))
                .AddSingleton<SettingsService>()
                .AddSingleton<DialogService>()
                .AddSingleton<MainWindowViewModel>()
                .BuildServiceProvider();

            var settings = services.GetRequiredService<SettingsService>();
            settings.Load();
            Strings.SetLanguage(settings.Current.Language);
            LocalizeRice();
            Strings.LanguageChanged += (_, _) => LocalizeRice();

            // The title bar's light / dark button is session-only in Rice; keep the choice across restarts.
            RequestedThemeVariant = settings.Current.DarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
            ThemeManager.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ThemeManager.IsDark))
                    settings.Save(settings.Current with { DarkTheme = ThemeManager.Instance.IsDark });
            };

            var mainVm = services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = mainWindow;
            desktop.ShutdownRequested += (_, _) => _lifetime.Cancel();
            _ = mainVm.InitializeAsync(_lifetime.Token);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // The title bar reads these when it is created, so a language change shows there after a restart.
    private static void LocalizeRice()
    {
        RiceStrings.ToggleTheme = Strings.Get("ToggleTheme");
        RiceStrings.Minimize = Strings.Get("Minimize");
        RiceStrings.Maximize = Strings.Get("Maximize");
        RiceStrings.Restore = Strings.Get("Restore");
        RiceStrings.Close = Strings.Get("Close");
        RiceStrings.About = Strings.Get("About");
        RiceStrings.Author = Strings.Get("Author");
        RiceStrings.Version = Strings.Get("Version");
        RiceStrings.Ok = Strings.Get("Ok");
        RiceStrings.ToastClose = Strings.Get("Close");
        RiceStrings.InfoBarClose = Strings.Get("Dismiss");
    }
}
