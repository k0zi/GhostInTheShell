using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.SamplePlugin;

/// <summary>Go from the official tarball, the same on every distro.</summary>
[Export(typeof(IContainerComponent))]
public sealed class GoToolchain : ContainerComponentBase
{
    public override string Id => "go";

    public override string DisplayName => "Go";

    public override string Description => "Go toolchain (latest stable)";

    public override ComponentKind Kind => ComponentKind.Toolchain;

    public override int Order => 600;

    public override IReadOnlyList<string> UserPath { get; } = ["~/go/bin"];

    public override string? Check => "go version";

    protected override IReadOnlyDictionary<string, string> Root { get; } = new Dictionary<string, string>
    {
        [FamilyCommands.AnyFamily] = """arch=$(uname -m); case "$arch" in x86_64) arch=amd64 ;; aarch64) arch=arm64 ;; esac; version=$(curl -fsSL 'https://go.dev/VERSION?m=text' | head -n1); curl -fsSL "https://go.dev/dl/$version.linux-$arch.tar.gz" | tar -xz -C /usr/local && ln -sf /usr/local/go/bin/* /usr/local/bin/""",
    };
}
