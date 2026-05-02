using System;
using Windows.Storage;

namespace SharpTimer.App.Services;

public sealed class AppSettingsService
{
    private const string UseInspectionKey = "UseInspection";
    private const string DecimalPlacesKey = "DecimalPlaces";
    private const string ThemeKey = "Theme";
    private const string BackdropMaterialKey = "BackdropMaterial";
    private const string LanguageKey = "Language";
    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public AppSettings Load()
    {
        return new AppSettings
        {
            UseInspection = ReadBoolean(UseInspectionKey, true),
            DecimalPlaces = Math.Clamp(ReadInt32(DecimalPlacesKey, 2), 2, 3),
            Theme = ReadTheme(),
            BackdropMaterial = ReadBackdropMaterial(),
            Language = ReadLanguage()
        };
    }

    public void Save(AppSettings settings)
    {
        _localSettings.Values[UseInspectionKey] = settings.UseInspection;
        _localSettings.Values[DecimalPlacesKey] = Math.Clamp(settings.DecimalPlaces, 2, 3);
        _localSettings.Values[ThemeKey] = settings.Theme.ToString();
        _localSettings.Values[BackdropMaterialKey] = settings.BackdropMaterial.ToString();
        _localSettings.Values[LanguageKey] = settings.Language.ToString();
    }

    private bool ReadBoolean(string key, bool fallback)
    {
        return _localSettings.Values.TryGetValue(key, out var value) && value is bool boolean
            ? boolean
            : fallback;
    }

    private int ReadInt32(string key, int fallback)
    {
        return _localSettings.Values.TryGetValue(key, out var value) && value is int integer
            ? integer
            : fallback;
    }

    private AppThemePreference ReadTheme()
    {
        if (!_localSettings.Values.TryGetValue(ThemeKey, out var value) || value is not string text)
        {
            return AppThemePreference.Light;
        }

        return Enum.TryParse<AppThemePreference>(text, out var theme)
            ? theme
            : AppThemePreference.Light;
    }

    private AppLanguagePreference ReadLanguage()
    {
        if (!_localSettings.Values.TryGetValue(LanguageKey, out var value) || value is not string text)
        {
            return AppLanguagePreference.English;
        }

        return Enum.TryParse<AppLanguagePreference>(text, out var language)
            ? language
            : AppLanguagePreference.English;
    }

    private AppBackdropMaterialPreference ReadBackdropMaterial()
    {
        if (!_localSettings.Values.TryGetValue(BackdropMaterialKey, out var value) || value is not string text)
        {
            return AppBackdropMaterialPreference.Mica;
        }

        return Enum.TryParse<AppBackdropMaterialPreference>(text, out var material)
            ? material
            : AppBackdropMaterialPreference.Mica;
    }
}
