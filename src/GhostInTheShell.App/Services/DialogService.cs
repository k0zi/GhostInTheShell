using Avalonia.Controls;
using GhostInTheShell.Core.Localization;
using SukiUI.Dialogs;

namespace GhostInTheShell.App.Services;

/// <summary>Awaitable wrappers around SukiUI dialogs.</summary>
public sealed class DialogService(ISukiDialogManager manager)
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmText) =>
        ShowAsync<bool>(title, false, complete =>
        {
            var panel = new StackPanel { Spacing = 16, MaxWidth = 440 };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            var buttons = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            };
            var cancel = new Button { Content = Strings.Get("Cancel") };
            cancel.Click += (_, _) => complete(false);
            var ok = new Button { Content = confirmText, Classes = { "Flat", "Danger" } };
            ok.Click += (_, _) => complete(true);
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            panel.Children.Add(buttons);
            return panel;
        });

    /// <summary>Shows the content built by <paramref name="content"/> until it calls the completion callback it was given.</summary>
    public Task<T> ShowAsync<T>(string title, T dismissedResult, Func<Action<T>, object> content)
    {
        var tcs = new TaskCompletionSource<T>();
        SukiDialogBuilder? builder = null;

        void Complete(T result)
        {
            tcs.TrySetResult(result);
            if (builder is not null) manager.TryDismissDialog(builder.Dialog);
        }

        builder = manager.CreateDialog()
            .WithTitle(title)
            .WithContent(content(Complete))
            .Dismiss().ByClickingBackground()
            .OnDismissed(_ => tcs.TrySetResult(dismissedResult));
        builder.TryShow();
        return tcs.Task;
    }
}
