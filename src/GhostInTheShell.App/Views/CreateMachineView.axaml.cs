using Avalonia;
using Avalonia.Controls;

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

    private void TopLevel_SizeChanged(object? sender, SizeChangedEventArgs e) => FitToWindow();

    // The dialog host measures its content with unlimited height, so the tabs need an explicit cap to scroll.
    private void FitToWindow()
    {
        if (_topLevel is not null)
            Tabs.MaxHeight = Math.Max(160, _topLevel.ClientSize.Height - DialogChrome);
    }
}
