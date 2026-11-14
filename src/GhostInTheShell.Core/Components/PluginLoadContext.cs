using System.Reflection;
using System.Runtime.Loader;

namespace GhostInTheShell.Core.Components;

/// <summary>
/// Isolates one plugin with its own dependencies. The contract assemblies always come from the app,
/// otherwise the plugin's <c>IContainerComponent</c> would be a different type from the app's.
/// </summary>
internal sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(Path.GetFileNameWithoutExtension(pluginPath))
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    internal static bool IsShared(AssemblyName name) =>
        name.Name is { } simple
        && (simple == typeof(Plugins.IContainerComponent).Assembly.GetName().Name
            || simple.StartsWith("System.Composition", StringComparison.Ordinal));

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsShared(assemblyName)) return null;
        return _resolver.ResolveAssemblyToPath(assemblyName) is { } path ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) =>
        _resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } path ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
}
