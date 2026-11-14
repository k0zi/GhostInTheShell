using System.Composition.Hosting;
using System.Reflection;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.Core.Components;

/// <summary>
/// Finds plugin assemblies (<c>&lt;directory&gt;/&lt;name&gt;/&lt;name&gt;.dll</c>, next to their dependencies) and collects
/// their <see cref="IContainerComponent"/> exports with System.Composition.
/// </summary>
public static class PluginLoader
{
    /// <summary>The components an assembly exports.</summary>
    public static IReadOnlyList<IContainerComponent> Compose(Assembly assembly)
    {
        using var container = new ContainerConfiguration().WithAssembly(assembly).CreateContainer();
        return container.GetExports<IContainerComponent>().ToList();
    }

    /// <summary>Every plugin's components, directory by directory; a plugin that fails becomes an error and is skipped.</summary>
    public static IReadOnlyList<LoadedComponent> LoadDirectories(IEnumerable<string> directories, ICollection<ComponentLoadError> errors)
    {
        var result = new List<LoadedComponent>();
        // An installed app's own plugins folder is also the system-wide one; load it once.
        var distinct = directories.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal);
        foreach (var path in distinct.SelectMany(FindPlugins))
        {
            try
            {
                var assembly = new PluginLoadContext(path).LoadFromAssemblyPath(path);
                var components = Compose(assembly);
                if (components.Count == 0)
                    errors.Add(new ComponentLoadError(path, $"No {nameof(IContainerComponent)} export found."));
                result.AddRange(components.Select(c => new LoadedComponent(c, path)));
            }
            catch (Exception ex)
            {
                // Third-party code: whatever it throws while loading or composing must not take the app down.
                errors.Add(new ComponentLoadError(path, Describe(ex)));
            }
        }

        return result;
    }

    /// <summary>Plugin assemblies in a directory, in name order so the result does not depend on the file system.</summary>
    internal static IEnumerable<string> FindPlugins(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateDirectories(directory)
            .Order(StringComparer.Ordinal)
            .Select(dir => Path.Combine(dir, Path.GetFileName(dir) + ".dll"))
            .Where(File.Exists);
    }

    private static string Describe(Exception ex) => ex switch
    {
        ReflectionTypeLoadException rtle when rtle.LoaderExceptions.FirstOrDefault(e => e is not null) is { } inner => inner.Message,
        TargetInvocationException { InnerException: { } inner } => inner.Message,
        _ => ex.Message,
    };
}
