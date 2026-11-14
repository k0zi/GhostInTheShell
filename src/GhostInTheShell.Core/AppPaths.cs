namespace GhostInTheShell.Core;

public static class AppPaths
{
    public static string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "ghostintheshell");

    /// <summary>JSON components the user added or patched.</summary>
    public static string ComponentsDirectory { get; } = Path.Combine(ConfigDirectory, "components");

    /// <summary>The plugins that ship with the app (every agent and development environment), next to the executable.</summary>
    public static string BundledPluginsDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "plugins");

    /// <summary>
    /// Where plugin assemblies are looked for, later ones overriding earlier ones: the bundled ones, system-wide ones
    /// (installed independently of the app, e.g. by a distro package; for an installed app this is the bundled
    /// folder itself), then the user's own.
    /// </summary>
    public static IReadOnlyList<string> PluginDirectories { get; } =
    [
        BundledPluginsDirectory,
        "/usr/lib/ghostintheshell/plugins",
        Path.Combine(ConfigDirectory, "plugins"),
    ];
}
