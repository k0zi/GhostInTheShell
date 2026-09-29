using System.Diagnostics;
using System.Text;
using GhostInTheShell.Core.Localization;

namespace GhostInTheShell.Podman;

public sealed record PodmanResult(int ExitCode, string StdOut, string StdErr);

/// <summary>Thin wrapper over the podman executable. Arguments go through ArgumentList, never a shell.</summary>
public class PodmanCli(string executable = "podman")
{
    public string Executable { get; } = executable;

    /// <summary>Runs podman and throws <see cref="PodmanException"/> on a non-zero exit.</summary>
    public async Task<string> RunAsync(IEnumerable<string> args, CancellationToken ct = default)
    {
        var result = await TryRunAsync(args, ct);
        if (result.ExitCode != 0)
            throw Failure(args, result.ExitCode, result.StdErr);
        return result.StdOut;
    }

    /// <summary>Like <see cref="RunAsync(IEnumerable{string}, CancellationToken)"/>, feeding <paramref name="standardInput"/> to the process.</summary>
    /// <remarks>Use for secrets: stdin is not visible in the process list the way arguments are.</remarks>
    public async Task<string> RunAsync(IEnumerable<string> args, string standardInput, CancellationToken ct = default)
    {
        var result = await TryRunAsync(args, standardInput, ct);
        if (result.ExitCode != 0)
            throw Failure(args, result.ExitCode, result.StdErr);
        return result.StdOut;
    }

    public Task<PodmanResult> TryRunAsync(IEnumerable<string> args, CancellationToken ct = default) =>
        TryRunAsync(args, null, ct);

    public virtual async Task<PodmanResult> TryRunAsync(IEnumerable<string> args, string? standardInput, CancellationToken ct = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var exit = await ExecuteAsync(args, line => stdout.AppendLine(line), line => stderr.AppendLine(line), standardInput, ct);
        return new PodmanResult(exit, stdout.ToString(), stderr.ToString().Trim());
    }

    /// <summary>Runs podman and forwards every output line (stdout and stderr) as it arrives.</summary>
    public virtual async Task StreamAsync(IEnumerable<string> args, Action<string> onLine, CancellationToken ct = default)
    {
        // Keep the tail of the output: podman build reports the actual failure in its last lines.
        var tail = new Queue<string>();
        void Collect(string line)
        {
            onLine(line);
            lock (tail)
            {
                tail.Enqueue(line);
                if (tail.Count > 20) tail.Dequeue();
            }
        }

        var exit = await ExecuteAsync(args, Collect, Collect, null, ct);
        if (exit != 0)
            throw Failure(args, exit, string.Join('\n', tail));
    }

    private async Task<int> ExecuteAsync(IEnumerable<string> args, Action<string> onOut, Action<string> onErr,
        string? standardInput, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(Executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new PodmanException(Strings.Format("PodmanStartFailedFormat", Executable, ex.Message), -1, ex.Message);
        }

        if (standardInput is not null)
        {
            try { await process.StandardInput.WriteAsync(standardInput); }
            catch (IOException) { /* The process exited early; its exit code tells the story. */ }
        }

        process.StandardInput.Close();
        await using var _ = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        });

        var outTask = Pump(process.StandardOutput, onOut);
        var errTask = Pump(process.StandardError, onErr);
        await process.WaitForExitAsync(CancellationToken.None);
        await Task.WhenAll(outTask, errTask);
        ct.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    private static async Task Pump(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync() is { } line)
            onLine(line);
    }

    private static PodmanException Failure(IEnumerable<string> args, int exit, string stderr)
    {
        var verb = string.Join(' ', args.Take(2));
        var detail = string.IsNullOrWhiteSpace(stderr) ? Strings.Format("ExitCodeFormat", exit) : stderr;
        return new PodmanException($"podman {verb}: {detail}", exit, stderr);
    }
}
