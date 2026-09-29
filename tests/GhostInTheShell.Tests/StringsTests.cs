using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using GhostInTheShell.Core.Localization;

namespace GhostInTheShell.Tests;

public partial class StringsTests
{
    private static readonly ResourceManager Resources = new(typeof(Strings).FullName!, typeof(Strings).Assembly);

    public static TheoryData<string> TranslatedLanguages() =>
        new(Strings.Languages.Select(l => l.Code).Where(c => c != Strings.DefaultLanguage));

    [Theory]
    [MemberData(nameof(TranslatedLanguages))]
    public void Every_language_translates_every_key_with_the_same_placeholders(string code)
    {
        var english = Load(CultureInfo.InvariantCulture);
        var translated = Load(CultureInfo.GetCultureInfo(code));

        Assert.Equal(english.Keys.Order(), translated.Keys.Order());
        foreach (var (key, text) in english)
            Assert.True(Placeholders(text).SetEquals(Placeholders(translated[key])), $"{code}: {key} placeholders differ");
    }

    [Fact]
    public void Defaults_to_english_and_switches_language()
    {
        try
        {
            Strings.SetLanguage(null);
            Assert.Equal("Machines", Strings.Get("Machines"));

            var raised = 0;
            void OnChanged(object? s, EventArgs e) => raised++;
            Strings.LanguageChanged += OnChanged;
            Strings.SetLanguage("hu");
            Strings.LanguageChanged -= OnChanged;

            Assert.Equal(1, raised);
            Assert.Equal("Gépek", Strings.Get("Machines"));
            Assert.Equal("ghost-1 fut.", Strings.Format("MachineRunningFormat", "ghost-1"));
        }
        finally
        {
            Strings.SetLanguage(Strings.DefaultLanguage);
        }
    }

    [Fact]
    public void Unknown_language_falls_back_to_english()
    {
        Strings.SetLanguage("xx");
        Assert.Equal("en", Strings.Culture.Name);
    }

    private static Dictionary<string, string> Load(CultureInfo culture) =>
        Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!);

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    [GeneratedRegex(@"\{(\d+)[^}]*\}")]
    private static partial Regex PlaceholderPattern();
}
