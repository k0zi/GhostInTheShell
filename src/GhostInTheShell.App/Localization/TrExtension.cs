using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace GhostInTheShell.App.Localization;

/// <summary><c>{l:Tr Key}</c> — a localized text that updates when the language changes.</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
}
