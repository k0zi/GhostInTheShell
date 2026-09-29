namespace GhostInTheShell.Podman;

/// <summary>
/// Labels on containers and volumes are the only record of what the app manages — there is no
/// separate database to drift out of sync with podman.
/// </summary>
internal static class PodmanLabels
{
    public const string Managed = "gits.managed";
    public const string Name = "gits.name";
    public const string Os = "gits.os";
    public const string Agents = "gits.agents";
    public const string Toolchains = "gits.toolchains";
    public const string Cpus = "gits.cpus";
    public const string MemoryMb = "gits.memory-mb";
    public const string DiskGb = "gits.disk-gb";

    public const string ManagedFilter = Managed + "=true";
    public const string ContainerPrefix = "gits-";

    public static string ContainerName(string vmName) => ContainerPrefix + vmName;

    public static string HomeVolumeName(string vmName) => $"{ContainerPrefix}{vmName}-home";
}
