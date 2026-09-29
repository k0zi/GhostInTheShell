using System.Diagnostics;
using System.Text;
using GhostInTheShell.Core.Localization;

namespace GhostInTheShell.App.Services;

public sealed record TerminalPreset(string Name, string Executable, string Template)
{
    public override string ToString() => Name;
}

/// <summary>Opens a shell command in an external terminal emulator described by a template like <c>kitty {cmd}</c>.</summary>
public static class TerminalLauncher
{
    public const string CommandToken = "{cmd}";

    public static IReadOnlyList<TerminalPreset> Presets { get; } =
    [
        new("Ptyxis", "ptyxis", "ptyxis --new-window -- {cmd}"),
        new("GNOME Terminal", "gnome-terminal", "gnome-terminal -- {cmd}"),
        new("Konsole", "konsole", "konsole -e {cmd}"),
        new("kitty", "kitty", "kitty {cmd}"),
        new("Alacritty", "alacritty", "alacritty -e {cmd}"),
        new("Ghostty", "ghostty", "ghostty -e {cmd}"),
        new("WezTerm", "wezterm", "wezterm start -- {cmd}"),
        new("xterm", "xterm", "xterm -e {cmd}"),
        new("x-terminal-emulator", "x-terminal-emulator", "x-terminal-emulator -e {cmd}"),
    ];

    /// <summary>The first preset whose executable is on PATH.</summary>
    public static string DetectDefaultTemplate() =>
        Presets.FirstOrDefault(p => IsOnPath(p.Executable))?.Template ?? Presets[^1].Template;

    public static void Launch(string template, ProcessStartInfo shell)
    {
        var args = Expand(template, shell);
        var psi = new ProcessStartInfo(args[0]) { UseShellExecute = false };
        foreach (var arg in args.Skip(1)) psi.ArgumentList.Add(arg);
        Process.Start(psi)?.Dispose();
    }

    /// <summary>Splits the template and substitutes the shell command's argv for the <c>{cmd}</c> token.</summary>
    public static List<string> Expand(string template, ProcessStartInfo shell)
    {
        var tokens = Tokenize(template);
        if (tokens.Count == 0) throw new ArgumentException(Strings.Get("TemplateEmpty"));
        if (!tokens.Contains(CommandToken))
            throw new ArgumentException(Strings.Format("TemplateMissingTokenFormat", CommandToken));

        var cmd = new List<string> { shell.FileName };
        cmd.AddRange(shell.ArgumentList);
        return tokens.SelectMany(t => t == CommandToken ? cmd : [t]).ToList();
    }

    /// <summary>Whitespace split with single/double quote grouping — enough for a terminal command line.</summary>
    internal static List<string> Tokenize(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var inToken = false;
        foreach (var ch in text)
        {
            if (quote is not null)
            {
                if (ch == quote) quote = null;
                else current.Append(ch);
            }
            else if (ch is '"' or '\'')
            {
                quote = ch;
                inToken = true;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (inToken) result.Add(current.ToString());
                current.Clear();
                inToken = false;
            }
            else
            {
                current.Append(ch);
                inToken = true;
            }
        }

        if (inToken) result.Add(current.ToString());
        return result;
    }

    private static bool IsOnPath(string executable) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Any(dir => File.Exists(Path.Combine(dir, executable)));
}
