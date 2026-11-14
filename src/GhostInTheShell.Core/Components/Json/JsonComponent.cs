using GhostInTheShell.Plugins;

namespace GhostInTheShell.Core.Components.Json;

/// <summary>A component written as JSON in the user's <c>components</c> directory, so it can be added or patched without compiling.</summary>
public sealed class JsonComponent : ContainerComponentBase
{
    private readonly IReadOnlyDictionary<string, string>? _root;
    private readonly string? _user;
    private readonly string? _check;

    public JsonComponent(
        string id,
        ComponentKind kind,
        string? displayName = null,
        string? description = null,
        int order = 1000,
        IReadOnlyDictionary<string, string>? root = null,
        string? user = null,
        string? check = null,
        IReadOnlyList<string>? userPath = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("A component needs an id.");
        Id = id;
        Kind = kind;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        Description = description ?? "";
        Order = order;
        UserPath = userPath ?? [];
        _root = root;
        _user = user;
        _check = check;
    }

    public override string Id { get; }

    public override string DisplayName { get; }

    public override string Description { get; }

    public override ComponentKind Kind { get; }

    public override int Order { get; }

    public override IReadOnlyList<string> UserPath { get; }

    public override string? Check => _check;

    protected override IReadOnlyDictionary<string, string>? Root => _root;

    protected override string? User => _user;
}
