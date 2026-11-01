using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GhostInTheShell.App.ViewModels;
using KD.Avalonia.Rice.Controls;

namespace GhostInTheShell.App.Views;

public partial class MainWindow : RiceWindow
{
    public MainWindow() => InitializeComponent();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel { Dialogs.Current: { } dialog })
        {
            dialog.Dismiss();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void DialogBackground_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { Dialogs.Current: { } dialog }) dialog.Dismiss();
    }

    // Follow the build output like a terminal would.
    private void Log_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) box.CaretIndex = box.Text?.Length ?? 0;
    }

    private void Card_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Rapid clicks on the action buttons or selecting text in the log are not meant as "open".
        if (e.Source is Visual source
            && (source.FindAncestorOfType<Button>(includeSelf: true) is not null
                || source.FindAncestorOfType<TextBox>(includeSelf: true) is not null))
            return;

        if (sender is Control { DataContext: MachineViewModel { CanOpenTerminal: true } card }
            && DataContext is MainWindowViewModel vm)
        {
            vm.OpenTerminalCommand.Execute(card);
            e.Handled = true;
        }
    }
}
