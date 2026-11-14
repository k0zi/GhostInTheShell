using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.ClaudeCode;

[Export(typeof(IContainerComponent))]
public sealed class ClaudeCodeAgent : ContainerComponentBase
{
    public override string Id => "claude";

    public override string DisplayName => "Claude Code";

    public override string Description => "Anthropic Claude Code CLI";

    public override ComponentKind Kind => ComponentKind.Agent;

    public override int Order => 100;

    public override string? Check => "claude --version";

    protected override string User => "curl -fsSL https://claude.ai/install.sh | bash";
}
