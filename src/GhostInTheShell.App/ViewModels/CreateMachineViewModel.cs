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

/// <summary>What the "new machine" form produces.</summary>
public sealed record NewMachineRequest(VmSpec Spec, VmCredentials Credentials);

/// <summary>The "new machine" form. Completes with a request, or null when cancelled.</summary>
public sealed partial class CreateMachineViewModel : ViewModelBase
{
    public const int MachineTab = 0;
    public const int UserAccountTab = 1;

    private readonly Action<NewMachineRequest?> _complete;
    private readonly IReadOnlySet<string> _existingNames;

    public CreateMachineViewModel(Catalog catalog, IReadOnlySet<string> existingNames, Action<NewMachineRequest?> complete)
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
    [ObservableProperty] private int _selectedTab = MachineTab;

    [ObservableProperty] private string _userName = VmSpec.DefaultUserName;
    [ObservableProperty] private string _userPassword = "";
    [ObservableProperty] private string _userPasswordConfirm = "";
    [ObservableProperty] private string _adminPassword = "";
    [ObservableProperty] private string _adminPasswordConfirm = "";

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
            Toolchains.Where(t => t.IsSelected).Select(t => t.Definition.Id).ToList(),
            UserName.Trim());
        var credentials = new VmCredentials(UserPassword, AdminPassword);

        // Account problems first, and on their own tab, so the message points at fields the user can see.
        var accountError = !VmSpec.IsValidUserName(spec.UserName) ? Strings.Get("UserNameInvalid")
            : UserPassword != UserPasswordConfirm ? Strings.Get("PasswordsDoNotMatch")
            : AdminPassword != AdminPasswordConfirm ? Strings.Get("AdminPasswordsDoNotMatch")
            : credentials.Validate();
        if (accountError is not null)
        {
            ErrorText = accountError;
            SelectedTab = UserAccountTab;
            return;
        }

        ErrorText = spec.Validate()
                    ?? (_existingNames.Contains(spec.Name) ? Strings.Format("NameExistsFormat", spec.Name) : null);
        if (ErrorText is not null)
        {
            SelectedTab = MachineTab;
            return;
        }

        _complete(new NewMachineRequest(spec, credentials));
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
