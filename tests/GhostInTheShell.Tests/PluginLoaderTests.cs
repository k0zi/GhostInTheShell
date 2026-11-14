using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Components;
using GhostInTheShell.Core.Provisioning;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Tests;

public sealed class PluginLoaderTests : IDisposable
{
    /// <summary>The sample plugin's build output, copied here by the test project.</summary>
    private static readonly string SamplePlugins = Path.Combine(AppContext.BaseDirectory, "sample-plugins");

    private readonly string _dir = Directory.CreateTempSubdirectory("gits-plugins-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Sample_plugin_is_loaded_from_its_folder()
    {
        var errors = new List<ComponentLoadError>();
        var loaded = PluginLoader.LoadDirectories([SamplePlugins, SamplePlugins], errors); // listed twice, loaded once

        Assert.Empty(errors);
        var go = Assert.Single(loaded);
        Assert.Equal("go", go.Component.Id);
        Assert.Equal(ComponentKind.Toolchain, go.Component.Kind);
        Assert.EndsWith("GhostInTheShell.SamplePlugin.dll", go.Source);
        // Same contract type as the app's, so the plugin's assembly did not bring its own copy.
        Assert.Same(typeof(IContainerComponent).Assembly, go.Component.GetType().GetInterfaces().Single().Assembly);
    }

    [Fact]
    public void Plugin_components_join_the_registry_and_the_containerfile()
    {
        var catalog = CatalogLoader.LoadDefault();
        var registry = ComponentRegistry.Load(catalog, [BundledComponents.Directory, SamplePlugins], componentsDirectory: _dir);

        Assert.Equal("go", registry.Toolchains[^1].Id); // order 600, after the bundled ones
        Assert.Equal(11, registry.All.Count);
        var file = ContainerfileBuilder.Build(catalog, registry, "ubuntu-24.04", [], ["go"], "dev");
        Assert.Contains("# toolchain: go", file);
        Assert.Contains("go.dev/dl/", file);
        Assert.Contains("/home/dev/go/bin", file);
    }

    [Fact]
    public void Broken_plugin_is_an_error_and_the_others_still_load()
    {
        var broken = Directory.CreateDirectory(Path.Combine(_dir, "Broken"));
        File.WriteAllText(Path.Combine(broken.FullName, "Broken.dll"), "not an assembly");

        var errors = new List<ComponentLoadError>();
        var loaded = PluginLoader.LoadDirectories([_dir, SamplePlugins], errors);

        Assert.EndsWith("Broken.dll", Assert.Single(errors).Path);
        Assert.Equal("go", Assert.Single(loaded).Component.Id);
    }

    [Fact]
    public void Only_dlls_named_after_their_folder_are_plugins()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Empty"));
        var other = Directory.CreateDirectory(Path.Combine(_dir, "Plugin"));
        File.WriteAllText(Path.Combine(other.FullName, "Dependency.dll"), "");

        Assert.Empty(PluginLoader.FindPlugins(_dir));
        Assert.Empty(PluginLoader.FindPlugins(Path.Combine(_dir, "missing")));
    }
}
