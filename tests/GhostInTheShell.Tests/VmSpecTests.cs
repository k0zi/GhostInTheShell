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

    [Fact]
    public void Validate_reports_resource_problems()
    {
        Assert.Null(new VmSpec("ok", 1, 1024, 10, "manjaro", []).Validate());
        Assert.NotNull(new VmSpec("ok", 0, 1024, 10, "manjaro", []).Validate());
        Assert.NotNull(new VmSpec("ok", 1, 100, 10, "manjaro", []).Validate());
        Assert.NotNull(new VmSpec("ok", 1, 1024, 0, "manjaro", []).Validate());
        Assert.NotNull(new VmSpec("ok", 1, 1024, 10, "", []).Validate());
    }
}
