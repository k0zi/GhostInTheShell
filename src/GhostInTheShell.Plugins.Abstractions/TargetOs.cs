namespace GhostInTheShell.Plugins;

/// <summary>The distribution a machine is being built from, as far as a component needs to know.</summary>
/// <param name="Id">Catalog id, e.g. <c>ubuntu-24.04</c>.</param>
/// <param name="Family">Package family (<c>debian</c>, <c>fedora</c>, <c>arch</c>), or null when unknown.</param>
public sealed record TargetOs(string Id, string? Family);
