using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GhostInTheShell.Core;
using GhostInTheShell.Core.Localization;
using GhostInTheShell.Core.Models;
using GhostInTheShell.Core.Provisioning;

namespace GhostInTheShell.Podman;

/// <summary>Runs machines as rootless podman containers built from a generated Containerfile.</summary>
public sealed class PodmanProvider(Core.Catalog.Catalog catalog, PodmanCli? cli = null) : IVmProvider
{
    private readonly PodmanCli _cli = cli ?? new PodmanCli();

    public string DisplayName => "Podman";

    public event EventHandler? MachinesChanged;

    public async Task<ProviderHealth> CheckAsync(CancellationToken ct = default)
    {
        PodmanResult result;
        try
        {
            result = await _cli.TryRunAsync(["info", "--format", "json"], ct);
        }
        catch (PodmanException ex)
        {
            return ProviderHealth.Unavailable(Strings.Format("PodmanNotFoundFormat", ex.StdErr));
        }

        if (result.ExitCode != 0)
            return ProviderHealth.Unavailable(Strings.Format("PodmanNotWorkingFormat", result.StdErr));

        using var doc = JsonDocument.Parse(result.StdOut);
        var root = doc.RootElement;
        var version = root.GetProperty("version").GetProperty("Version").GetString();
        var warnings = new List<string>();
        if (!DiskQuotaEnforceable(root))
            warnings.Add(Strings.Get("DiskQuotaNotEnforceable"));

        return new ProviderHealth(true, version, warnings);
    }

    /// <summary>
    /// Podman's size storage option needs the overlay driver on XFS with project quotas, as root.
    /// Anything else rejects it, so we fall back to monitoring usage.
    /// </summary>
    internal static bool DiskQuotaEnforceable(JsonElement info)
    {
        var rootless = info.GetProperty("host").GetProperty("security").GetProperty("rootless").GetBoolean();
        var store = info.GetProperty("store");
        var driver = store.GetProperty("graphDriverName").GetString();
        var backing = store.TryGetProperty("graphStatus", out var gs) && gs.TryGetProperty("Backing Filesystem", out var bf)
            ? bf.GetString()
            : null;
        return !rootless && driver == "overlay" && string.Equals(backing, "xfs", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<VmInfo>> ListAsync(bool includeDiskUsage = false, CancellationToken ct = default)
    {
        List<string> args = ["ps", "-a", "--filter", $"label={PodmanLabels.ManagedFilter}", "--format", "json"];
        if (includeDiskUsage) args.Add("--size");
        var machines = PodmanParser.ParseContainers(await _cli.RunAsync(args, ct));

        if (includeDiskUsage && machines.Count > 0)
        {
            var volumeUsage = await GetVolumeUsageAsync(ct);
            machines = machines
                .Select(m => m with
                {
                    DiskUsedBytes = (m.DiskUsedBytes ?? 0)
                                    + volumeUsage.GetValueOrDefault(PodmanLabels.HomeVolumeName(m.Name)),
                })
                .ToList();
        }

        return machines.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Volume files belong to the container's subordinate uids, which the host user cannot read,
    /// so du runs inside podman's user namespace.
    /// </summary>
    private async Task<Dictionary<string, long>> GetVolumeUsageAsync(CancellationToken ct)
    {
        var volumes = PodmanParser.ParseVolumes(await _cli.RunAsync(
            ["volume", "ls", "--filter", $"label={PodmanLabels.ManagedFilter}", "--format", "json"], ct));
        if (volumes.Count == 0) return [];

        // du exits non-zero on any unreadable file but still prints the totals, so ignore the exit code.
        var du = await _cli.TryRunAsync(["unshare", "du", "-sb", .. volumes.Values], ct);
        var byPath = PodmanParser.ParseDu(du.StdOut);
        return volumes.ToDictionary(v => v.Key, v => byPath.GetValueOrDefault(v.Value));
    }

    public async Task CreateAsync(VmSpec spec, VmCredentials credentials, IProgress<string> log, CancellationToken ct = default)
    {
        if (spec.Validate() is { } error) throw new ArgumentException(error, nameof(spec));
        if (credentials.Validate() is { } credentialsError) throw new ArgumentException(credentialsError, nameof(credentials));
        // Checked up front: podman would otherwise fail only after a possibly long image build.
        if (spec.HostFolder is { } hostFolder && !Directory.Exists(hostFolder))
            throw new DirectoryNotFoundException(Strings.Format("HostFolderMissingFormat", hostFolder));

        var containerName = PodmanLabels.ContainerName(spec.Name);
        var volumeName = PodmanLabels.HomeVolumeName(spec.Name);
        if ((await _cli.TryRunAsync(["container", "exists", containerName], ct)).ExitCode == 0)
            throw new InvalidOperationException(Strings.Format("MachineExistsFormat", spec.Name));

        var image = await BuildImageAsync(spec, log, ct);

        var volumeCreated = false;
        try
        {
            log.Report(Strings.Format("LogCreatingVolumeFormat", volumeName));
            await _cli.RunAsync(["volume", "create", "--label", PodmanLabels.ManagedFilter, "--label", $"{PodmanLabels.Name}={spec.Name}", volumeName], ct);
            volumeCreated = true;

            HostIds? keepId = null;
            if (spec.HostFolder is not null)
            {
                log.Report(Strings.Format("LogSharingHostFolderFormat", spec.HostFolder, VmSpec.HostMountName));
                keepId = await GetKeepIdAsync(image, ct);
            }

            log.Report(Strings.Format("LogCreatingContainerFormat", containerName));
            var createArgs = BuildCreateArgs(spec, image, keepId);
            var withQuota = await _cli.TryRunAsync([.. createArgs.Take(1), "--storage-opt", $"size={spec.DiskGb}G", .. createArgs.Skip(1)], ct);
            if (withQuota.ExitCode != 0)
            {
                log.Report(Strings.Get("LogQuotaNotEnforced"));
                await _cli.RunAsync(createArgs, ct);
            }

            log.Report(Strings.Get("LogStarting"));
            await _cli.RunAsync(["start", containerName], ct);

            log.Report(Strings.Format("LogConfiguringUserFormat", spec.UserName));
            await ConfigureAccountsAsync(containerName, spec.UserName, credentials, ct);
            log.Report(Strings.Get("LogDone"));
        }
        catch
        {
            // Leave nothing half-made behind: a failed create must not block retrying with the same name.
            await _cli.TryRunAsync(["rm", "-f", "-t", "0", containerName], CancellationToken.None);
            if (volumeCreated) await _cli.TryRunAsync(["volume", "rm", "-f", volumeName], CancellationToken.None);
            throw;
        }
        finally
        {
            RaiseChanged();
        }
    }

    private async Task<string> BuildImageAsync(VmSpec spec, IProgress<string> log, CancellationToken ct)
    {
        var containerfile = ContainerfileBuilder.Build(catalog, spec.OsId, spec.AgentIds, spec.ToolchainIds, spec.UserName);
        var tag = ContainerfileBuilder.ImageTag(spec.OsId, containerfile);

        if ((await _cli.TryRunAsync(["image", "exists", tag], ct)).ExitCode == 0)
        {
            log.Report(Strings.Format("LogCachedImageFormat", tag));
            return tag;
        }

        var dir = Directory.CreateTempSubdirectory("gits-build-");
        try
        {
            var file = Path.Combine(dir.FullName, "Containerfile");
            await File.WriteAllTextAsync(file, containerfile, ct);
            log.Report(Strings.Format("LogBuildingImageFormat", tag));
            await _cli.StreamAsync(["build", "--layers", "--format", "docker", "-t", tag, "-f", file, dir.FullName], log.Report, ct);
            return tag;
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// The image user's uid/gid, so the host user can be mapped onto it (<c>--userns=keep-id</c>).
    /// Only rootless podman needs this: rootful already sees the host's real ids.
    /// </summary>
    private async Task<HostIds?> GetKeepIdAsync(string image, CancellationToken ct)
    {
        var rootless = (await _cli.RunAsync(["info", "--format", "{{.Host.Security.Rootless}}"], ct)).Trim();
        if (!bool.TryParse(rootless, out var isRootless) || !isRootless) return null;

        // The image's USER is the machine user, so plain id reports it.
        var ids = (await _cli.RunAsync(["run", "--rm", image, "sh", "-c", "id -u && id -g"], ct))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new HostIds(int.Parse(ids[0], CultureInfo.InvariantCulture), int.Parse(ids[1], CultureInfo.InvariantCulture));
    }

    /// <summary>Sets the sudo rule and passwords as root inside the started container.</summary>
    private async Task ConfigureAccountsAsync(string containerName, string userName, VmCredentials credentials, CancellationToken ct)
    {
        await _cli.RunAsync(["exec", "-u", "root", containerName, "sh", "-c", SudoersCommand(userName, credentials)], ct);
        if (ChpasswdInput(userName, credentials) is { } input)
            await _cli.RunAsync(["exec", "-i", "-u", "root", containerName, "chpasswd"], input, ct);
    }

    /// <summary>With a user password sudo asks for it; without one it stays passwordless, as before.</summary>
    internal static string SudoersCommand(string userName, VmCredentials credentials)
    {
        // userName is validated to [a-z0-9_-], so it is safe inside the shell command.
        var rule = credentials.HasUserPassword ? $"{userName} ALL=(ALL) ALL" : $"{userName} ALL=(ALL) NOPASSWD:ALL";
        return $"echo '{rule}' > /etc/sudoers.d/gits-user && chmod 0440 /etc/sudoers.d/gits-user";
    }

    /// <summary>chpasswd's stdin ("user:password" per line), or null when there is nothing to set.</summary>
    internal static string? ChpasswdInput(string userName, VmCredentials credentials)
    {
        var sb = new StringBuilder();
        if (credentials.HasUserPassword) sb.Append(userName).Append(':').Append(credentials.UserPassword).Append('\n');
        if (credentials.HasAdminPassword) sb.Append("root:").Append(credentials.AdminPassword).Append('\n');
        return sb.Length == 0 ? null : sb.ToString();
    }

    internal readonly record struct HostIds(int Uid, int Gid);

    /// <param name="keepId">The machine user's ids when a host folder is shared under rootless podman.</param>
    internal static List<string> BuildCreateArgs(VmSpec spec, string image, HostIds? keepId = null) =>
    [
        "create",
        "--name", PodmanLabels.ContainerName(spec.Name),
        "--hostname", spec.Name,
        "--init",
        "--cpus", spec.Cpus.ToString(CultureInfo.InvariantCulture),
        "--memory", $"{spec.MemoryMb.ToString(CultureInfo.InvariantCulture)}m",
        // Podman copies the image's home into an empty named volume, so installed agents survive.
        "-v", $"{PodmanLabels.HomeVolumeName(spec.Name)}:{ContainerfileBuilder.HomeDirectory(spec.UserName)}",
        .. HostFolderArgs(spec, keepId),
        "--label", PodmanLabels.ManagedFilter,
        "--label", $"{PodmanLabels.Name}={spec.Name}",
        "--label", $"{PodmanLabels.Os}={spec.OsId}",
        "--label", $"{PodmanLabels.Agents}={string.Join(',', spec.AgentIds)}",
        "--label", $"{PodmanLabels.Toolchains}={string.Join(',', spec.ToolchainIds)}",
        "--label", $"{PodmanLabels.User}={spec.UserName}",
        .. spec.HostFolder is null ? [] : new[] { "--label", $"{PodmanLabels.HostFolder}={spec.HostFolder}" },
        "--label", $"{PodmanLabels.Cpus}={spec.Cpus.ToString(CultureInfo.InvariantCulture)}",
        "--label", $"{PodmanLabels.MemoryMb}={spec.MemoryMb.ToString(CultureInfo.InvariantCulture)}",
        "--label", $"{PodmanLabels.DiskGb}={spec.DiskGb.ToString(CultureInfo.InvariantCulture)}",
        image,
    ];

    /// <summary>Nothing at all without a host folder, so no ~/host mount point is created either.</summary>
    private static IEnumerable<string> HostFolderArgs(VmSpec spec, HostIds? keepId)
    {
        if (spec.HostFolder is null) yield break;

        // Map the host user onto the machine user so both sides own what they create in the folder.
        if (keepId is { } ids)
        {
            yield return "--userns";
            yield return $"keep-id:uid={ids.Uid},gid={ids.Gid}";
        }

        // Relabelling (:z) would rewrite SELinux labels on the host folder; turn separation off for this container instead.
        yield return "--security-opt";
        yield return "label=disable";
        yield return "-v";
        yield return $"{spec.HostFolder}:{ContainerfileBuilder.HomeDirectory(spec.UserName)}/{VmSpec.HostMountName}";
    }

    public async Task StartAsync(string id, CancellationToken ct = default)
    {
        await _cli.RunAsync(["start", id], ct);
        RaiseChanged();
    }

    public async Task StopAsync(string id, CancellationToken ct = default)
    {
        await _cli.RunAsync(["stop", "-t", "10", id], ct);
        RaiseChanged();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        // Read the volume name from the label rather than deriving it from the id, which may not be ours.
        var name = (await _cli.RunAsync(["inspect", "--format", $"{{{{index .Config.Labels \"{PodmanLabels.Name}\"}}}}", id], ct)).Trim();
        await _cli.RunAsync(["rm", "-f", "-t", "0", id], ct);
        if (VmSpec.IsValidName(name))
            await _cli.TryRunAsync(["volume", "rm", "-f", PodmanLabels.HomeVolumeName(name)], ct);
        RaiseChanged();
    }

    public ProcessStartInfo GetShellCommand(VmInfo machine)
    {
        var user = machine.Spec.UserName;
        var psi = new ProcessStartInfo(_cli.Executable);
        foreach (var arg in new[] { "exec", "-it", "-u", user, "-w", ContainerfileBuilder.HomeDirectory(user), machine.Id, "bash", "-l" })
            psi.ArgumentList.Add(arg);
        return psi;
    }

    public void StartWatching(CancellationToken ct) => _ = Task.Run(() => WatchLoopAsync(ct), ct);

    /// <summary>
    /// Follows podman events so state changes made outside the app show up too. If the event
    /// stream dies it falls back to a periodic nudge and then reconnects.
    /// </summary>
    private async Task WatchLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _cli.StreamAsync(
                    ["events", "--filter", "type=container", "--filter", $"label={PodmanLabels.ManagedFilter}", "--format", "json"],
                    _ => RaiseChanged(),
                    ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (PodmanException)
            {
                // Fall through to the polling delay below.
            }

            RaiseChanged();
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void RaiseChanged() => MachinesChanged?.Invoke(this, EventArgs.Empty);
}
