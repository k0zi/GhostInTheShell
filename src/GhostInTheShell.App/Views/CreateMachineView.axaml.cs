using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using GhostInTheShell.App.ViewModels;

namespace GhostInTheShell.App.Views;

public partial class CreateMachineView : UserControl
{
    // Dialog title, padding and the button row, plus a margin to the window edge.
    private const double DialogChrome = 230;

    private TopLevel? _topLevel;

    public CreateMachineView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null) return;
        _topLevel.SizeChanged += TopLevel_SizeChanged;
        FitToWindow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null) _topLevel.SizeChanged -= TopLevel_SizeChanged;
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    // The picker needs the window, which the view model does not know about.
    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        if (_topLevel is null || DataContext is not CreateMachineViewModel vm) return;

        var start = CreateMachineViewModel.NormalizeHostFolder(vm.HostFolder) is { } current && Directory.Exists(current)
            ? await _topLevel.StorageProvider.TryGetFolderFromPathAsync(current)
            : null;
        var folders = await _topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Core.Localization.Strings.Get("HostFolder"),
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path) vm.HostFolder = path;
    }

    private void TopLevel_SizeChanged(object? sender, SizeChangedEventArgs e) => FitToWindow();

    // The dialog host measures its content with unlimited height, so the tabs need an explicit cap to scroll.
    private void FitToWindow()
    {
        if (_topLevel is not null)
            Tabs.MaxHeight = Math.Max(160, _topLevel.ClientSize.Height - DialogChrome);
    }
}
