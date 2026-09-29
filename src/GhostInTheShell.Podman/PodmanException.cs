namespace GhostInTheShell.Podman;

public sealed class PodmanException(string message, int exitCode, string stderr) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
    public string StdErr { get; } = stderr;
}
