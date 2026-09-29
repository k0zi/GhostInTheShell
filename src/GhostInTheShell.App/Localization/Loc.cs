using System.ComponentModel;
using GhostInTheShell.Core.Localization;

namespace GhostInTheShell.App.Localization;

/// <summary>Bindable view of <see cref="Strings"/>: raises an indexer change so every bound text follows a language switch.</summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Loc() => Strings.LanguageChanged += (_, _) =>
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    };

    public string this[string key] => Strings.Get(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}
