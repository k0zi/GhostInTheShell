using Avalonia.Controls;
using SukiUI.Controls;

namespace GhostInTheShell.App.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow() => InitializeComponent();

    // Follow the build output like a terminal would.
    private void Log_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) box.CaretIndex = box.Text?.Length ?? 0;
    }
}
