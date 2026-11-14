using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.Pi;

[Export(typeof(IContainerComponent))]
public sealed class PiAgent : ContainerComponentBase
{
    public override string Id => "pi";

    public override string DisplayName => "pi";

    public override string Description => "pi.dev coding agent";

    public override ComponentKind Kind => ComponentKind.Agent;

    public override int Order => 300;

    public override string? Check => "pi --version";

    protected override string User => """npm install -g --prefix "$HOME/.local" @earendil-works/pi-coding-agent""";
}
