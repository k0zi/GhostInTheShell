namespace GhostInTheShell.Core.Models;

public enum VmState
{
    Creating,
    Running,
    Stopped,
    Error,
}

/// <summary>A machine as the provider currently sees it.</summary>
public sealed record VmInfo(
    string Id,
    VmState State,
    VmSpec Spec,
    DateTimeOffset CreatedAt,
    string? StatusText = null,
    long? DiskUsedBytes = null)
{
    public string Name => Spec.Name;

    public bool IsOverDiskLimit => DiskUsedBytes is { } used && used > Spec.DiskGb * 1024L * 1024 * 1024;
}
