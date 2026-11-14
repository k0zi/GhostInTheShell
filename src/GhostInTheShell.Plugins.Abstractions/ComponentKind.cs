namespace GhostInTheShell.Plugins;

/// <summary>What a component is; also the order its layers take in the Containerfile.</summary>
public enum ComponentKind
{
    /// <summary>A language development environment (compiler, SDK, package tools).</summary>
    Toolchain,

    /// <summary>A coding agent.</summary>
    Agent,
}
