using GhostInTheShell.Plugins;

namespace GhostInTheShell.Core.Components;

/// <summary>A component and where it came from.</summary>
/// <param name="Source">The plugin assembly or JSON file; null when the component was created in code.</param>
public sealed record LoadedComponent(IContainerComponent Component, string? Source = null);

/// <summary>A plugin or JSON file that could not be loaded; the rest of the components still are.</summary>
public sealed record ComponentLoadError(string Path, string Message);
