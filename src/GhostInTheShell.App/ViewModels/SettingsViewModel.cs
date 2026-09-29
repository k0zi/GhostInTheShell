using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostInTheShell.App.Services;

namespace GhostInTheShell.App.ViewModels;

/// <summary>Completes with the new terminal template, or null when cancelled.</summary>
public sealed partial class SettingsViewModel(string currentTemplate, Action<string?> complete) : ViewModelBase
{
    public IReadOnlyList<TerminalPreset> Presets => TerminalLauncher.Presets;

    [ObservableProperty] private string _terminalTemplate = currentTemplate;
    [ObservableProperty] private TerminalPreset? _selectedPreset;
    [ObservableProperty] private string? _errorText;

    partial void OnSelectedPresetChanged(TerminalPreset? value)
    {
        if (value is not null) TerminalTemplate = value.Template;
    }

    [RelayCommand]
    private void Save()
    {
        if (!TerminalTemplate.Contains(TerminalLauncher.CommandToken, StringComparison.Ordinal))
        {
            ErrorText = $"A sablonnak tartalmaznia kell a {TerminalLauncher.CommandToken} helyőrzőt.";
            return;
        }

        complete(TerminalTemplate.Trim());
    }

    [RelayCommand]
    private void Cancel() => complete(null);
}
