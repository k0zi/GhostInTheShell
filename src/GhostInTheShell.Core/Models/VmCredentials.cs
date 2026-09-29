using GhostInTheShell.Core.Localization;

namespace GhostInTheShell.Core.Models;

/// <summary>
/// Passwords set when a machine is created. Kept out of <see cref="VmSpec"/> because the spec is stored
/// in container labels; these are never stored anywhere by the app.
/// </summary>
/// <param name="UserPassword">The login user's password, used by sudo. Null or empty: sudo without a password.</param>
/// <param name="AdminPassword">The root password. Null or empty: root login stays disabled.</param>
public sealed record VmCredentials(string? UserPassword, string? AdminPassword)
{
    public static VmCredentials None { get; } = new(null, null);

    public bool HasUserPassword => !string.IsNullOrEmpty(UserPassword);

    public bool HasAdminPassword => !string.IsNullOrEmpty(AdminPassword);

    /// <summary>Returns the first problem, or null. chpasswd reads one "user:password" per line.</summary>
    public string? Validate() =>
        HasLineBreak(UserPassword) || HasLineBreak(AdminPassword) ? Strings.Get("PasswordLineBreak") : null;

    // Records print every property; never let a password reach a log or an exception message.
    public override string ToString() =>
        $"VmCredentials {{ UserPassword = {(HasUserPassword ? "***" : "none")}, AdminPassword = {(HasAdminPassword ? "***" : "none")} }}";

    private static bool HasLineBreak(string? text) => text is not null && text.AsSpan().IndexOfAny('\r', '\n') >= 0;
}
