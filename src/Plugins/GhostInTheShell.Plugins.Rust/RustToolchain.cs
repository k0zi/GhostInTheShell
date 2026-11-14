using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.Rust;

[Export(typeof(IContainerComponent))]
public sealed class RustToolchain : ContainerComponentBase
{
    public override string Id => "rust";

    public override string DisplayName => "Rust";

    public override string Description => "rustup, cargo, rustc (stable)";

    public override ComponentKind Kind => ComponentKind.Toolchain;

    public override int Order => 400;

    public override IReadOnlyList<string> UserPath { get; } = ["~/.cargo/bin"];

    public override string? Check => "cargo --version && rustc --version";

    protected override string User => "curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --profile default";
}
