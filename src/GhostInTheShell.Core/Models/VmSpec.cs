using System.Text.RegularExpressions;

namespace GhostInTheShell.Core.Models;

/// <summary>What the user asked for when creating a machine.</summary>
public sealed partial record VmSpec(
    string Name,
    int Cpus,
    int MemoryMb,
    int DiskGb,
    string OsId,
    IReadOnlyList<string> AgentIds)
{
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>Returns the first problem with the spec, or null when it is usable.</summary>
    public string? Validate()
    {
        if (!IsValidName(Name))
            return "A név 2–31 karakter: kisbetű, szám és kötőjel, betűvel vagy számmal kezdve.";
        if (Cpus < 1) return "Legalább 1 CPU mag kell.";
        if (MemoryMb < 256) return "Legalább 256 MB memória kell.";
        if (DiskGb < 1) return "Legalább 1 GB tárterület kell.";
        if (string.IsNullOrWhiteSpace(OsId)) return "Válassz operációs rendszert.";
        return null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,30}$")]
    private static partial Regex NamePattern();
}
