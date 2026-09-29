namespace GhostInTheShell.Core;

public static class AppPaths
{
    public static string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "ghostintheshell");
}
