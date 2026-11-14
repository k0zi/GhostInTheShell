using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Components;
using GhostInTheShell.Core.Components.Json;
using GhostInTheShell.Core.Provisioning;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Tests;

public class ContainerfileBuilderTests
{
    private static readonly Catalog Catalog = CatalogLoader.LoadDefault();
    private static readonly ComponentRegistry Components = BundledComponents.Registry;

    [Fact]
    public void Default_catalog_has_the_requested_distros_and_agents()
    {
        Assert.Equal(["ubuntu-24.04", "ubuntu-26.04", "fedora-43", "manjaro"], Catalog.OperatingSystems.Select(o => o.Id));
        Assert.Equal(["claude", "codex", "pi", "hermes", "opencode"], Components.Agents.Select(a => a.Id));
        Assert.Equal(["cpp", "dotnet", "java", "rust", "python"], Components.Toolchains.Select(t => t.Id));
        Assert.Empty(Catalog.Agents); // components now, not catalog entries
        Assert.Empty(Catalog.Toolchains);
    }

    [Theory]
    [InlineData("git")]
    [InlineData("ripgrep")]
    [InlineData("jq")]
    [InlineData("mc")]
    [InlineData("btop")]
    [InlineData("tmux")]
    public void Every_distro_installs_the_base_developer_tools(string package)
    {
        foreach (var os in Catalog.OperatingSystems)
            Assert.Contains($" {package} ", os.Setup);
    }

    [Fact]
    public void Btop_replaces_htop()
    {
        foreach (var os in Catalog.OperatingSystems)
            Assert.DoesNotContain(" htop ", os.Setup);
    }

    [Fact]
    public void Oh_my_posh_is_installed_for_everyone_and_enabled_for_the_user()
    {
        var file = ContainerfileBuilder.Build(Catalog, Components, "manjaro", [], ["rust"], "dev");
        var lines = file.Split('\n');

        var install = Array.FindIndex(lines, l => l.Contains("oh-my-posh/releases/latest/download/posh-linux-"));
        var user = Array.IndexOf(lines, "USER dev");
        var init = Array.FindIndex(lines, l => l.Contains("oh-my-posh init bash") && l.Contains(">> ~/.bashrc"));
        var rust = Array.IndexOf(lines, "# toolchain user: rust");
        Assert.True(install > 0 && install < user, "the binary goes to /usr/local/bin as root");
        Assert.True(init > user && init < rust, "the prompt is enabled as the user, before toolchains and agents");
    }

    [Fact]
    public void Every_toolchain_supports_every_default_distro()
    {
        foreach (var os in Catalog.OperatingSystems)
        foreach (var toolchain in Components.Toolchains)
            Assert.True(toolchain.SupportsOs(os.ToTarget()), $"{toolchain.Id} on {os.Id}");
    }

    [Fact]
    public void Toolchains_install_as_root_then_check_as_agent_before_the_agents()
    {
        var file = ContainerfileBuilder.Build(Catalog, Components, "fedora-43", ["claude"], ["rust", "cpp", "java"]);
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
        var file = ContainerfileBuilder.Build(Catalog, Components, "fedora-43", ["hermes"], []);
        var lines = file.Split('\n');

        var prerequisites = Array.IndexOf(lines, "# agent prerequisites: hermes");
        Assert.True(prerequisites > 0 && prerequisites < Array.IndexOf(lines, "USER agent"));
        Assert.Equal("RUN dnf install -y libatomic && dnf clean all", lines[prerequisites + 1]);

        Assert.DoesNotContain("libatomic", ContainerfileBuilder.Build(Catalog, Components, "fedora-43", ["claude"], []));
    }

    [Fact]
    public void Every_agent_supports_every_default_distro()
    {
        foreach (var os in Catalog.OperatingSystems)
        foreach (var agent in Components.Agents)
            Assert.True(agent.SupportsOs(os.ToTarget()), $"{agent.Id} on {os.Id}");
    }

    [Fact]
    public void Builds_for_the_chosen_user_after_the_shared_root_layers()
    {
        var file = ContainerfileBuilder.Build(Catalog, Components, "fedora-43", ["claude"], ["cpp"], "dev");
        var lines = file.Split('\n');

        var cpp = Array.IndexOf(lines, "# toolchain: cpp");
        var userAdd = Array.FindIndex(lines, l => l.Contains("useradd -m -s /bin/bash dev"));
        Assert.True(cpp > 0 && userAdd > cpp, "user-specific layers must not break cache sharing of root layers");
        Assert.Contains("USER dev", lines);
        Assert.Contains("WORKDIR /home/dev", lines);
        Assert.Contains(lines, l => l.StartsWith("ENV PATH=/home/dev/.local/bin:"));
        Assert.DoesNotContain("/home/agent", file);
        Assert.DoesNotContain("sudoers", file); // sudo rules depend on the password, set at create time
    }

    [Theory]
    [InlineData("~/.local/bin", "/home/dev/.local/bin")]
    [InlineData("/home/agent/.cargo/bin", "/home/dev/.cargo/bin")] // older user catalogs
    [InlineData("/opt/tools/bin", "/opt/tools/bin")]
    [InlineData("/home/agentx/bin", "/home/agentx/bin")]
    public void ExpandHome_maps_catalog_paths_to_the_user_home(string path, string expected) =>
        Assert.Equal(expected, ContainerfileBuilder.ExpandHome(path, "/home/dev"));

    [Fact]
    public void Invalid_user_name_is_rejected_before_it_reaches_a_shell_command() =>
        Assert.Throws<ArgumentException>(() => ContainerfileBuilder.Build(Catalog, Components, "fedora-43", [], [], "x; rm -rf /"));

    [Fact]
    public void Family_specific_command_is_picked_per_distro()
    {
        Assert.Contains("apt-get install", ContainerfileBuilder.Build(Catalog, Components, "ubuntu-26.04", [], ["python"]));
        Assert.Contains("pacman -Syu", ContainerfileBuilder.Build(Catalog, Components, "manjaro", [], ["python"]));
    }

    [Fact]
    public void Toolchain_without_a_command_for_the_distro_throws()
    {
        var components = new ComponentRegistry(
        [
            new JsonComponent("zig", ComponentKind.Toolchain, root: new Dictionary<string, string> { ["debian"] = "apt-get install zig" }),
        ]);
        Assert.Throws<NotSupportedException>(() => ContainerfileBuilder.Build(Catalog, components, "fedora-43", [], ["zig"]));
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
        Assert.Empty(catalog.UserSetup);
        Assert.Null(catalog.OperatingSystems[0].Family);
    }

    [Fact]
    public void Build_uses_os_image_and_installs_agents_as_the_agent_user()
    {
        var file = ContainerfileBuilder.Build(Catalog, Components, "fedora-43", ["opencode", "claude"]);
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
        var a = ContainerfileBuilder.Build(Catalog, Components, "manjaro", ["pi", "hermes"]);
        var b = ContainerfileBuilder.Build(Catalog, Components, "manjaro", ["hermes", "pi"]);
        Assert.Equal(a, b);
        Assert.Equal(ContainerfileBuilder.ImageTag("manjaro", a), ContainerfileBuilder.ImageTag("manjaro", b));
        Assert.StartsWith("localhost/gits/manjaro:", ContainerfileBuilder.ImageTag("manjaro", a));
    }

    [Fact]
    public void Different_agents_yield_different_tags()
    {
        var a = ContainerfileBuilder.Build(Catalog, Components, "ubuntu-24.04", ["pi"]);
        var b = ContainerfileBuilder.Build(Catalog, Components, "ubuntu-24.04", ["claude"]);
        Assert.NotEqual(ContainerfileBuilder.ImageTag("ubuntu-24.04", a), ContainerfileBuilder.ImageTag("ubuntu-24.04", b));
    }

    [Fact]
    public void Unknown_os_throws()
    {
        Assert.Throws<KeyNotFoundException>(() => ContainerfileBuilder.Build(Catalog, Components, "windows-95", []));
    }

    [Fact]
    public void Unknown_component_throws() =>
        Assert.Throws<KeyNotFoundException>(() => ContainerfileBuilder.Build(Catalog, Components, "fedora-43", ["no-such-agent"]));

    [Fact]
    public void Path_has_the_base_entries_and_only_the_selected_components_own()
    {
        var file = ContainerfileBuilder.Build(Catalog, Components, "fedora-43", ["opencode"], ["rust"], "dev");
        Assert.Contains("ENV PATH=/home/dev/.local/bin:/home/dev/.cargo/bin:/home/dev/.opencode/bin:$PATH", file);
        Assert.DoesNotContain(".hermes", file);
    }

    [Fact]
    public void Component_from_a_plugin_or_json_is_built_like_a_built_in_one()
    {
        var components = new ComponentRegistry(
        [
            .. Components.All,
            new JsonComponent("zig", ComponentKind.Toolchain, "Zig", order: 50,
                root: new Dictionary<string, string> { ["*"] = "install-zig" }, check: "zig version", userPath: ["~/.zig"]),
        ]);
        var lines = ContainerfileBuilder.Build(Catalog, components, "fedora-43", ["claude"], ["cpp", "zig"]).Split('\n');

        var zig = Array.IndexOf(lines, "# toolchain: zig");
        Assert.True(zig > 0 && zig < Array.IndexOf(lines, "# toolchain: cpp"), "order 50 comes before cpp (100)");
        Assert.Equal("RUN install-zig", lines[zig + 1]);
        Assert.Equal("RUN zig version", lines[Array.IndexOf(lines, "# toolchain user: zig") + 1]);
        Assert.Contains(lines, l => l.StartsWith("ENV PATH=") && l.Contains("/home/agent/.zig"));
    }
}
