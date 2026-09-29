using System.Diagnostics;
using GhostInTheShell.App.Services;

namespace GhostInTheShell.Tests;

public class TerminalLauncherTests
{
    private static ProcessStartInfo Shell()
    {
        var psi = new ProcessStartInfo("podman");
        foreach (var a in new[] { "exec", "-it", "gits-x", "bash", "-l" }) psi.ArgumentList.Add(a);
        return psi;
    }

    [Fact]
    public void Expand_substitutes_command_argv()
    {
        Assert.Equal(["kitty", "podman", "exec", "-it", "gits-x", "bash", "-l"], TerminalLauncher.Expand("kitty {cmd}", Shell()));
    }

    [Fact]
    public void Expand_keeps_quoted_arguments_together()
    {
        var args = TerminalLauncher.Expand("myterm --title 'Ghost shell' -e {cmd}", Shell());
        Assert.Equal(["myterm", "--title", "Ghost shell", "-e", "podman"], args.Take(5));
    }

    [Fact]
    public void Expand_requires_placeholder()
    {
        Assert.Throws<ArgumentException>(() => TerminalLauncher.Expand("kitty", Shell()));
    }
}
