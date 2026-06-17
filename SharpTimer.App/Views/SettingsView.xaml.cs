using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpTimer.App.Services;
using System;

namespace SharpTimer.App.Views;

public sealed partial class SettingsView : UserControl
{
    private bool _isApplyingSettings;

    public SettingsView()
    {
        InitializeComponent();
    }

    public event EventHandler<AppSettings>? SettingsChanged;

    public void Render(AppSettings settings)
    {
        _isApplyingSettings = true;
        try
        {
            InspectionSwitch.IsOn = settings.UseInspection;
            SetSelectedIndex(PrecisionComboBox, settings.DecimalPlaces == 3 ? 1 : 0);
            SetSelectedIndex(ThemeComboBox, settings.Theme switch
            {
                AppThemePreference.Light => 1,
                AppThemePreference.Dark => 2,
                _ => 0
            });
            SetSelectedIndex(BackdropMaterialComboBox, settings.BackdropMaterial switch
            {
                AppBackdropMaterialPreference.MicaAlt => 1,
                AppBackdropMaterialPreference.Acrylic => 2,
                _ => 0
            });
            SetSelectedIndex(LanguageComboBox, settings.Language == AppLanguagePreference.English ? 1 : 0);
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    public void ApplyLanguage(LocalizedStrings strings)
    {
        SettingsTitleText.Text = strings.SettingsTitle;
        SettingsDescriptionText.Text = strings.SettingsDescription;
        SettingsTimingSectionTitleText.Text = strings.SettingsTimingSectionTitle;
        SettingsTimingSectionDescriptionText.Text = strings.SettingsTimingSectionDescription;
        SettingsAppearanceSectionTitleText.Text = strings.SettingsAppearanceSectionTitle;
        SettingsAppearanceSectionDescriptionText.Text = strings.SettingsAppearanceSectionDescription;
        SettingsLanguageSectionTitleText.Text = strings.SettingsLanguageSectionTitle;
        SettingsLanguageSectionDescriptionText.Text = strings.SettingsLanguageSectionDescription;
        InspectionSwitch.Header = strings.InspectionHeader;
        PrecisionComboBox.Header = strings.PrecisionHeader;
        CentisecondsItem.Content = strings.Centiseconds;
        MillisecondsItem.Content = strings.Milliseconds;
        ThemeComboBox.Header = strings.ThemeHeader;
        SystemThemeItem.Content = strings.SystemTheme;
        LightThemeItem.Content = strings.LightTheme;
        DarkThemeItem.Content = strings.DarkTheme;
        BackdropMaterialComboBox.Header = strings.BackdropMaterialHeader;
        MicaMaterialItem.Content = strings.MicaMaterial;
        MicaAltMaterialItem.Content = strings.MicaAltMaterial;
        AcrylicMaterialItem.Content = strings.AcrylicMaterial;
        LanguageComboBox.Header = strings.LanguageHeader;
        ChineseLanguageItem.Content = strings.ChineseLanguage;
        EnglishLanguageItem.Content = strings.EnglishLanguage;
    }

    private void SettingControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        SettingsChanged?.Invoke(this, new AppSettings
        {
            UseInspection = InspectionSwitch.IsOn,
            DecimalPlaces = PrecisionComboBox.SelectedIndex == 1 ? 3 : 2,
            Theme = ThemeComboBox.SelectedIndex switch
            {
                1 => AppThemePreference.Light,
                2 => AppThemePreference.Dark,
                _ => AppThemePreference.System
            },
            BackdropMaterial = BackdropMaterialComboBox.SelectedIndex switch
            {
                1 => AppBackdropMaterialPreference.MicaAlt,
                2 => AppBackdropMaterialPreference.Acrylic,
                _ => AppBackdropMaterialPreference.Mica
            },
            Language = LanguageComboBox.SelectedIndex == 1
                ? AppLanguagePreference.English
                : AppLanguagePreference.Chinese
        });
    }

    private static void SetSelectedIndex(ComboBox comboBox, int selectedIndex)
    {
        if (comboBox.SelectedIndex != selectedIndex)
        {
            comboBox.SelectedIndex = selectedIndex;
        }
    }
}
