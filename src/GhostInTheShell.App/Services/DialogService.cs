using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using GhostInTheShell.Core.Localization;

namespace GhostInTheShell.App.Services;

/// <summary>The dialog on screen; <see cref="Dismiss"/> closes it with its dismissed result.</summary>
public sealed record DialogRequest(string Title, object Content, Action Dismiss);

/// <summary>Awaitable dialogs, shown by the dialog layer of the main window.</summary>
public sealed partial class DialogService : ObservableObject
{
    [ObservableProperty] private DialogRequest? _current;

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
            var ok = new Button { Content = confirmText, Classes = { "danger" } };
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
        DialogRequest? request = null;

        void Complete(T result)
        {
            if (!tcs.TrySetResult(result)) return;
            if (Current == request) Current = null;
        }

        // One dialog at a time: a new one dismisses the previous.
        Current?.Dismiss();
        request = new DialogRequest(title, content(Complete), () => Complete(dismissedResult));
        Current = request;
        return tcs.Task;
    }
}
