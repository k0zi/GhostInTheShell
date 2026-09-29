namespace GhostInTheShell.Core.Catalog;

/// <summary>A Linux distribution a machine can be based on.</summary>
/// <param name="Setup">Shell command run as root to install the base packages.</param>
public sealed record OsDefinition(string Id, string DisplayName, string Image, string Setup);

/// <summary>A coding agent that can be installed into a machine.</summary>
/// <param name="Install">Shell command run as the <c>agent</c> user.</param>
/// <param name="Check">Command that proves the install worked; run at build time.</param>
public sealed record AgentDefinition(string Id, string DisplayName, string Description, string Install, string? Check);

/// <summary>Everything a machine can be built from. Editable as JSON so install commands can be fixed without a rebuild.</summary>
public sealed record Catalog(
    IReadOnlyList<OsDefinition> OperatingSystems,
    IReadOnlyList<string> CommonSetup,
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<string> UserPath)
{
    public OsDefinition GetOs(string id) =>
        OperatingSystems.FirstOrDefault(o => o.Id == id)
        ?? throw new KeyNotFoundException($"Ismeretlen operációs rendszer: {id}");

    public AgentDefinition GetAgent(string id) =>
        Agents.FirstOrDefault(a => a.Id == id)
        ?? throw new KeyNotFoundException($"Ismeretlen agent: {id}");
}
