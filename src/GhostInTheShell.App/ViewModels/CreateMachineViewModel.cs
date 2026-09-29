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
        _selectedOs = OperatingSystems.FirstOrDefault();
        _name = SuggestName(existingNames);
        MaxCpus = Environment.ProcessorCount;
        MaxMemoryGb = Math.Max(1, (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024L * 1024 * 1024)));
        _cpus = Math.Min(2, MaxCpus);
        _memoryGb = Math.Min(4, MaxMemoryGb);
    }

    public IReadOnlyList<OsDefinition> OperatingSystems { get; }

    public ObservableCollection<AgentOption> Agents { get; }

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
            Agents.Where(a => a.IsSelected).Select(a => a.Definition.Id).ToList());

        ErrorText = spec.Validate()
                    ?? (_existingNames.Contains(spec.Name) ? Strings.Format("NameExistsFormat", spec.Name) : null);
        if (ErrorText is null) _complete(spec);
    }

    [RelayCommand]
    private void Cancel() => _complete(null);

    private static string SuggestName(IReadOnlySet<string> existing)
    {
        for (var i = 1; ; i++)
        {
            var name = $"ghost-{i}";
            if (!existing.Contains(name)) return name;
        }
    }
}
