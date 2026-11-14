using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Components;

namespace GhostInTheShell.Tests;

/// <summary>The app's bundled plugins, copied to the test output by BundledPlugins.targets, loaded the way the app loads them.</summary>
internal static class BundledComponents
{
    public static string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "plugins");

    public static ComponentRegistry Registry { get; } = Load();

    /// <param name="componentsDirectory">JSON components; none by default.</param>
    public static ComponentRegistry Load(Catalog? catalog = null, string? componentsDirectory = null) =>
        ComponentRegistry.Load(catalog ?? CatalogLoader.LoadDefault(), [Directory],
            componentsDirectory ?? Path.Combine(AppContext.BaseDirectory, "no-json-components"));
}
