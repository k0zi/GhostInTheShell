using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Localization;
using GhostInTheShell.Core.Models;

namespace GhostInTheShell.App.ViewModels;

public sealed partial class AgentOption(AgentDefinition definition) : ObservableObject
{
    public AgentDefinition Definition { get; } = definition;

    [ObservableProperty] private bool _isSelected;

    /// <summary>False when the agent's prerequisites have no install command for the chosen distro.</summary>
    [ObservableProperty] private bool _isSupported = true;

    partial void OnIsSupportedChanged(bool value)
    {
        if (!value) IsSelected = false;
    }
}

public sealed partial class ToolchainOption(ToolchainDefinition definition) : ObservableObject
{
    public ToolchainDefinition Definition { get; } = definition;

    [ObservableProperty] private bool _isSelected;

    /// <summary>False when the chosen distro has no install command for this toolchain.</summary>
    [ObservableProperty] private bool _isSupported = true;

    partial void OnIsSupportedChanged(bool value)
    {
        if (!value) IsSelected = false;
    }
}

/// <summary>The "new machine" form. Completes with a spec, or null when cancelled.</summary>
public sealed partial class CreateMachineViewModel : ViewModelBase
{
    private readonly Action<VmSpec?> _complete;
    private readonly IReadOnlySet<string> _existingNames;

    public CreateMachineViewModel(Catalog catalog, IReadOnlySet<string> existingNames, Action<VmSpec?> complete)
    {
        _complete = complete;
        _existingNames = existingNames;
        OperatingSystems = catalog.OperatingSystems;
        Agents = new(catalog.Agents.Select(a => new AgentOption(a)));
        Toolchains = new(catalog.Toolchains.Select(t => new ToolchainOption(t)));
        _selectedOs = OperatingSystems.FirstOrDefault();
        UpdateSupport();
        _name = SuggestName(existingNames);
        MaxCpus = Environment.ProcessorCount;
        MaxMemoryGb = Math.Max(1, (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024L * 1024 * 1024)));
        _cpus = Math.Min(2, MaxCpus);
        _memoryGb = Math.Min(4, MaxMemoryGb);
    }

    public IReadOnlyList<OsDefinition> OperatingSystems { get; }

    public ObservableCollection<AgentOption> Agents { get; }

    public ObservableCollection<ToolchainOption> Toolchains { get; }

    public int MaxCpus { get; }

    public int MaxMemoryGb { get; }

    public int MaxDiskGb => 1024;

    [ObservableProperty] private string _name;
    [ObservableProperty] private double _cpus;
    [ObservableProperty] private double _memoryGb;
    [ObservableProperty] private double _diskGb = 20;
    [ObservableProperty] private OsDefinition? _selectedOs;
    [ObservableProperty] private string? _errorText;

    [RelayCommand]
    private void Create()
    {
        var spec = new VmSpec(
            Name.Trim(),
            (int)Cpus,
            (int)MemoryGb * 1024,
            (int)DiskGb,
            SelectedOs?.Id ?? "",
            Agents.Where(a => a.IsSelected).Select(a => a.Definition.Id).ToList(),
            Toolchains.Where(t => t.IsSelected).Select(t => t.Definition.Id).ToList());

        ErrorText = spec.Validate()
                    ?? (_existingNames.Contains(spec.Name) ? Strings.Format("NameExistsFormat", spec.Name) : null);
        if (ErrorText is null) _complete(spec);
    }

    [RelayCommand]
    private void Cancel() => _complete(null);

    partial void OnSelectedOsChanged(OsDefinition? value) => UpdateSupport();

    private void UpdateSupport()
    {
        foreach (var option in Agents)
            option.IsSupported = SelectedOs is not null && option.Definition.SupportsOs(SelectedOs);
        foreach (var option in Toolchains)
            option.IsSupported = SelectedOs is not null && option.Definition.SupportsOs(SelectedOs);
    }

    private static string SuggestName(IReadOnlySet<string> existing)
    {
        for (var i = 1; ; i++)
        {
            var name = $"ghost-{i}";
            if (!existing.Contains(name)) return name;
        }
    }
}
