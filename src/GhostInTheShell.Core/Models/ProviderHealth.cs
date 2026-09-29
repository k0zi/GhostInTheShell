namespace GhostInTheShell.Core.Models;

/// <param name="IsAvailable">False when the backend cannot be used at all (e.g. not installed).</param>
/// <param name="Version">Backend version string, when available.</param>
/// <param name="Warnings">Non-fatal limitations the user should know about.</param>
public sealed record ProviderHealth(bool IsAvailable, string? Version, IReadOnlyList<string> Warnings)
{
    public static ProviderHealth Unavailable(string reason) => new(false, null, [reason]);
}
