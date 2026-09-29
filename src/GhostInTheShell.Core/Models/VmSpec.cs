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
    IReadOnlyList<string> ToolchainIds,
    string UserName = VmSpec.DefaultUserName,
    string? HostFolder = null)
{
    public const string DefaultUserName = "agent";

    /// <summary>Where <see cref="HostFolder"/> appears, relative to the user's home.</summary>
    public const string HostMountName = "host";

    public static bool IsValidName(string? name) => name is not null && NamePattern().IsMatch(name);

    /// <summary>A portable Linux login name; <c>root</c> is taken.</summary>
    public static bool IsValidUserName(string? name) => name is not null && name != "root" && UserNamePattern().IsMatch(name);

    /// <summary>
    /// An absolute host path usable in <c>podman -v</c>: a colon would split the mount spec.
    /// Whether it exists is the provider's check, since only it sees the host.
    /// </summary>
    public static bool IsValidHostFolder(string? path) =>
        path is not null && Path.IsPathFullyQualified(path) && path.AsSpan().IndexOfAny(':', '\n', '\r') < 0;

    /// <summary>Returns the first problem with the spec, or null when it is usable.</summary>
    public string? Validate()
    {
        if (!IsValidName(Name))
            return Strings.Get("NameInvalid");
        if (Cpus < 1) return Strings.Get("CpuMin");
        if (MemoryMb < 256) return Strings.Get("MemoryMin");
        if (DiskGb < 1) return Strings.Get("DiskMin");
        if (string.IsNullOrWhiteSpace(OsId)) return Strings.Get("OsRequired");
        if (!IsValidUserName(UserName)) return Strings.Get("UserNameInvalid");
        if (HostFolder is not null && !IsValidHostFolder(HostFolder)) return Strings.Get("HostFolderInvalid");
        return null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,30}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[a-z_][a-z0-9_-]{0,31}$")]
    private static partial Regex UserNamePattern();
}
