using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostInTheShell.App.Services;
using GhostInTheShell.Core.Localization;

namespace GhostInTheShell.App.ViewModels;

public sealed record SettingsResult(string TerminalTemplate, string Language);

/// <summary>Completes with the edited settings, or null when cancelled.</summary>
public sealed partial class SettingsViewModel(string currentTemplate, string currentLanguage, Action<SettingsResult?> complete)
    : ViewModelBase
{
    public IReadOnlyList<TerminalPreset> Presets => TerminalLauncher.Presets;

    public IReadOnlyList<LanguageOption> Languages => Strings.Languages;

    [ObservableProperty] private string _terminalTemplate = currentTemplate;
    [ObservableProperty] private TerminalPreset? _selectedPreset;
    [ObservableProperty] private LanguageOption? _selectedLanguage =
        Strings.Languages.FirstOrDefault(l => l.Code == currentLanguage) ?? Strings.Languages[0];
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
            ErrorText = Strings.Format("TemplateMissingTokenFormat", TerminalLauncher.CommandToken);
            return;
        }

        complete(new SettingsResult(TerminalTemplate.Trim(), SelectedLanguage?.Code ?? Strings.DefaultLanguage));
    }

    [RelayCommand]
    private void Cancel() => complete(null);
}
