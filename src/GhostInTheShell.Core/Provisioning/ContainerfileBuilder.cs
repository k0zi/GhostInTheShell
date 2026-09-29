using System.Security.Cryptography;
using System.Text;
using GhostInTheShell.Core.Catalog;

namespace GhostInTheShell.Core.Provisioning;

/// <summary>
/// Turns an OS + agent selection into a Containerfile. Each agent gets its own layer so that
/// machines sharing a prefix of the selection reuse the build cache.
/// </summary>
public static class ContainerfileBuilder
{
    public const string UserName = "agent";
    public const string HomeDirectory = "/home/" + UserName;

    public static string Build(Catalog.Catalog catalog, string osId, IEnumerable<string> agentIds)
    {
        var os = catalog.GetOs(osId);
        // Catalog order, not click order, so the same selection always yields the same file (and image tag).
        var agents = catalog.Agents.Where(a => agentIds.Contains(a.Id)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"FROM {os.Image}");
        sb.AppendLine("SHELL [\"/bin/bash\", \"-c\"]");
        sb.AppendLine("USER root");
        sb.AppendLine(Run(os.Setup));
        foreach (var step in catalog.CommonSetup)
            sb.AppendLine(Run(step));

        sb.AppendLine(Run(
            $"useradd -m -s /bin/bash {UserName} " +
            $"&& echo '{UserName} ALL=(ALL) NOPASSWD:ALL' > /etc/sudoers.d/{UserName} " +
            $"&& chmod 0440 /etc/sudoers.d/{UserName}"));

        var path = string.Join(':', catalog.UserPath.Append("$PATH"));
        sb.AppendLine($"ENV PATH={path}");
        // Login shells reset PATH from /etc/profile, so persist it for interactive sessions too.
        sb.AppendLine(Run($"echo 'export PATH={string.Join(':', catalog.UserPath)}:$PATH' > /etc/profile.d/gits-agent-path.sh"));

        sb.AppendLine($"USER {UserName}");
        sb.AppendLine($"WORKDIR {HomeDirectory}");
        foreach (var agent in agents)
        {
            sb.AppendLine($"# agent: {agent.Id}");
            var install = agent.Check is null ? agent.Install : $"{agent.Install} && {agent.Check}";
            sb.AppendLine(Run(install));
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

    private static string Run(string command) => $"RUN {command}";
}
