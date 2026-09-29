using System.Text.RegularExpressions;
using GhostInTheShell.Core.Localization;

namespace GhostInTheShell.Core.Models;

/// <summary>What the user asked for when creating a machine.</summary>
public sealed partial record VmSpec(
    string Name,
    int Cpus,
    int MemoryMb,
    int DiskGb,
    string OsId,
    IReadOnlyList<string> AgentIds,
    IReadOnlyList<string> ToolchainIds)
{
    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>Returns the first problem with the spec, or null when it is usable.</summary>
    public string? Validate()
    {
        if (!IsValidName(Name))
            return Strings.Get("NameInvalid");
        if (Cpus < 1) return Strings.Get("CpuMin");
        if (MemoryMb < 256) return Strings.Get("MemoryMin");
        if (DiskGb < 1) return Strings.Get("DiskMin");
        if (string.IsNullOrWhiteSpace(OsId)) return Strings.Get("OsRequired");
        return null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,30}$")]
    private static partial Regex NamePattern();
}
