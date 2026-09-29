using System.Globalization;
using System.Resources;

namespace GhostInTheShell.Core.Localization;

public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// User-facing text. English is the neutral resource; other languages are satellite resx files.
/// The language is held here rather than in the thread culture so async continuations cannot drift.
/// </summary>
public static class Strings
{
    public const string DefaultLanguage = "en";

    private static readonly ResourceManager Resources = new(typeof(Strings).FullName!, typeof(Strings).Assembly);

    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en", "English"),
        new("hu", "Magyar"),
    ];

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(DefaultLanguage);

    public static event EventHandler? LanguageChanged;

    /// <summary>Unknown or missing codes fall back to English.</summary>
    public static void SetLanguage(string? code)
    {
        var language = Languages.FirstOrDefault(l => l.Code == code)?.Code ?? DefaultLanguage;
        if (Culture.Name == language) return;
        Culture = CultureInfo.GetCultureInfo(language);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key) => Resources.GetString(key, Culture) ?? key;

    public static string Format(string key, params object?[] args) => string.Format(Culture, Get(key), args);
}
