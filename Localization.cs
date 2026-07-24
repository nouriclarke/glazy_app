using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Resources;

namespace ASTEM_DB;

public static class Localization
{
    private const string ResourceBaseName = "ASTEM_DB.Resources.Languages.LangResource";
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ASTEM_DB",
        "language.txt"
    );

    public static ResourceManager Resources { get; } =
        new(ResourceBaseName, typeof(Localization).Assembly);

    public static CultureInfo CurrentCulture { get; private set; } =
        CultureInfo.GetCultureInfo("en");

    public static string CurrentLanguageCode =>
        CurrentCulture.TwoLetterISOLanguageName == "ja" ? "ja" : "en";

    public static event EventHandler? CultureChanged;

    public static void Initialize()
    {
        var preferredLanguage = ReadPreferredLanguage();
        if (string.IsNullOrWhiteSpace(preferredLanguage))
        {
            preferredLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }

        SetCulture(preferredLanguage, persist: false);
    }

    public static void SetCulture(string? languageCode, bool persist = true)
    {
        var normalizedCode = languageCode?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true
            ? "ja"
            : "en";
        CurrentCulture = CultureInfo.GetCultureInfo(normalizedCode);
        CultureInfo.CurrentUICulture = CurrentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CurrentCulture;

        ApplyApplicationResources();
        if (persist)
        {
            SavePreferredLanguage(normalizedCode);
        }

        CultureChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        return Resources.GetString(key, CurrentCulture)
            ?? Resources.GetString(key, CultureInfo.GetCultureInfo("en"))
            ?? key;
    }

    public static string Format(string key, params object[] values)
    {
        return string.Format(CurrentCulture, Get(key), values);
    }

    private static void ApplyApplicationResources()
    {
        if (Application.Current?.Resources == null)
        {
            return;
        }

        var resourceSet = Resources.GetResourceSet(
            CultureInfo.GetCultureInfo("en"),
            createIfNotExists: true,
            tryParents: true
        );
        if (resourceSet == null)
        {
            return;
        }

        foreach (DictionaryEntry entry in resourceSet)
        {
            if (entry.Key is string key)
            {
                Application.Current.Resources[key] = Get(key);
            }
        }
    }

    private static string? ReadPreferredLanguage()
    {
        try
        {
            return File.Exists(PreferencePath)
                ? File.ReadAllText(PreferencePath).Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void SavePreferredLanguage(string languageCode)
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(PreferencePath, languageCode);
        }
        catch
        {
            // The language still changes for this session if preferences are read-only.
        }
    }
}

public class L10n : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        var key = parameter?.ToString();
        return string.IsNullOrWhiteSpace(key) ? string.Empty : Localization.Get(key);
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        throw new NotImplementedException();
    }
}
