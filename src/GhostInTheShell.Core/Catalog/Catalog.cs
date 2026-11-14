using GhostInTheShell.Plugins;

namespace GhostInTheShell.Core.Catalog;

/// <summary>A Linux distribution a machine can be based on.</summary>
/// <param name="Setup">Shell command run as root to install the base packages.</param>
/// <param name="Family">Package family (<c>debian</c>, <c>fedora</c>, <c>arch</c>) that picks a component's install command.</param>
public sealed record OsDefinition(string Id, string DisplayName, string Image, string Setup, string? Family = null)
{
    /// <summary>What components see of this distribution.</summary>
    public TargetOs ToTarget() => new(Id, Family);
}

/// <summary>A coding agent in the old catalog format; still read from user catalogs and turned into a component.</summary>
/// <param name="Install">Shell command run as the machine user.</param>
/// <param name="Root">Prerequisites installed as root, keyed by distro family.</param>
public sealed record AgentDefinition(
    string Id,
    string DisplayName,
    string Description,
    string Install,
    string? Check,
    IReadOnlyDictionary<string, string>? Root = null);

/// <summary>A development environment in the old catalog format; still read from user catalogs and turned into a component.</summary>
public sealed record ToolchainDefinition(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyDictionary<string, string>? Root,
    string? User,
    string? Check);

/// <summary>
/// The base of every machine. Editable as JSON so install commands can be fixed without a rebuild.
/// Agents and development environments are components (see <see cref="Components.ComponentRegistry"/>).
/// </summary>
/// <param name="CommonSetup">Commands run as root on every machine.</param>
/// <param name="UserPath">Directories on every machine's PATH; components add their own.</param>
/// <param name="UserSetup">Commands run as the machine user on every machine (shell configuration).</param>
/// <param name="Agents">Old format only: agents defined in a user catalog.</param>
/// <param name="Toolchains">Old format only: development environments defined in a user catalog.</param>
public sealed record Catalog(
    IReadOnlyList<OsDefinition> OperatingSystems,
    IReadOnlyList<string> CommonSetup,
    IReadOnlyList<string> UserPath,
    IReadOnlyList<string>? UserSetup = null,
    IReadOnlyList<AgentDefinition>? Agents = null,
    IReadOnlyList<ToolchainDefinition>? Toolchains = null)
{
    // Older user catalogs have no such section.
    public IReadOnlyList<string> UserSetup { get; init; } = UserSetup ?? [];

    public IReadOnlyList<AgentDefinition> Agents { get; init; } = Agents ?? [];

    public IReadOnlyList<ToolchainDefinition> Toolchains { get; init; } = Toolchains ?? [];

    public OsDefinition GetOs(string id) =>
        OperatingSystems.FirstOrDefault(o => o.Id == id)
        ?? throw new KeyNotFoundException($"Unknown operating system: {id}");
}
