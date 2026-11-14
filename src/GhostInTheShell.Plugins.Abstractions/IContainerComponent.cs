namespace GhostInTheShell.Plugins;

/// <summary>
/// Something that can be installed into a machine. Export implementations with
/// <c>[Export(typeof(IContainerComponent))]</c> (System.Composition) to make them discoverable.
/// </summary>
public interface IContainerComponent
{
    /// <summary>Stable identifier stored with each machine; a component with the same id replaces an earlier one.</summary>
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    ComponentKind Kind { get; }

    /// <summary>Position among components of the same kind. Fixes the layer order, and so the image tag.</summary>
    int Order { get; }

    /// <summary>Directories (<c>~</c> is the machine user's home) added to PATH when this component is selected.</summary>
    IReadOnlyList<string> UserPath { get; }

    /// <summary>Command that proves the install worked; run at build time as the machine user.</summary>
    string? Check { get; }

    /// <summary>False when the component cannot be installed on this distribution.</summary>
    bool SupportsOs(TargetOs os);

    /// <summary>Shell command run as root, or null when nothing is needed.</summary>
    string? GetRootCommand(TargetOs os);

    /// <summary>Shell command run as the machine user after all root commands, or null when nothing is needed.</summary>
    string? GetUserCommand(TargetOs os);
}
