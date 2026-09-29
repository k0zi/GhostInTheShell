using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Provisioning;

namespace GhostInTheShell.Tests;

public class ContainerfileBuilderTests
{
    private static readonly Catalog Catalog = CatalogLoader.LoadDefault();

    [Fact]
    public void Default_catalog_has_the_requested_distros_and_agents()
    {
        Assert.Equal(["ubuntu-24.04", "ubuntu-26.04", "fedora-43", "manjaro"], Catalog.OperatingSystems.Select(o => o.Id));
        Assert.Equal(["claude", "codex", "pi", "hermes", "opencode"], Catalog.Agents.Select(a => a.Id));
        Assert.Equal(["cpp", "dotnet", "java", "rust", "python"], Catalog.Toolchains.Select(t => t.Id));
    }

    [Fact]
    public void Every_toolchain_supports_every_default_distro()
    {
        foreach (var os in Catalog.OperatingSystems)
        foreach (var toolchain in Catalog.Toolchains)
            Assert.True(toolchain.SupportsOs(os), $"{toolchain.Id} on {os.Id}");
    }

    [Fact]
    public void Toolchains_install_as_root_then_check_as_agent_before_the_agents()
    {
        var file = ContainerfileBuilder.Build(Catalog, "fedora-43", ["claude"], ["rust", "cpp", "java"]);
        var lines = file.Split('\n');

        var cpp = Array.IndexOf(lines, "# toolchain: cpp");
        var java = Array.IndexOf(lines, "# toolchain: java");
        var user = Array.IndexOf(lines, "USER agent");
        var cppCheck = Array.IndexOf(lines, "# toolchain user: cpp");
        var rust = Array.IndexOf(lines, "# toolchain user: rust");
        var claude = Array.IndexOf(lines, "# agent: claude");
        Assert.True(cpp > 0 && java > cpp && user > java, "root parts come before USER agent, in catalog order");
        Assert.True(cppCheck > user && rust > cppCheck && claude > rust, "user parts and checks run as agent, before the agents");
        Assert.DoesNotContain("# toolchain: rust", file); // rust has no root part
        Assert.StartsWith("RUN dnf install", lines[cpp + 1]); // fedora family command
        Assert.Contains("api.adoptium.net", lines[java + 1]); // "*" fallback
        Assert.Contains("sh.rustup.rs", lines[rust + 1]);
        Assert.EndsWith("cargo --version && rustc --version", lines[rust + 1]);
        Assert.DoesNotContain("dotnet-install", file);
    }

    [Fact]
    public void Agent_prerequisites_install_as_root_only_when_the_agent_is_selected()
    {
        var file = ContainerfileBuilder.Build(Catalog, "fedora-43", ["hermes"], []);
        var lines = file.Split('\n');

        var prerequisites = Array.IndexOf(lines, "# agent prerequisites: hermes");
        Assert.True(prerequisites > 0 && prerequisites < Array.IndexOf(lines, "USER agent"));
        Assert.Equal("RUN dnf install -y libatomic && dnf clean all", lines[prerequisites + 1]);

        Assert.DoesNotContain("libatomic", ContainerfileBuilder.Build(Catalog, "fedora-43", ["claude"], []));
    }

    [Fact]
    public void Every_agent_supports_every_default_distro()
    {
        foreach (var os in Catalog.OperatingSystems)
        foreach (var agent in Catalog.Agents)
            Assert.True(agent.SupportsOs(os), $"{agent.Id} on {os.Id}");
    }

    [Fact]
    public void Family_specific_command_is_picked_per_distro()
    {
        Assert.Contains("apt-get install", ContainerfileBuilder.Build(Catalog, "ubuntu-26.04", [], ["python"]));
        Assert.Contains("pacman -Syu", ContainerfileBuilder.Build(Catalog, "manjaro", [], ["python"]));
    }

    [Fact]
    public void Toolchain_without_a_command_for_the_distro_throws()
    {
        var catalog = Catalog with
        {
            Toolchains = [new ToolchainDefinition("zig", "Zig", "", new Dictionary<string, string> { ["debian"] = "apt-get install zig" }, null, null)],
        };
        Assert.Throws<NotSupportedException>(() => ContainerfileBuilder.Build(catalog, "fedora-43", [], ["zig"]));
    }

    [Fact]
    public void Catalog_without_toolchains_or_families_still_loads()
    {
        const string json = """
            { "operatingSystems": [ { "id": "x", "displayName": "X", "image": "x", "setup": "true" } ],
              "commonSetup": [], "agents": [], "userPath": [] }
            """;
        var catalog = CatalogLoader.Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));
        Assert.Empty(catalog.Toolchains);
        Assert.Null(catalog.OperatingSystems[0].Family);
    }

    [Fact]
    public void Build_uses_os_image_and_installs_agents_as_the_agent_user()
    {
        var file = ContainerfileBuilder.Build(Catalog, "fedora-43", ["opencode", "claude"]);
        var lines = file.Split('\n');

        Assert.Equal("FROM registry.fedoraproject.org/fedora:43", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("RUN dnf install"));
        var userLine = Array.IndexOf(lines, "USER agent");
        var claudeLine = Array.FindIndex(lines, l => l.Contains("claude.ai/install.sh"));
        var opencodeLine = Array.FindIndex(lines, l => l.Contains("opencode.ai/install"));
        Assert.True(userLine > 0 && claudeLine > userLine && opencodeLine > claudeLine,
            "agents must be installed after switching user, in catalog order");
        Assert.DoesNotContain("pi-coding-agent", file);
    }

    [Fact]
    public void Same_selection_in_any_order_yields_the_same_image_tag()
    {
        var a = ContainerfileBuilder.Build(Catalog, "manjaro", ["pi", "hermes"]);
        var b = ContainerfileBuilder.Build(Catalog, "manjaro", ["hermes", "pi"]);
        Assert.Equal(a, b);
        Assert.Equal(ContainerfileBuilder.ImageTag("manjaro", a), ContainerfileBuilder.ImageTag("manjaro", b));
        Assert.StartsWith("localhost/gits/manjaro:", ContainerfileBuilder.ImageTag("manjaro", a));
    }

    [Fact]
    public void Different_agents_yield_different_tags()
    {
        var a = ContainerfileBuilder.Build(Catalog, "ubuntu-24.04", ["pi"]);
        var b = ContainerfileBuilder.Build(Catalog, "ubuntu-24.04", ["claude"]);
        Assert.NotEqual(ContainerfileBuilder.ImageTag("ubuntu-24.04", a), ContainerfileBuilder.ImageTag("ubuntu-24.04", b));
    }

    [Fact]
    public void Unknown_os_throws()
    {
        Assert.Throws<KeyNotFoundException>(() => ContainerfileBuilder.Build(Catalog, "windows-95", []));
    }
}
