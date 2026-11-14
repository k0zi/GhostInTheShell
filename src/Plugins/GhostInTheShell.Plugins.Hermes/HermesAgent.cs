using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.Hermes;

[Export(typeof(IContainerComponent))]
public sealed class HermesAgent : ContainerComponentBase
{
    public override string Id => "hermes";

    public override string DisplayName => "Hermes Agent";

    public override string Description => "Nous Research Hermes Agent";

    public override ComponentKind Kind => ComponentKind.Agent;

    public override int Order => 400;

    public override IReadOnlyList<string> UserPath { get; } = ["~/.hermes/bin"];

    public override string? Check => "hermes --version";

    protected override IReadOnlyDictionary<string, string> Root { get; } = new Dictionary<string, string>
    {
        ["debian"] = "export DEBIAN_FRONTEND=noninteractive && apt-get update && apt-get install -y --no-install-recommends libatomic1 && rm -rf /var/lib/apt/lists/*",
        ["fedora"] = "dnf install -y libatomic && dnf clean all",
        ["arch"] = "pacman -Syu --noconfirm --needed gcc-libs && pacman -Scc --noconfirm",
    };

    protected override string User => "export npm_config_fetch_retries=5 npm_config_fetch_retry_mintimeout=20000 npm_config_fetch_retry_maxtimeout=120000 && curl -fsSL https://raw.githubusercontent.com/NousResearch/hermes-agent/main/scripts/install.sh | bash -s -- --non-interactive --skip-browser --skip-computer-use";
}
