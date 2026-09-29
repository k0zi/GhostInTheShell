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
