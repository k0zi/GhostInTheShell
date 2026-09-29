using System.Text.Json;
using System.Text.Json.Serialization;
using GhostInTheShell.Core;

namespace GhostInTheShell.App.Services;

/// <param name="Language">UI language code; null means English.</param>
public sealed record AppSettings(string? TerminalTemplate = null, bool DarkTheme = true, string? Language = null);

public sealed class SettingsService
{
    private readonly string _path = Path.Combine(AppPaths.ConfigDirectory, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize(File.ReadAllText(_path), SettingsJsonContext.Default.AppSettings) ?? new();
        }
        catch (JsonException)
        {
            // A hand-edited file that no longer parses should not stop the app from starting.
            Current = new();
        }
    }

    public void Save(AppSettings settings)
    {
        Current = settings;
        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
