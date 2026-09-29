using System.Text.Json;
using GhostInTheShell.Core.Models;
using GhostInTheShell.Podman;

namespace GhostInTheShell.Tests;

public class PodmanParserTests
{
    private const string PsJson = """
        [
          {
            "Id": "abc",
            "Names": ["gits-ghost-1"],
            "State": "running",
            "Status": "Up 5 minutes",
            "Created": 1786531075,
            "Labels": {
              "gits.managed": "true", "gits.name": "ghost-1", "gits.os": "fedora-43",
              "gits.agents": "claude,opencode", "gits.toolchains": "rust,python", "gits.cpus": "2", "gits.memory-mb": "4096", "gits.disk-gb": "20"
            },
            "Size": { "rootFsSize": 900, "rwSize": 1234 }
          },
          {
            "Id": "def",
            "Names": ["someone-else"],
            "State": "exited",
            "Labels": { "gits.managed": "true" }
          },
          {
            "Id": "ghi",
            "Names": ["gits-ghost-2"],
            "State": "exited",
            "Created": 1786531075,
            "Labels": { "gits.name": "ghost-2", "gits.os": "manjaro", "gits.agents": "", "gits.cpus": "1", "gits.memory-mb": "1024", "gits.disk-gb": "5" }
          }
        ]
        """;

    [Fact]
    public void ParseContainers_reads_spec_from_labels()
    {
        var machines = PodmanParser.ParseContainers(PsJson);

        Assert.Equal(2, machines.Count); // the one without a gits.name label is skipped
        var first = machines[0];
        Assert.Equal("gits-ghost-1", first.Id);
        Assert.Equal(VmState.Running, first.State);
        Assert.Equal(new VmSpec("ghost-1", 2, 4096, 20, "fedora-43", [], []) with { AgentIds = first.Spec.AgentIds, ToolchainIds = first.Spec.ToolchainIds }, first.Spec);
        Assert.Equal(["claude", "opencode"], first.Spec.AgentIds);
        Assert.Equal(["rust", "python"], first.Spec.ToolchainIds);
        Assert.Equal(1234, first.DiskUsedBytes);

        Assert.Equal(VmState.Stopped, machines[1].State);
        Assert.Empty(machines[1].Spec.AgentIds);
        Assert.Empty(machines[1].Spec.ToolchainIds); // made before toolchains existed: no label
        Assert.Null(machines[1].DiskUsedBytes);
    }

    [Fact]
    public void ParseContainers_handles_empty_output() => Assert.Empty(PodmanParser.ParseContainers(""));

    [Theory]
    [InlineData("running", VmState.Running)]
    [InlineData("exited", VmState.Stopped)]
    [InlineData("created", VmState.Stopped)]
    [InlineData("paused", VmState.Stopped)]
    [InlineData("weird", VmState.Error)]
    public void MapState(string podman, VmState expected) => Assert.Equal(expected, PodmanParser.MapState(podman));

    [Fact]
    public void ParseDu_reads_bytes_per_path()
    {
        var result = PodmanParser.ParseDu("123\t/a/b\n456\t/c d/e\ngarbage\n");
        Assert.Equal(123, result["/a/b"]);
        Assert.Equal(456, result["/c d/e"]);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseVolumes_maps_name_to_mountpoint()
    {
        var result = PodmanParser.ParseVolumes("""[{"Name":"gits-a-home","Mountpoint":"/x/_data"}]""");
        Assert.Equal("/x/_data", result["gits-a-home"]);
    }

    [Theory]
    [InlineData(true, "overlay", "xfs", false)]
    [InlineData(false, "overlay", "extfs", false)]
    [InlineData(false, "overlay", "xfs", true)]
    public void Disk_quota_needs_rootful_overlay_on_xfs(bool rootless, string driver, string fs, bool expected)
    {
        var json = $$"""
            {"host":{"security":{"rootless":{{rootless.ToString().ToLowerInvariant()}} } },
             "store":{"graphDriverName":"{{driver}}","graphStatus":{"Backing Filesystem":"{{fs}}" } } }
            """;
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(expected, PodmanProvider.DiskQuotaEnforceable(doc.RootElement));
    }

    [Fact]
    public void Create_args_carry_limits_and_labels()
    {
        var args = PodmanProvider.BuildCreateArgs(new VmSpec("ghost-1", 3, 2048, 15, "manjaro", ["pi"], ["rust", "java"]), "img:tag");

        Assert.Equal("create", args[0]);
        Assert.Equal("img:tag", args[^1]);
        Assert.Equal("3", args[args.IndexOf("--cpus") + 1]);
        Assert.Equal("2048m", args[args.IndexOf("--memory") + 1]);
        Assert.Equal("gits-ghost-1-home:/home/agent", args[args.IndexOf("-v") + 1]);
        Assert.Contains("gits.agents=pi", args);
        Assert.Contains("gits.toolchains=rust,java", args);
        Assert.Contains("gits.disk-gb=15", args);
    }

    [Fact]
    public void Create_args_mount_home_and_label_the_user()
    {
        var args = PodmanProvider.BuildCreateArgs(new VmSpec("ghost-1", 1, 1024, 5, "manjaro", [], [], "dev"), "img:tag");
        Assert.Equal("gits-ghost-1-home:/home/dev", args[args.IndexOf("-v") + 1]);
        Assert.Contains("gits.user=dev", args);
    }

    [Fact]
    public void Machines_without_a_user_label_use_the_default_user()
    {
        var machines = PodmanParser.ParseContainers(PsJson);
        Assert.All(machines, m => Assert.Equal("agent", m.Spec.UserName));
    }

    [Fact]
    public void Sudo_needs_the_password_only_when_one_was_set()
    {
        Assert.Contains("dev ALL=(ALL) NOPASSWD:ALL", PodmanProvider.SudoersCommand("dev", VmCredentials.None));
        var withPassword = PodmanProvider.SudoersCommand("dev", new VmCredentials("pw", null));
        Assert.Contains("dev ALL=(ALL) ALL", withPassword);
        Assert.DoesNotContain("pw", withPassword.Replace("/etc/sudoers.d", ""));
    }

    [Fact]
    public void Chpasswd_input_sets_only_the_given_passwords()
    {
        Assert.Null(PodmanProvider.ChpasswdInput("dev", VmCredentials.None));
        Assert.Equal("dev:a:b\n", PodmanProvider.ChpasswdInput("dev", new VmCredentials("a:b", "")));
        Assert.Equal("dev:u\nroot:r\n", PodmanProvider.ChpasswdInput("dev", new VmCredentials("u", "r")));
        Assert.Equal("root:r\n", PodmanProvider.ChpasswdInput("dev", new VmCredentials(null, "r")));
    }
}
