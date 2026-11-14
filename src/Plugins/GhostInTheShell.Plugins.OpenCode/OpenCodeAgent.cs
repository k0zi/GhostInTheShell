using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.OpenCode;

[Export(typeof(IContainerComponent))]
public sealed class OpenCodeAgent : ContainerComponentBase
{
    public override string Id => "opencode";

    public override string DisplayName => "OpenCode";

    public override string Description => "sst OpenCode CLI";

    public override ComponentKind Kind => ComponentKind.Agent;

    public override int Order => 500;

    public override IReadOnlyList<string> UserPath { get; } = ["~/.opencode/bin"];

    public override string? Check => "opencode --version";

    protected override string User => "curl -fsSL https://opencode.ai/install | bash";
}
