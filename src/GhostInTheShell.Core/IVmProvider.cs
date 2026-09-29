using System.Diagnostics;
using GhostInTheShell.Core.Models;

namespace GhostInTheShell.Core;

/// <summary>
/// A backend that runs the machines. The app only manages them; how they run is up to the provider.
/// </summary>
public interface IVmProvider
{
    string DisplayName { get; }

    /// <summary>Raised (on any thread) when a managed machine may have changed state.</summary>
    event EventHandler? MachinesChanged;

    Task<ProviderHealth> CheckAsync(CancellationToken ct = default);

    /// <param name="includeDiskUsage">Also measure disk usage; slower, so callers do it less often.</param>
    Task<IReadOnlyList<VmInfo>> ListAsync(bool includeDiskUsage = false, CancellationToken ct = default);

    /// <summary>Creates and starts a machine. Build output is reported line by line to <paramref name="log"/>.</summary>
    Task CreateAsync(VmSpec spec, IProgress<string> log, CancellationToken ct = default);

    Task StartAsync(string id, CancellationToken ct = default);

    Task StopAsync(string id, CancellationToken ct = default);

    /// <summary>Removes the machine and all of its data.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>The interactive shell command for a running machine; the caller wraps it in a terminal.</summary>
    ProcessStartInfo GetShellCommand(string id);

    /// <summary>Starts watching for state changes in the background. Stops when <paramref name="ct"/> is cancelled.</summary>
    void StartWatching(CancellationToken ct);
}
