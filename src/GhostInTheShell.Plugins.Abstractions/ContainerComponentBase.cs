namespace GhostInTheShell.Plugins;

/// <summary>
/// A component described by plain commands: root commands per distro family, one user command and a check.
/// Override the methods for anything that needs more logic.
/// </summary>
public abstract class ContainerComponentBase : IContainerComponent
{
    public abstract string Id { get; }

    public abstract string DisplayName { get; }

    public virtual string Description => "";

    public abstract ComponentKind Kind { get; }

    public virtual int Order => 1000;

    public virtual IReadOnlyList<string> UserPath => [];

    public virtual string? Check => null;

    /// <summary>Commands run as root, keyed by <see cref="TargetOs.Family"/>; <c>*</c> matches any family. Null means none.</summary>
    protected virtual IReadOnlyDictionary<string, string>? Root => null;

    /// <summary>Command run as the machine user, the same on every distro.</summary>
    protected virtual string? User => null;

    /// <summary>Without root commands the component runs anywhere; with them, the distro needs a matching entry.</summary>
    public virtual bool SupportsOs(TargetOs os) => Root is null || FamilyCommands.Resolve(Root, os) is not null;

    public virtual string? GetRootCommand(TargetOs os) => FamilyCommands.Resolve(Root, os);

    public virtual string? GetUserCommand(TargetOs os) => User;

    public override string ToString() => $"{Kind} {Id}";
}
