using System.Text.Json;
using System.Text.Json.Serialization;
using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Core.Components.Json;

/// <summary>Reads JSON components: one object or an array of them per <c>*.json</c> file.</summary>
public static class JsonComponentSource
{
    /// <summary>Every component in the directory, in file name order; broken files become errors instead of exceptions.</summary>
    public static IReadOnlyList<LoadedComponent> LoadDirectory(string directory, ICollection<ComponentLoadError> errors)
    {
        if (!Directory.Exists(directory)) return [];
        var result = new List<LoadedComponent>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                using var file = File.OpenRead(path);
                result.AddRange(Parse(file).Select(c => new LoadedComponent(c, path)));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
            {
                errors.Add(new ComponentLoadError(path, ex.Message));
            }
        }

        return result;
    }

    public static IReadOnlyList<JsonComponent> Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        var root = document.RootElement;
        return root.ValueKind switch
        {
            JsonValueKind.Array => root.EnumerateArray().Select(ParseOne).ToList(),
            JsonValueKind.Object => [ParseOne(root)],
            _ => throw new InvalidDataException("Expected a component object or an array of them."),
        };
    }

    private static JsonComponent ParseOne(JsonElement element) =>
        element.Deserialize(JsonComponentJsonContext.Default.JsonComponentData)?.ToComponent()
        ?? throw new InvalidDataException("Empty component.");

    /// <summary>Agents and development environments from a catalog in the old format, which defined them inline.</summary>
    public static IReadOnlyList<JsonComponent> FromLegacyCatalog(Catalog.Catalog catalog) =>
    [
        .. catalog.Toolchains.Select((t, i) => new JsonComponent(t.Id, ComponentKind.Toolchain, t.DisplayName, t.Description,
            (i + 1) * 100, t.Root, t.User, t.Check)),
        .. catalog.Agents.Select((a, i) => new JsonComponent(a.Id, ComponentKind.Agent, a.DisplayName, a.Description,
            (i + 1) * 100, a.Root, a.Install, a.Check)),
    ];
}

/// <summary>The JSON shape of a component; <c>install</c> is accepted as the old name of <c>user</c>.</summary>
internal sealed record JsonComponentData(
    string? Id,
    ComponentKind? Kind,
    string? DisplayName,
    string? Description,
    int? Order,
    Dictionary<string, string>? Root,
    string? User,
    string? Install,
    string? Check,
    List<string>? UserPath)
{
    public JsonComponent ToComponent() =>
        new(Id ?? "", Kind ?? throw new InvalidDataException($"Component '{Id}' needs a kind (toolchain or agent)."), DisplayName, Description, Order ?? 1000, Root, User ?? Install, Check, UserPath);
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(JsonComponentData))]
internal sealed partial class JsonComponentJsonContext : JsonSerializerContext;
