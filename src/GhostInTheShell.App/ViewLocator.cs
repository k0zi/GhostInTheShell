using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GhostInTheShell.App.ViewModels;

namespace GhostInTheShell.App;

/// <summary>Maps FooViewModel to FooView by name.</summary>
[RequiresUnreferencedCode("Resolves views by reflection.")]
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null) return null;
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = param.GetType().Assembly.GetType(name);
        return type is null
            ? new TextBlock { Text = "Nem található nézet: " + name }
            : (Control)Activator.CreateInstance(type)!;
    }

    public bool Match(object? data) => data is ViewModelBase;
}
