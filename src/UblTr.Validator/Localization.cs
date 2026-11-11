namespace UblTr.Validator;

/// <summary>Output language. Turkish is the default: the people who fix e-Fatura errors usually read Turkish.</summary>
public enum Language
{
    Tr,
    En,
}

/// <summary>A text in both supported languages. Every rule must provide both (enforced by a test).</summary>
public sealed record LocalizedText(string Tr, string En)
{
    public string In(Language language) => language == Language.En ? En : Tr;

    public static LocalizedText Same(string text) => new(text, text);
}

public static class LanguageParser
{
    /// <summary>Parses a language name; anything unrecognised falls back to Turkish.</summary>
    public static Language Parse(string? value) => TryParse(value, out var language) ? language : Language.Tr;

    public static bool TryParse(string? value, out Language language)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "tr" or "tr-tr" or "turkish":
                language = Language.Tr;
                return true;
            case "en" or "en-us" or "en-gb" or "english":
                language = Language.En;
                return true;
            default:
                language = Language.Tr;
                return false;
        }
    }
}
