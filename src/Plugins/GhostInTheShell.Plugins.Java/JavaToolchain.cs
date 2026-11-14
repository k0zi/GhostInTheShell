using System.Composition;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Plugins.Java;

[Export(typeof(IContainerComponent))]
public sealed class JavaToolchain : ContainerComponentBase
{
    public override string Id => "java";

    public override string DisplayName => "Java";

    public override string Description => "OpenJDK 25 (LTS, Eclipse Temurin)";

    public override ComponentKind Kind => ComponentKind.Toolchain;

    public override int Order => 300;

    public override string? Check => "java -version && javac -version";

    protected override IReadOnlyDictionary<string, string> Root { get; } = new Dictionary<string, string>
    {
        ["*"] = """arch=$(uname -m); case "$arch" in x86_64) arch=x64 ;; esac; mkdir -p /opt/jdk && curl -fsSL "https://api.adoptium.net/v3/binary/latest/25/ga/linux/$arch/jdk/hotspot/normal/eclipse" | tar -xz -C /opt/jdk --strip-components=1 && ln -sf /opt/jdk/bin/* /usr/local/bin/ && echo 'export JAVA_HOME=/opt/jdk' > /etc/profile.d/gits-java.sh""",
    };
}
