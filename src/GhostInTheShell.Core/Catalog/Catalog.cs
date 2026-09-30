namespace GhostInTheShell.Core.Catalog;

/// <summary>A Linux distribution a machine can be based on.</summary>
/// <param name="Setup">Shell command run as root to install the base packages.</param>
/// <param name="Family">Package family (<c>debian</c>, <c>fedora</c>, <c>arch</c>) that picks a toolchain's install command.</param>
public sealed record OsDefinition(string Id, string DisplayName, string Image, string Setup, string? Family = null);

/// <summary>A coding agent that can be installed into a machine.</summary>
/// <param name="Install">Shell command run as the <c>agent</c> user.</param>
/// <param name="Check">Command that proves the install worked; run at build time.</param>
/// <param name="Root">Prerequisites installed as root only when this agent is selected, keyed like <see cref="ToolchainDefinition.Root"/>.</param>
public sealed record AgentDefinition(
    string Id,
    string DisplayName,
    string Description,
    string Install,
    string? Check,
    IReadOnlyDictionary<string, string>? Root = null)
{
    public bool SupportsOs(OsDefinition os) => Root is null || FamilyCommands.Resolve(Root, os) is not null;

    public string? GetRootCommand(OsDefinition os) => FamilyCommands.Resolve(Root, os);
}

/// <summary>A language development environment (compiler, SDK, package tools).</summary>
/// <param name="Root">Commands run as root, keyed by <see cref="OsDefinition.Family"/>; <c>*</c> matches any family.</param>
/// <param name="User">Command run as the <c>agent</c> user after the root part.</param>
/// <param name="Check">Command that proves the install worked; run at build time as the <c>agent</c> user.</param>
public sealed record ToolchainDefinition(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyDictionary<string, string>? Root,
    string? User,
    string? Check)
{
    public bool SupportsOs(OsDefinition os) => Root is null || FamilyCommands.Resolve(Root, os) is not null;

    public string? GetRootCommand(OsDefinition os) => FamilyCommands.Resolve(Root, os);
}

/// <summary>Per-distro commands keyed by <see cref="OsDefinition.Family"/>, with <c>*</c> matching any family.</summary>
public static class FamilyCommands
{
    public const string AnyFamily = "*";

    /// <summary>The command for the distro, or null when there is none (or no commands at all).</summary>
    public static string? Resolve(IReadOnlyDictionary<string, string>? commands, OsDefinition os)
    {
        if (commands is null) return null;
        if (os.Family is not null && commands.TryGetValue(os.Family, out var command)) return command;
        return commands.GetValueOrDefault(AnyFamily);
    }
}

/// <summary>Everything a machine can be built from. Editable as JSON so install commands can be fixed without a rebuild.</summary>
/// <param name="CommonSetup">Commands run as root on every machine.</param>
/// <param name="UserSetup">Commands run as the machine user on every machine (shell configuration).</param>
public sealed record Catalog(
    IReadOnlyList<OsDefinition> OperatingSystems,
    IReadOnlyList<string> CommonSetup,
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<string> UserPath,
    IReadOnlyList<ToolchainDefinition>? Toolchains = null,
    IReadOnlyList<string>? UserSetup = null)
{
    // Older user catalogs have neither section.
    public IReadOnlyList<ToolchainDefinition> Toolchains { get; init; } = Toolchains ?? [];

    public IReadOnlyList<string> UserSetup { get; init; } = UserSetup ?? [];

    public OsDefinition GetOs(string id) =>
        OperatingSystems.FirstOrDefault(o => o.Id == id)
        ?? throw new KeyNotFoundException($"Unknown operating system: {id}");

    public AgentDefinition GetAgent(string id) =>
        Agents.FirstOrDefault(a => a.Id == id)
        ?? throw new KeyNotFoundException($"Unknown agent: {id}");

    public ToolchainDefinition GetToolchain(string id) =>
        Toolchains.FirstOrDefault(t => t.Id == id)
        ?? throw new KeyNotFoundException($"Unknown development environment: {id}");
}
