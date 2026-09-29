using System.Text.Json;
using System.Text.Json.Serialization;

namespace GhostInTheShell.Core.Catalog;

public static class CatalogLoader
{
    /// <summary>The user's copy wins over the built-in one so install commands can be patched locally.</summary>
    public static Catalog Load(string? overridePath = null)
    {
        overridePath ??= Path.Combine(AppPaths.ConfigDirectory, "catalog.json");
        if (File.Exists(overridePath))
        {
            using var file = File.OpenRead(overridePath);
            return Parse(file);
        }

        return LoadDefault();
    }

    public static Catalog LoadDefault()
    {
        using var stream = typeof(CatalogLoader).Assembly.GetManifestResourceStream("GhostInTheShell.Core.catalog.json")
                           ?? throw new InvalidOperationException("The built-in catalog.json is missing.");
        return Parse(stream);
    }

    public static Catalog Parse(Stream json) =>
        JsonSerializer.Deserialize(json, CatalogJsonContext.Default.Catalog)
        ?? throw new InvalidDataException("The catalog is empty.");
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Catalog))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;
