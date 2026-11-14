using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.Cpp;

[Export(typeof(IContainerComponent))]
public sealed class CppToolchain : ContainerComponentBase
{
    public override string Id => "cpp";

    public override string DisplayName => "C / C++";

    public override string Description => "gcc, g++, clang, gdb, CMake, Ninja";

    public override ComponentKind Kind => ComponentKind.Toolchain;

    public override int Order => 100;

    public override string? Check => "gcc --version && g++ --version && clang --version && cmake --version";

    protected override IReadOnlyDictionary<string, string> Root { get; } = new Dictionary<string, string>
    {
        ["debian"] = "export DEBIAN_FRONTEND=noninteractive && apt-get update && apt-get install -y --no-install-recommends gcc g++ clang gdb cmake ninja-build pkg-config && rm -rf /var/lib/apt/lists/*",
        ["fedora"] = "dnf install -y gcc gcc-c++ clang gdb cmake ninja-build pkgconf-pkg-config && dnf clean all",
        ["arch"] = "pacman -Syu --noconfirm --needed gcc clang gdb cmake ninja pkgconf && pacman -Scc --noconfirm",
    };
}
