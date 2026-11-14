using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.Python;

[Export(typeof(IContainerComponent))]
public sealed class PythonToolchain : ContainerComponentBase
{
    public override string Id => "python";

    public override string DisplayName => "Python";

    public override string Description => "pip, venv, pipx, uv, dev headers";

    public override ComponentKind Kind => ComponentKind.Toolchain;

    public override int Order => 500;

    public override string? Check => "python3 --version && pip3 --version && uv --version";

    protected override IReadOnlyDictionary<string, string> Root { get; } = new Dictionary<string, string>
    {
        ["debian"] = "export DEBIAN_FRONTEND=noninteractive && apt-get update && apt-get install -y --no-install-recommends python3-pip python3-venv python3-dev pipx && rm -rf /var/lib/apt/lists/*",
        ["fedora"] = "dnf install -y python3-pip python3-devel pipx && dnf clean all",
        ["arch"] = "pacman -Syu --noconfirm --needed python-pip python-pipx && pacman -Scc --noconfirm",
    };

    protected override string User => "curl -LsSf https://astral.sh/uv/install.sh | env UV_NO_MODIFY_PATH=1 sh";
}
