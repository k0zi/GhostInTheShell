using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Components;
using GhostInTheShell.Core.Localization;
using GhostInTheShell.Core.Models;
using GhostInTheShell.Plugins;

namespace GhostInTheShell.App.ViewModels;

/// <summary>One agent or development environment checkbox in the "new machine" form.</summary>
public sealed partial class ComponentOption(IContainerComponent component) : ObservableObject
{
    public IContainerComponent Component { get; } = component;

    [ObservableProperty] private bool _isSelected;

    /// <summary>False when the component has no install command for the chosen distro.</summary>
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
    public const int SharedFolderTab = 2;

    private readonly Action<NewMachineRequest?> _complete;
    private readonly IReadOnlySet<string> _existingNames;

    public CreateMachineViewModel(Catalog catalog, ComponentRegistry components, IReadOnlySet<string> existingNames,
        Action<NewMachineRequest?> complete)
    {
        _complete = complete;
        _existingNames = existingNames;
        OperatingSystems = catalog.OperatingSystems;
        Agents = new(components.Agents.Select(c => new ComponentOption(c)));
        Toolchains = new(components.Toolchains.Select(c => new ComponentOption(c)));
        _selectedOs = OperatingSystems.FirstOrDefault();
        UpdateSupport();
        _name = SuggestName(existingNames);
        MaxCpus = Environment.ProcessorCount;
        MaxMemoryGb = Math.Max(1, (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024L * 1024 * 1024)));
        _cpus = Math.Min(2, MaxCpus);
        _memoryGb = Math.Min(4, MaxMemoryGb);
    }

    public IReadOnlyList<OsDefinition> OperatingSystems { get; }

    public ObservableCollection<ComponentOption> Agents { get; }

    public ObservableCollection<ComponentOption> Toolchains { get; }

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

    [ObservableProperty] private string _hostFolder = "";

    [RelayCommand]
    private void Create()
    {
        var spec = new VmSpec(
            Name.Trim(),
            (int)Cpus,
            (int)MemoryGb * 1024,
            (int)DiskGb,
            SelectedOs?.Id ?? "",
            Agents.Where(a => a.IsSelected).Select(a => a.Component.Id).ToList(),
            Toolchains.Where(t => t.IsSelected).Select(t => t.Component.Id).ToList(),
            UserName.Trim(),
            NormalizeHostFolder(HostFolder));
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

        var folderError = spec.HostFolder is null ? null
            : !VmSpec.IsValidHostFolder(spec.HostFolder) ? Strings.Get("HostFolderInvalid")
            : !Directory.Exists(spec.HostFolder) ? Strings.Format("HostFolderMissingFormat", spec.HostFolder)
            : null;
        if (folderError is not null)
        {
            ErrorText = folderError;
            SelectedTab = SharedFolderTab;
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
        var target = SelectedOs?.ToTarget();
        foreach (var option in Agents.Concat(Toolchains))
            option.IsSupported = target is not null && option.Component.SupportsOs(target);
    }

    /// <summary>Empty means no sharing; <c>~</c> is the host user's home, as in a shell.</summary>
    internal static string? NormalizeHostFolder(string text)
    {
        var path = text.Trim();
        if (path.Length == 0) return null;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path == "~") path = home;
        else if (path.StartsWith("~/", StringComparison.Ordinal)) path = Path.Combine(home, path[2..]);
        // Path.TrimEndingDirectorySeparator keeps "/" itself intact.
        return Path.TrimEndingDirectorySeparator(path);
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
