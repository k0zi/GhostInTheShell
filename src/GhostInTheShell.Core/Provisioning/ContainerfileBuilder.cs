using System.Security.Cryptography;
using System.Text;
using GhostInTheShell.Core.Components;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Core.Provisioning;

/// <summary>
/// Turns an OS + component selection into a Containerfile. Each component gets its
/// own layer so that machines sharing a prefix of the selection reuse the build cache.
/// </summary>
public static class ContainerfileBuilder
{
    /// <summary>The home directory the catalog's <c>userPath</c> was written against before <c>~</c> was supported.</summary>
    private const string LegacyHome = "/home/agent";

    public static string HomeDirectory(string userName) => $"/home/{userName}";

    /// <param name="components">Where the selected ids are looked up.</param>
    /// <param name="userName">The login user; must already be valid (<see cref="Models.VmSpec.IsValidUserName"/>).</param>
    public static string Build(Catalog.Catalog catalog, ComponentRegistry components, string osId, IEnumerable<string> agentIds,
        IEnumerable<string>? toolchainIds = null, string userName = Models.VmSpec.DefaultUserName)
    {
        if (!Models.VmSpec.IsValidUserName(userName))
            throw new ArgumentException($"Invalid user name: {userName}", nameof(userName));
        var home = HomeDirectory(userName);
        var os = catalog.GetOs(osId);
        var target = os.ToTarget();
        var selectedIds = agentIds.Concat(toolchainIds ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var id in selectedIds)
            components.Get(id);
        // Registry order, not click order, so the same selection always yields the same file (and image tag).
        var selected = components.All.Where(c => selectedIds.Contains(c.Id)).ToList();
        if (selected.FirstOrDefault(c => !c.SupportsOs(target)) is { } unsupported)
            throw new NotSupportedException($"{unsupported.DisplayName} has no install command for {os.DisplayName}.");

        var sb = new StringBuilder();
        sb.AppendLine($"FROM {os.Image}");
        sb.AppendLine("SHELL [\"/bin/bash\", \"-c\"]");
        sb.AppendLine("USER root");
        sb.AppendLine(Run(os.Setup));
        foreach (var step in catalog.CommonSetup)
            sb.AppendLine(Run(step));

        foreach (var component in selected)
        {
            if (component.GetRootCommand(target) is not { } root) continue;
            sb.AppendLine(component.Kind == ComponentKind.Agent
                ? $"# agent prerequisites: {component.Id}"
                : $"# toolchain: {component.Id}");
            sb.AppendLine(Run(root));
        }

        // The user comes after the shared root layers so machines with different user names still share them.
        // Sudo rules and passwords are set when the container is created, never baked into the image.
        sb.AppendLine(Run(
            $"if id -u {userName} >/dev/null 2>&1; then " +
            // Some images ship a user already (ubuntu:24.04 has "ubuntu"); reuse it only if it is a normal account.
            $"[ \"$(getent passwd {userName} | cut -d: -f6)\" = {home} ] || {{ echo '{userName} is a system account' >&2; exit 1; }}; " +
            $"usermod -s /bin/bash {userName}; " +
            $"else useradd -m -s /bin/bash {userName}; fi"));

        var userPath = catalog.UserPath.Concat(selected.SelectMany(c => c.UserPath))
            .Select(p => ExpandHome(p, home))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        sb.AppendLine($"ENV PATH={string.Join(':', userPath.Append("$PATH"))}");
        // Login shells reset PATH from /etc/profile, so persist it for interactive sessions too.
        sb.AppendLine(Run($"echo 'export PATH={string.Join(':', userPath)}:$PATH' > /etc/profile.d/gits-agent-path.sh"));

        sb.AppendLine($"USER {userName}");
        sb.AppendLine($"WORKDIR {home}");
        foreach (var step in catalog.UserSetup)
            sb.AppendLine(Run(step));
        foreach (var component in selected)
        {
            var command = string.Join(" && ", new[] { component.GetUserCommand(target), component.Check }.Where(c => c is not null));
            if (command.Length == 0) continue;
            sb.AppendLine(component.Kind == ComponentKind.Agent
                ? $"# agent: {component.Id}"
                : $"# toolchain user: {component.Id}");
            sb.AppendLine(Run(command));
        }

        sb.AppendLine("CMD [\"sleep\", \"infinity\"]");
        return sb.ToString();
    }

    /// <summary>Content-addressed tag: any catalog change produces a new image instead of a stale cached one.</summary>
    public static string ImageTag(string osId, string containerfile)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(containerfile)))[..12];
        return $"localhost/gits/{osId}:{hash}";
    }

    /// <summary>Resolves <c>~</c> (and the old fixed <c>/home/agent</c>) in a catalog path to the machine user's home.</summary>
    internal static string ExpandHome(string path, string home)
    {
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal)) return home + path[1..];
        if (path == LegacyHome || path.StartsWith(LegacyHome + "/", StringComparison.Ordinal)) return home + path[LegacyHome.Length..];
        return path;
    }

    private static string Run(string command) => $"RUN {command}";
}
