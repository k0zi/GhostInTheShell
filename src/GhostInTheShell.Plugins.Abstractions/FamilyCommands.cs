namespace GhostInTheShell.Plugins;

/// <summary>Per-distro commands keyed by <see cref="TargetOs.Family"/>, with <c>*</c> matching any family.</summary>
public static class FamilyCommands
{
    public const string AnyFamily = "*";

    /// <summary>The command for the distro, or null when there is none (or no commands at all).</summary>
    public static string? Resolve(IReadOnlyDictionary<string, string>? commands, TargetOs os) => Resolve(commands, os.Family);

    /// <inheritdoc cref="Resolve(IReadOnlyDictionary{string, string}?, TargetOs)"/>
    public static string? Resolve(IReadOnlyDictionary<string, string>? commands, string? family)
    {
        if (commands is null) return null;
        if (family is not null && commands.TryGetValue(family, out var command)) return command;
        return commands.GetValueOrDefault(AnyFamily);
    }
}
