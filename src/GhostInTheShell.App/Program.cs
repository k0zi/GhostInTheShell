using Avalonia;
using KD.Avalonia.Rice;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;

namespace GhostInTheShell.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseRice(o =>
            {
                o.AppInfo = new RiceAppInfo
                {
                    AppId = "ghostintheshell",
                    Name = "Ghost in the Shell",
                    IconUri = "avares://GhostInTheShell/Assets/ghostintheshell.png",
                };
                o.DefaultThemeId = BuiltInThemes.Fedora.Id;
            })
            .LogToTrace();
}
