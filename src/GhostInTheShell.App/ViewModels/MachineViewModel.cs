using CommunityToolkit.Mvvm.ComponentModel;
using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Components;
using GhostInTheShell.Core.Localization;
using GhostInTheShell.Core.Models;

namespace GhostInTheShell.App.ViewModels;

/// <summary>One card in the machine list: either a machine the provider reports, or one still being created.</summary>
public sealed partial class MachineViewModel : ViewModelBase
{
    private const int MaxLogLines = 400;
    private readonly Catalog _catalog;
    private readonly ComponentRegistry _components;
    private readonly List<string> _logLines = [];

    public MachineViewModel(VmInfo info, Catalog catalog, ComponentRegistry components)
    {
        _catalog = catalog;
        _components = components;
        _info = info;
    }

    /// <summary>Placeholder shown while <see cref="Core.IVmProvider.CreateAsync"/> runs.</summary>
    public static MachineViewModel Pending(VmSpec spec, Catalog catalog, ComponentRegistry components, CancellationTokenSource cts) =>
        new(new VmInfo("", VmState.Creating, spec, DateTimeOffset.Now), catalog, components) { CreationCts = cts };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Id), nameof(Name), nameof(State), nameof(OsName), nameof(AgentNames), nameof(HasAgents),
        nameof(ToolchainNames), nameof(HasToolchains), nameof(UserName), nameof(HostFolder),
        nameof(ResourcesText), nameof(UsageText), nameof(IsOverDiskLimit), nameof(IsRunning), nameof(IsStopped),
        nameof(IsCreating), nameof(IsFailed), nameof(StateText), nameof(CanStart), nameof(CanStop), nameof(CanOpenTerminal),
        nameof(CanDelete))]
    private VmInfo _info;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(CanStop), nameof(CanOpenTerminal), nameof(CanDelete))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFailed), nameof(StateText))]
    private string? _error;

    [ObservableProperty] private string _log = "";

    [ObservableProperty] private bool _isLogVisible;

    public CancellationTokenSource? CreationCts { get; private init; }

    public string Id => Info.Id;

    public string Name => Info.Name;

    public VmState State => Info.State;

    public string OsName => _catalog.OperatingSystems.FirstOrDefault(o => o.Id == Info.Spec.OsId)?.DisplayName ?? Info.Spec.OsId;

    public IReadOnlyList<string> AgentNames =>
        Info.Spec.AgentIds.Select(DisplayNameOf).ToList();

    public string UserName => Info.Spec.UserName;

    public string? HostFolder => Info.Spec.HostFolder;

    public bool HasAgents => Info.Spec.AgentIds.Count > 0;

    public IReadOnlyList<string> ToolchainNames =>
        Info.Spec.ToolchainIds.Select(DisplayNameOf).ToList();

    public bool HasToolchains => Info.Spec.ToolchainIds.Count > 0;

    // A machine may name a component whose plugin has since been removed.
    private string DisplayNameOf(string id) => _components.Find(id)?.DisplayName ?? id;

    public string ResourcesText =>
        Strings.Format("ResourcesFormat", Info.Spec.Cpus, Info.Spec.MemoryMb / 1024.0, Info.Spec.DiskGb);

    public string? UsageText => Info.DiskUsedBytes is { } used
        ? Strings.Format("UsageFormat", used / (1024.0 * 1024 * 1024), Info.Spec.DiskGb)
        : null;

    public bool IsOverDiskLimit => Info.IsOverDiskLimit;

    public bool IsRunning => State == VmState.Running;

    public bool IsStopped => State == VmState.Stopped;

    public bool IsCreating => State == VmState.Creating && Error is null;

    public bool IsFailed => Error is not null || State == VmState.Error;

    public string StateText => Error is not null && State == VmState.Creating
        ? Strings.Get("StateFailed")
        : State switch
        {
            VmState.Creating => Strings.Get("StateCreating"),
            VmState.Running => Strings.Get("StateRunning"),
            VmState.Stopped => Strings.Get("StateStopped"),
            _ => Info.StatusText ?? Strings.Get("StateError"),
        };

    public bool CanStart => !IsBusy && State is VmState.Stopped or VmState.Error;

    public bool CanStop => !IsBusy && State == VmState.Running;

    public bool CanOpenTerminal => !IsBusy && State == VmState.Running;

    public bool CanDelete => !IsBusy && State != VmState.Creating;

    public void AppendLog(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > MaxLogLines) _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
        Log = string.Join('\n', _logLines);
    }

    /// <summary>Re-reads the texts built in code after the UI language changed.</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(ResourcesText));
        OnPropertyChanged(nameof(UsageText));
        OnPropertyChanged(nameof(StateText));
    }

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(IsCreating));
}
