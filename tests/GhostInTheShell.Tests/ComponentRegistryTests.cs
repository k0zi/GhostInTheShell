using System.Text;
using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Components;
using GhostInTheShell.Core.Components.Json;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Tests;

public sealed class ComponentRegistryTests : IDisposable
{
    private static readonly Catalog Catalog = CatalogLoader.LoadDefault();
    private readonly string _dir = Directory.CreateTempSubdirectory("gits-components-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ComponentRegistry Load(Catalog? catalog = null) => BundledComponents.Load(catalog ?? Catalog, _dir);

    [Fact]
    public void Every_bundled_component_comes_from_its_own_plugin()
    {
        var registry = Load();
        Assert.Equal(10, registry.All.Count);
        Assert.Equal(10, registry.Loaded.Select(l => l.Source).Distinct().Count());
        Assert.All(registry.Loaded, l => Assert.StartsWith(BundledComponents.Directory, l.Source));
        Assert.Empty(registry.Errors);
        Assert.Equal(registry.Toolchains.Concat(registry.Agents), registry.All);
    }

    [Fact]
    public void Json_component_with_a_built_in_id_replaces_it()
    {
        File.WriteAllText(Path.Combine(_dir, "claude.json"), """
            // comments are allowed
            { "id": "claude", "kind": "agent", "displayName": "Claude (patched)", "order": 100,
              "user": "echo patched", "check": "claude --version", }
            """);

        var registry = Load();
        var claude = registry.Get("claude");
        Assert.Equal("Claude (patched)", claude.DisplayName);
        Assert.Equal("echo patched", claude.GetUserCommand(new TargetOs("fedora-43", "fedora")));
        Assert.EndsWith("claude.json", registry.Loaded.Single(l => l.Component.Id == "claude").Source);
        Assert.Equal(10, registry.All.Count);
    }

    [Fact]
    public void A_file_can_hold_several_components()
    {
        File.WriteAllText(Path.Combine(_dir, "extra.json"), """
            [
              { "id": "zig", "kind": "Toolchain", "root": { "*": "install-zig" } },
              { "id": "aider", "kind": "agent", "install": "pipx install aider-chat", "userPath": ["~/.local/bin"] }
            ]
            """);

        var registry = Load();
        Assert.Equal("zig", registry.Get("zig").DisplayName); // the id when no name is given
        Assert.Equal(ComponentKind.Agent, registry.Get("aider").Kind);
        Assert.Equal("pipx install aider-chat", registry.Get("aider").GetUserCommand(new TargetOs("x", null)));
        Assert.Equal("aider", registry.Agents[^1].Id); // default order 1000 puts it last
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "id": "x" }""")] // no kind
    [InlineData("""{ "kind": "agent" }""")] // no id
    [InlineData("42")]
    public void Broken_json_file_is_an_error_not_an_exception(string json)
    {
        File.WriteAllText(Path.Combine(_dir, "broken.json"), json);

        var registry = Load();
        var error = Assert.Single(registry.Errors);
        Assert.EndsWith("broken.json", error.Path);
        Assert.Equal(10, registry.All.Count);
    }

    [Fact]
    public void Old_user_catalog_with_inline_agents_and_toolchains_still_works()
    {
        const string json = """
            { "operatingSystems": [ { "id": "x", "displayName": "X", "image": "x", "setup": "true", "family": "debian" } ],
              "commonSetup": [], "userPath": [],
              "agents": [ { "id": "claude", "displayName": "Claude Code", "description": "", "install": "old-install", "check": null },
                          { "id": "aider", "displayName": "Aider", "description": "", "install": "pipx install aider-chat", "check": null } ],
              "toolchains": [ { "id": "zig", "displayName": "Zig", "description": "", "root": { "debian": "apt-get install zig" } } ] }
            """;
        var catalog = CatalogLoader.Parse(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        var registry = Load(catalog);
        var os = catalog.GetOs("x").ToTarget();
        Assert.Equal("old-install", registry.Get("claude").GetUserCommand(os));
        Assert.Equal("Aider", registry.Get("aider").DisplayName);
        Assert.Equal("apt-get install zig", registry.Get("zig").GetRootCommand(os));
        Assert.False(registry.Get("zig").SupportsOs(new TargetOs("f", "fedora")));
    }

    [Fact]
    public void Missing_name_is_found_as_null_and_get_throws()
    {
        Assert.Null(BundledComponents.Registry.Find("nope"));
        Assert.Throws<KeyNotFoundException>(() => BundledComponents.Registry.Get("nope"));
    }

    [Fact]
    public void Json_component_parses_from_a_stream()
    {
        var parsed = JsonComponentSource.Parse(new MemoryStream(Encoding.UTF8.GetBytes(
            """{ "id": "go", "kind": "toolchain", "displayName": "Go", "check": "go version" }""")));
        var go = Assert.Single(parsed);
        Assert.Equal("go version", go.Check);
        Assert.True(go.SupportsOs(new TargetOs("any", null)));
        Assert.Null(go.GetRootCommand(new TargetOs("any", null)));
    }
}
