using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.Codex;

[Export(typeof(IContainerComponent))]
public sealed class CodexAgent : ContainerComponentBase
{
    public override string Id => "codex";

    public override string DisplayName => "Codex";

    public override string Description => "OpenAI Codex CLI";

    public override ComponentKind Kind => ComponentKind.Agent;

    public override int Order => 200;

    public override string? Check => "codex --version";

    protected override string User => """npm install -g --prefix "$HOME/.local" @openai/codex""";
}
