using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpTimer.App.Services;
using SharpTimer.Core.SmartCubes;
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
            SetSelectedIndex(ScrambleProgressStyleComboBox, settings.SmartCubeScrambleProgressStyle == SmartCubeScrambleProgressStyle.DimCompleted ? 1 : 0);
            SetSelectedIndex(SolveMethodComboBox, settings.SmartCubeSolveMethod == SmartCubeSolveMethod.Roux ? 1 : 0);
            ScrambleFontSizeSlider.Value = settings.ScrambleFontSize;
            SmartCubePreviewSizeSlider.Value = settings.SmartCubePreviewSize;
            UpdateSliderValueText();
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    public void ApplyLanguage(LocalizedStrings strings)
    {
        SettingsTitleText.Text = strings.SettingsTitle;
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
        SmartCubeSectionTitleText.Text = strings.SmartCubeSectionTitle;
        ScrambleProgressStyleComboBox.Header = strings.ScrambleProgressStyleHeader;
        ScrambleProgressHideCompletedItem.Content = strings.ScrambleProgressHideCompleted;
        ScrambleProgressDimCompletedItem.Content = strings.ScrambleProgressDimCompleted;
        SolveMethodComboBox.Header = strings.SolveMethodHeader;
        SolveMethodCfopItem.Content = strings.SolveMethodCfop;
        SolveMethodRouxItem.Content = strings.SolveMethodRoux;
        ScrambleFontSizeHeaderText.Text = strings.ScrambleFontSizeHeader;
        SmartCubePreviewSizeHeaderText.Text = strings.SmartCubePreviewSizeHeader;
        UpdateSliderValueText();
    }

    private void SettingControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingSettings || ScrambleFontSizeSlider is null || SmartCubePreviewSizeSlider is null)
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
                : AppLanguagePreference.Chinese,
            SmartCubeScrambleProgressStyle = ScrambleProgressStyleComboBox.SelectedIndex == 1
                ? SmartCubeScrambleProgressStyle.DimCompleted
                : SmartCubeScrambleProgressStyle.HideCompleted,
            SmartCubeSolveMethod = SolveMethodComboBox.SelectedIndex == 1
                ? SmartCubeSolveMethod.Roux
                : SmartCubeSolveMethod.Cfop,
            ScrambleFontSize = (int)Math.Round(ScrambleFontSizeSlider.Value),
            SmartCubePreviewSize = (int)Math.Round(SmartCubePreviewSizeSlider.Value)
        });

        UpdateSliderValueText();
    }

    private static void SetSelectedIndex(ComboBox comboBox, int selectedIndex)
    {
        if (comboBox.SelectedIndex != selectedIndex)
        {
            comboBox.SelectedIndex = selectedIndex;
        }
    }

    private void UpdateSliderValueText()
    {
        if (ScrambleFontSizeValueText is null || SmartCubePreviewSizeValueText is null)
        {
            return;
        }

        ScrambleFontSizeValueText.Text = $"{Math.Round(ScrambleFontSizeSlider.Value)} px";
        SmartCubePreviewSizeValueText.Text = $"{Math.Round(SmartCubePreviewSizeSlider.Value)} px";
    }
}
