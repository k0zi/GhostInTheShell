using System.Globalization;
using System.Text.Json;
using GhostInTheShell.Core.Models;

namespace GhostInTheShell.Podman;

/// <summary>Parses podman's JSON output. Kept separate from the CLI calls so it can be tested on captured output.</summary>
internal static class PodmanParser
{
    public static List<VmInfo> ParseContainers(string json)
    {
        var result = new List<VmInfo>();
        if (string.IsNullOrWhiteSpace(json)) return result;

        using var doc = JsonDocument.Parse(json);
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            var labels = c.TryGetProperty("Labels", out var l) && l.ValueKind == JsonValueKind.Object
                ? l.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "")
                : [];
            if (!labels.TryGetValue(PodmanLabels.Name, out var name)) continue;

            var spec = new VmSpec(
                name,
                Int(labels, PodmanLabels.Cpus),
                Int(labels, PodmanLabels.MemoryMb),
                Int(labels, PodmanLabels.DiskGb),
                labels.GetValueOrDefault(PodmanLabels.Os, ""),
                List(labels, PodmanLabels.Agents),
                // Machines made before toolchains existed have no such label.
                List(labels, PodmanLabels.Toolchains),
                // Machines made before the user was configurable all use the default one.
                labels.GetValueOrDefault(PodmanLabels.User) is { Length: > 0 } user ? user : VmSpec.DefaultUserName,
                labels.GetValueOrDefault(PodmanLabels.HostFolder) is { Length: > 0 } folder ? folder : null);

            var containerName = c.GetProperty("Names")[0].GetString()!;
            var state = MapState(c.TryGetProperty("State", out var s) ? s.GetString() : null);
            var created = c.TryGetProperty("Created", out var cr) && cr.TryGetInt64(out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix)
                : DateTimeOffset.MinValue;
            long? rw = c.TryGetProperty("Size", out var size) && size.ValueKind == JsonValueKind.Object
                       && size.TryGetProperty("rwSize", out var rwEl) && rwEl.TryGetInt64(out var rwv)
                ? rwv
                : null;
            var status = c.TryGetProperty("Status", out var st) ? st.GetString() : null;

            result.Add(new VmInfo(containerName, state, spec, created, status, rw));
        }

        return result;
    }

    public static VmState MapState(string? podmanState) => podmanState?.ToLowerInvariant() switch
    {
        "running" => VmState.Running,
        "created" or "configured" or "exited" or "stopped" or "paused" or "initialized" => VmState.Stopped,
        "stopping" or "removing" => VmState.Stopped,
        _ => VmState.Error,
    };

    /// <summary>Volume name → mountpoint.</summary>
    public static Dictionary<string, string> ParseVolumes(string json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        using var doc = JsonDocument.Parse(json);
        foreach (var v in doc.RootElement.EnumerateArray())
            result[v.GetProperty("Name").GetString()!] = v.GetProperty("Mountpoint").GetString()!;
        return result;
    }

    /// <summary>Parses <c>du -sb</c> output ("bytes\tpath" per line) into path → bytes.</summary>
    public static Dictionary<string, long> ParseDu(string output)
    {
        var result = new Dictionary<string, long>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2 && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
                result[parts[1].Trim()] = bytes;
        }

        return result;
    }

    private static string[] List(Dictionary<string, string> labels, string key) =>
        labels.GetValueOrDefault(key, "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int Int(Dictionary<string, string> labels, string key) =>
        labels.TryGetValue(key, out var v) && int.TryParse(v, CultureInfo.InvariantCulture, out var i) ? i : 0;
}
