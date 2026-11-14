using GhostInTheShell.Core.Components.Json;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Core.Components;

/// <summary>
/// Every agent and development environment that can be installed: plugin assemblies (the bundled ones first), then
/// JSON components. A later component with the same id replaces an earlier one, so install commands can be patched.
/// </summary>
public sealed class ComponentRegistry
{
    private readonly Dictionary<string, LoadedComponent> _byId;

    public ComponentRegistry(IEnumerable<LoadedComponent> components, IReadOnlyList<ComponentLoadError>? errors = null)
    {
        _byId = new Dictionary<string, LoadedComponent>(StringComparer.Ordinal);
        foreach (var loaded in components)
            _byId[loaded.Component.Id] = loaded;

        Loaded = _byId.Values
            .OrderBy(l => l.Component.Kind)
            .ThenBy(l => l.Component.Order)
            .ThenBy(l => l.Component.Id, StringComparer.Ordinal)
            .ToList();
        All = Loaded.Select(l => l.Component).ToList();
        Toolchains = All.Where(c => c.Kind == ComponentKind.Toolchain).ToList();
        Agents = All.Where(c => c.Kind == ComponentKind.Agent).ToList();
        Errors = errors ?? [];
    }

    public ComponentRegistry(IEnumerable<IContainerComponent> components)
        : this(components.Select(c => new LoadedComponent(c)))
    {
    }

    /// <summary>Every component in Containerfile order: development environments, then agents, each by <see cref="IContainerComponent.Order"/>.</summary>
    public IReadOnlyList<IContainerComponent> All { get; }

    public IReadOnlyList<LoadedComponent> Loaded { get; }

    public IReadOnlyList<IContainerComponent> Toolchains { get; }

    public IReadOnlyList<IContainerComponent> Agents { get; }

    /// <summary>Plugins and JSON files that were skipped.</summary>
    public IReadOnlyList<ComponentLoadError> Errors { get; }

    public IContainerComponent? Find(string id) => _byId.GetValueOrDefault(id)?.Component;

    public IContainerComponent Get(string id) =>
        Find(id) ?? throw new KeyNotFoundException($"Unknown component: {id}");

    /// <summary>
    /// Loads everything: plugin assemblies (<see cref="AppPaths.PluginDirectories"/> unless given), components still
    /// defined in an old-format user catalog, and the user's JSON components, in that order of precedence.
    /// </summary>
    public static ComponentRegistry Load(Catalog.Catalog catalog, IEnumerable<string>? pluginDirectories = null,
        string? componentsDirectory = null)
    {
        var errors = new List<ComponentLoadError>();
        var components = new List<LoadedComponent>(PluginLoader.LoadDirectories(pluginDirectories ?? AppPaths.PluginDirectories, errors));
        components.AddRange(JsonComponentSource.FromLegacyCatalog(catalog).Select(c => new LoadedComponent(c, "catalog.json")));
        components.AddRange(JsonComponentSource.LoadDirectory(componentsDirectory ?? AppPaths.ComponentsDirectory, errors));
        return new ComponentRegistry(components, errors);
    }
}
