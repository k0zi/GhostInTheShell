using System.Collections.ObjectModel;
using Avalonia.Controls.Notifications;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GhostInTheShell.App.Services;
using GhostInTheShell.Core;
using GhostInTheShell.Core.Catalog;
using GhostInTheShell.Core.Localization;
using GhostInTheShell.Core.Models;
using SukiUI;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace GhostInTheShell.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private static readonly TimeSpan DiskUsageInterval = TimeSpan.FromSeconds(30);

    private readonly IVmProvider _provider;
    private readonly Catalog _catalog;
    private readonly SettingsService _settings;
    private readonly DialogService _dialogs;
    private readonly DispatcherTimer _refreshDebounce;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DateTimeOffset _lastDiskUsage = DateTimeOffset.MinValue;
    private CancellationToken _appLifetime;

    public MainWindowViewModel(IVmProvider provider, Catalog catalog, SettingsService settings, DialogService dialogs,
        ISukiDialogManager dialogManager, ISukiToastManager toastManager)
    {
        _provider = provider;
        _catalog = catalog;
        _settings = settings;
        _dialogs = dialogs;
        DialogManager = dialogManager;
        ToastManager = toastManager;

        // podman events fire several times per state change; coalesce them into one refresh.
        _refreshDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _refreshDebounce.Tick += async (_, _) =>
        {
            _refreshDebounce.Stop();
            await RefreshAsync();
        };
        _provider.MachinesChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            _refreshDebounce.Stop();
            _refreshDebounce.Start();
        });

        var usageTimer = new DispatcherTimer { Interval = DiskUsageInterval };
        usageTimer.Tick += async (_, _) => await RefreshAsync(includeDiskUsage: true);
        usageTimer.Start();

        Strings.LanguageChanged += async (_, _) =>
        {
            foreach (var card in Machines) card.RefreshTexts();
            // Provider messages are produced in the language that was active when they were made.
            await CheckHealthAsync();
        };
    }

    public ISukiDialogManager DialogManager { get; }

    public ISukiToastManager ToastManager { get; }

    public ObservableCollection<MachineViewModel> Machines { get; } = [];

    [ObservableProperty] private string? _backendError;
    [ObservableProperty] private string? _backendWarning;
    [ObservableProperty] private string _backendText = "";
    [ObservableProperty] private bool _isLoading = true;

    public bool IsEmpty => !IsLoading && Machines.Count == 0 && BackendError is null;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    partial void OnBackendErrorChanged(string? value) => OnPropertyChanged(nameof(IsEmpty));

    public async Task InitializeAsync(CancellationToken appLifetime)
    {
        _appLifetime = appLifetime;
        if (!await CheckHealthAsync())
        {
            IsLoading = false;
            return;
        }

        await RefreshAsync(includeDiskUsage: true);
        _provider.StartWatching(appLifetime);
    }

    private async Task<bool> CheckHealthAsync()
    {
        var health = await _provider.CheckAsync(_appLifetime);
        BackendText = health.Version is null ? _provider.DisplayName : $"{_provider.DisplayName} {health.Version}";
        BackendError = health.IsAvailable ? null : string.Join('\n', health.Warnings);
        BackendWarning = health.IsAvailable && health.Warnings.Count > 0 ? string.Join('\n', health.Warnings) : null;
        return health.IsAvailable;
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync(includeDiskUsage: true);

    private async Task RefreshAsync(bool includeDiskUsage = false)
    {
        if (BackendError is not null) return;
        // Skip overlapping refreshes instead of queueing them; the next event triggers another anyway.
        if (!await _refreshLock.WaitAsync(0)) return;
        try
        {
            // Disk usage is the slow part; piggy-back it on normal refreshes only when it is due.
            includeDiskUsage |= DateTimeOffset.Now - _lastDiskUsage > DiskUsageInterval;
            var machines = await _provider.ListAsync(includeDiskUsage);
            if (includeDiskUsage) _lastDiskUsage = DateTimeOffset.Now;
            Merge(machines, includeDiskUsage);
        }
        catch (Exception ex)
        {
            ShowError(Strings.Get("ListFailed"), ex);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            _refreshLock.Release();
        }
    }

    /// <summary>Updates cards in place so that busy flags, logs and scroll position survive a refresh.</summary>
    private void Merge(IReadOnlyList<VmInfo> machines, bool hasDiskUsage)
    {
        var byName = machines.ToDictionary(m => m.Name);
        for (var i = Machines.Count - 1; i >= 0; i--)
        {
            var card = Machines[i];
            if (card.State == VmState.Creating) continue;
            if (!byName.ContainsKey(card.Name)) Machines.RemoveAt(i);
        }

        foreach (var info in machines)
        {
            var existing = Machines.FirstOrDefault(m => m.Name == info.Name);
            // Keep the last known usage when this refresh did not measure it.
            var merged = hasDiskUsage || existing is null ? info : info with { DiskUsedBytes = existing.Info.DiskUsedBytes };
            if (existing is null) Insert(new MachineViewModel(merged, _catalog));
            else if (existing.State != VmState.Creating) existing.Info = merged;
        }
    }

    private void Insert(MachineViewModel card)
    {
        var index = 0;
        while (index < Machines.Count && string.CompareOrdinal(Machines[index].Name, card.Name) < 0) index++;
        Machines.Insert(index, card);
    }

    [RelayCommand]
    private async Task NewMachine()
    {
        var names = Machines.Select(m => m.Name).ToHashSet();
        var request = await _dialogs.ShowAsync<NewMachineRequest?>(Strings.Get("NewMachine"), null,
            complete => new CreateMachineViewModel(_catalog, names, complete));
        if (request is null) return;
        var spec = request.Spec;

        var cts = new CancellationTokenSource();
        var card = MachineViewModel.Pending(spec, _catalog, cts);
        card.IsLogVisible = true;
        Insert(card);
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            await _provider.CreateAsync(spec, request.Credentials, new Progress<string>(card.AppendLog), cts.Token);
            Machines.Remove(card);
            ShowToast(NotificationType.Success, Strings.Get("MachineCreated"), Strings.Format("MachineRunningFormat", spec.Name));
        }
        catch (OperationCanceledException)
        {
            Machines.Remove(card);
            ShowToast(NotificationType.Information, Strings.Get("CreateCancelled"), spec.Name);
        }
        catch (Exception ex)
        {
            // Keep the failed card with its log so the user can see what went wrong.
            card.Error = ex.Message;
            card.AppendLog($"✗ {ex.Message}");
            ShowError(Strings.Format("CreateFailedFormat", spec.Name), ex);
        }
        finally
        {
            cts.Dispose();
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private static void CancelCreate(MachineViewModel card)
    {
        try { card.CreationCts?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    [RelayCommand]
    private void Dismiss(MachineViewModel card)
    {
        Machines.Remove(card);
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private static void ToggleLog(MachineViewModel card) => card.IsLogVisible = !card.IsLogVisible;

    [RelayCommand]
    private Task Start(MachineViewModel card) => RunBusy(card, "StartFailedFormat", ct => _provider.StartAsync(card.Id, ct));

    [RelayCommand]
    private Task Stop(MachineViewModel card) => RunBusy(card, "StopFailedFormat", ct => _provider.StopAsync(card.Id, ct));

    [RelayCommand]
    private async Task Delete(MachineViewModel card)
    {
        var confirmed = await _dialogs.ConfirmAsync(
            Strings.Get("DeleteMachine"),
            Strings.Format("DeleteConfirmFormat", card.Name),
            Strings.Get("Delete"));
        if (!confirmed) return;

        await RunBusy(card, "DeleteFailedFormat", ct => _provider.DeleteAsync(card.Id, ct));
    }

    [RelayCommand]
    private void OpenTerminal(MachineViewModel card)
    {
        try
        {
            TerminalLauncher.Launch(TerminalTemplate, _provider.GetShellCommand(card.Info));
        }
        catch (Exception ex)
        {
            ShowError(Strings.Get("TerminalFailed"), ex);
        }
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        var result = await _dialogs.ShowAsync<SettingsResult?>(Strings.Get("Settings"), null,
            complete => new SettingsViewModel(TerminalTemplate, Strings.Culture.Name, complete));
        if (result is null) return;

        _settings.Save(_settings.Current with { TerminalTemplate = result.TerminalTemplate, Language = result.Language });
        Strings.SetLanguage(result.Language);
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var dark = SukiTheme.GetInstance().ActiveBaseTheme != ThemeVariant.Dark;
        SukiTheme.GetInstance().ChangeBaseTheme(dark ? ThemeVariant.Dark : ThemeVariant.Light);
        _settings.Save(_settings.Current with { DarkTheme = dark });
    }

    private string TerminalTemplate => _settings.Current.TerminalTemplate ?? TerminalLauncher.DetectDefaultTemplate();

    private async Task RunBusy(MachineViewModel card, string failedFormatKey, Func<CancellationToken, Task> operation)
    {
        card.IsBusy = true;
        try
        {
            await operation(CancellationToken.None);
        }
        catch (Exception ex)
        {
            ShowError(Strings.Format(failedFormatKey, card.Name), ex);
        }
        finally
        {
            card.IsBusy = false;
            await RefreshAsync();
        }
    }

    private void ShowError(string title, Exception ex) => ShowToast(NotificationType.Error, title, ex.Message);

    private void ShowToast(NotificationType type, string title, string message) =>
        ToastManager.CreateToast()
            .WithTitle(title)
            .WithContent(message)
            .OfType(type)
            .Dismiss().After(TimeSpan.FromSeconds(type == NotificationType.Error ? 10 : 4))
            .Dismiss().ByClicking()
            .Queue();
}
