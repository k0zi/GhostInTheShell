using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.Dotnet;

[Export(typeof(IContainerComponent))]
public sealed class DotnetToolchain : ContainerComponentBase
{
    public override string Id => "dotnet";

    public override string DisplayName => ".NET";

    public override string Description => ".NET 10 SDK (LTS)";

    public override ComponentKind Kind => ComponentKind.Toolchain;

    public override int Order => 200;

    public override string? Check => "dotnet --version";

    protected override IReadOnlyDictionary<string, string> Root { get; } = new Dictionary<string, string>
    {
        ["debian"] = """export DEBIAN_FRONTEND=noninteractive && apt-get update && apt-get install -y --no-install-recommends libicu-dev && rm -rf /var/lib/apt/lists/* && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/local/share/dotnet && rm /tmp/dotnet-install.sh && ln -sf /usr/local/share/dotnet/dotnet /usr/local/bin/dotnet && printf 'export DOTNET_ROOT=/usr/local/share/dotnet\nexport DOTNET_CLI_TELEMETRY_OPTOUT=1\nexport PATH="$PATH:$HOME/.dotnet/tools"\n' > /etc/profile.d/gits-dotnet.sh""",
        ["fedora"] = """dnf install -y libicu && dnf clean all && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/local/share/dotnet && rm /tmp/dotnet-install.sh && ln -sf /usr/local/share/dotnet/dotnet /usr/local/bin/dotnet && printf 'export DOTNET_ROOT=/usr/local/share/dotnet\nexport DOTNET_CLI_TELEMETRY_OPTOUT=1\nexport PATH="$PATH:$HOME/.dotnet/tools"\n' > /etc/profile.d/gits-dotnet.sh""",
        ["arch"] = """pacman -Syu --noconfirm --needed icu && pacman -Scc --noconfirm && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/local/share/dotnet && rm /tmp/dotnet-install.sh && ln -sf /usr/local/share/dotnet/dotnet /usr/local/bin/dotnet && printf 'export DOTNET_ROOT=/usr/local/share/dotnet\nexport DOTNET_CLI_TELEMETRY_OPTOUT=1\nexport PATH="$PATH:$HOME/.dotnet/tools"\n' > /etc/profile.d/gits-dotnet.sh""",
    };
}
