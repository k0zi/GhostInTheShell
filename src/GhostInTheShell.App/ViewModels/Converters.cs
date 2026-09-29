using Avalonia.Data.Converters;
using GhostInTheShell.Core.Models;

namespace GhostInTheShell.App.ViewModels;

public static class Converters
{
    /// <summary>True for cards that came from a create (running or failed), which are the ones with a build log.</summary>
    public static readonly IValueConverter IsCreatingState =
        new FuncValueConverter<VmState, bool>(state => state == VmState.Creating);
}
