using GhostInTheShell.Core.Models;

namespace GhostInTheShell.Tests;

public class VmSpecTests
{
    [Theory]
    [InlineData("ghost-1", true)]
    [InlineData("a1", true)]
    [InlineData("a", false)]
    [InlineData("-ghost", false)]
    [InlineData("Ghost", false)]
    [InlineData("ghost_1", false)]
    [InlineData("ghost;rm", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345", false)]
    public void Name_validation(string name, bool valid) => Assert.Equal(valid, VmSpec.IsValidName(name));

    [Theory]
    [InlineData("agent", true)]
    [InlineData("_svc", true)]
    [InlineData("dev-user_2", true)]
    [InlineData("root", false)]
    [InlineData("Dev", false)]
    [InlineData("2dev", false)]
    [InlineData("", false)]
    [InlineData("a b", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456", false)] // 33 characters
    public void IsValidUserName(string name, bool expected) => Assert.Equal(expected, VmSpec.IsValidUserName(name));

    [Fact]
    public void Default_user_is_agent_and_is_validated()
    {
        Assert.Equal("agent", new VmSpec("ok", 1, 1024, 10, "manjaro", [], []).UserName);
        Assert.NotNull(new VmSpec("ok", 1, 1024, 10, "manjaro", [], [], "root").Validate());
    }

    [Theory]
    [InlineData("/home/me/projects", true)]
    [InlineData("/", true)]
    [InlineData("relative/path", false)]
    [InlineData("~/projects", false)] // the form expands ~ before it gets here
    [InlineData("/home/me/a:b", false)] // would split the podman -v spec
    [InlineData("/home/me/a\nb", false)]
    public void IsValidHostFolder(string path, bool expected) => Assert.Equal(expected, VmSpec.IsValidHostFolder(path));

    [Fact]
    public void Host_folder_is_optional_and_validated()
    {
        Assert.Null(new VmSpec("ok", 1, 1024, 10, "manjaro", [], []).HostFolder);
        Assert.Null(new VmSpec("ok", 1, 1024, 10, "manjaro", [], [], "agent", "/srv").Validate());
        Assert.NotNull(new VmSpec("ok", 1, 1024, 10, "manjaro", [], [], "agent", "srv").Validate());
    }

    [Fact]
    public void Form_normalizes_the_host_folder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Null(App.ViewModels.CreateMachineViewModel.NormalizeHostFolder("   "));
        Assert.Equal(home, App.ViewModels.CreateMachineViewModel.NormalizeHostFolder("~"));
        Assert.Equal(Path.Combine(home, "code"), App.ViewModels.CreateMachineViewModel.NormalizeHostFolder(" ~/code/ "));
        Assert.Equal("/srv/data", App.ViewModels.CreateMachineViewModel.NormalizeHostFolder("/srv/data/"));
        Assert.Equal("/", App.ViewModels.CreateMachineViewModel.NormalizeHostFolder("/"));
    }

    [Fact]
    public void Credentials_never_print_passwords_and_reject_line_breaks()
    {
        var credentials = new VmCredentials("s3cret", "t0psecret");
        Assert.DoesNotContain("s3cret", credentials.ToString());
        Assert.DoesNotContain("t0psecret", credentials.ToString());
        Assert.Null(credentials.Validate());
        Assert.NotNull(new VmCredentials("a\nroot:x", null).Validate());
    }

    [Fact]
    public void Validate_reports_resource_problems()
    {
        Assert.Null(new VmSpec("ok", 1, 1024, 10, "manjaro", [], []).Validate());
        Assert.NotNull(new VmSpec("ok", 0, 1024, 10, "manjaro", [], []).Validate());
        Assert.NotNull(new VmSpec("ok", 1, 100, 10, "manjaro", [], []).Validate());
        Assert.NotNull(new VmSpec("ok", 1, 1024, 0, "manjaro", [], []).Validate());
        Assert.NotNull(new VmSpec("ok", 1, 1024, 10, "", [], []).Validate());
    }
}
