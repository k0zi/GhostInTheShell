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
using Microsoft.Extensions.DependencyInjection;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Toasts;

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
                .AddSingleton<ISukiDialogManager, SukiDialogManager>()
                .AddSingleton<ISukiToastManager, SukiToastManager>()
                .AddSingleton<DialogService>()
                .AddSingleton<MainWindowViewModel>()
                .BuildServiceProvider();

            var settings = services.GetRequiredService<SettingsService>();
            settings.Load();
            Strings.SetLanguage(settings.Current.Language);
            SukiTheme.GetInstance().ChangeBaseTheme(settings.Current.DarkTheme ? ThemeVariant.Dark : ThemeVariant.Light);

            var mainVm = services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
            desktop.ShutdownRequested += (_, _) => _lifetime.Cancel();
            _ = mainVm.InitializeAsync(_lifetime.Token);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
