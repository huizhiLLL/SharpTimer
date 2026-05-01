using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SharpTimer.App.Services;
using SharpTimer.App.ViewModels;
using SharpTimer.Core.Models;
using SharpTimer.Core.Statistics;
using SharpTimer.Core.Timer;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Storage;

namespace SharpTimer.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly ObservableCollection<SolveListItem> _solveItems = new();
        private readonly ObservableCollection<SessionListItem> _sessionItems = new();
        private readonly DispatcherTimer _uiTimer = new();
        private readonly AppSettingsService _settingsService = new();
        private TimerAppService? _appService;
        private TimerAppSnapshot? _lastSnapshot;
        private AppSettings _settings = new();
        private LocalizedStrings _strings = LocalizedStrings.For(AppLanguagePreference.Chinese);
        private bool _isRendering;
        private bool _isApplyingSettings;
        private bool _isSpaceDown;
        private bool _isReadyToStart;
        private double _currentTimerScale = 1;

        public MainWindow()
        {
            InitializeComponent();

            SolvesList.ItemsSource = _solveItems;
            SessionComboBox.ItemsSource = _sessionItems;
            RootGrid.Loaded += RootGrid_Loaded;

            _uiTimer.Interval = TimeSpan.FromMilliseconds(33);
            _uiTimer.Tick += UiTimer_Tick;
        }

        private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            RootGrid.Focus(FocusState.Programmatic);

            var databasePath = System.IO.Path.Combine(
                ApplicationData.Current.LocalFolder.Path,
                "sharptimer.db");
            _settings = _settingsService.Load();
            _strings = LocalizedStrings.For(_settings.Language);
            ApplyLanguage();
            ApplyTheme(_settings.Theme);
            _appService = new TimerAppService(databasePath, _settings);

            var snapshot = await _appService.InitializeAsync();
            RootGrid.SelectedItem = TimerNavItem;
            ShowPage(TimerPage);
            Render(snapshot);
            RenderSettings();
            _uiTimer.Start();
        }

        private void UiTimer_Tick(object? sender, object e)
        {
            if (_appService is null)
            {
                return;
            }

            Render(_appService.Tick(), refreshList: false);
        }

        private async void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right)
            {
                e.Handled = true;
                SwitchScramble(e.Key);
                return;
            }

            if (e.Key != Windows.System.VirtualKey.Space)
            {
                return;
            }

            e.Handled = true;
            if (_appService is null || _lastSnapshot is null || _isSpaceDown)
            {
                return;
            }

            _isSpaceDown = true;
            if (StartsOnKeyUp(_lastSnapshot.Timer.Phase))
            {
                _isReadyToStart = true;
                Render(_lastSnapshot, refreshList: false);
                return;
            }

            await RunPrimaryTimerActionAsync();
        }

        private async void RootGrid_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Space)
            {
                return;
            }

            e.Handled = true;
            if (!_isSpaceDown)
            {
                return;
            }

            _isSpaceDown = false;
            if (!_isReadyToStart)
            {
                return;
            }

            _isReadyToStart = false;
            await RunPrimaryTimerActionAsync();
        }

        private void AppNavigationView_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            if (ReferenceEquals(args.SelectedItem, TimerNavItem))
            {
                ShowPage(TimerPage);
            }
            else if (ReferenceEquals(args.SelectedItem, SolvesNavItem))
            {
                ShowPage(SolvesPage);
            }
            else if (args.IsSettingsSelected)
            {
                ShowPage(SettingsPage);
            }

            RootGrid.Focus(FocusState.Programmatic);
        }

        private async void SessionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRendering || _appService is null || SessionComboBox.SelectedItem is not SessionListItem item)
            {
                return;
            }

            Render(await _appService.SwitchSessionAsync(item.Id));
            RootGrid.Focus(FocusState.Programmatic);
        }

        private async void NewSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appService is null)
            {
                return;
            }

            var name = await ShowSessionNameDialogAsync(_strings.NewSessionDialogTitle, _strings.NewSessionDefaultName);
            if (name is null)
            {
                return;
            }

            Render(await _appService.CreateSessionAsync(name));
            RootGrid.Focus(FocusState.Programmatic);
        }

        private async void RenameSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appService is null || SessionComboBox.SelectedItem is not SessionListItem item)
            {
                return;
            }

            var name = await ShowSessionNameDialogAsync(_strings.RenameSessionDialogTitle, item.Name);
            if (name is null)
            {
                return;
            }

            Render(await _appService.RenameCurrentSessionAsync(name));
            RootGrid.Focus(FocusState.Programmatic);
        }

        private async void ArchiveSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appService is null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = _strings.ArchiveSessionDialogTitle,
                Content = _strings.ArchiveSessionDialogContent,
                PrimaryButtonText = _strings.Archive,
                CloseButtonText = _strings.Cancel,
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            Render(await _appService.ArchiveCurrentSessionAsync());
            RootGrid.Focus(FocusState.Programmatic);
        }

        private async void PlusTwoButton_Click(object sender, RoutedEventArgs e)
        {
            await SetSelectedPenaltyAsync(Penalty.PlusTwo);
        }

        private async void DnfButton_Click(object sender, RoutedEventArgs e)
        {
            await SetSelectedPenaltyAsync(Penalty.Dnf);
        }

        private async void ClearPenaltyButton_Click(object sender, RoutedEventArgs e)
        {
            await SetSelectedPenaltyAsync(Penalty.None);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appService is null || SolvesList.SelectedItem is not SolveListItem item)
            {
                return;
            }

            Render(await _appService.DeleteSolveAsync(item.Id));
            RootGrid.Focus(FocusState.Programmatic);
        }

        private void InspectionSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private void PrecisionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private async System.Threading.Tasks.Task RunPrimaryTimerActionAsync()
        {
            if (_appService is null)
            {
                return;
            }

            Render(await _appService.HandlePrimaryTimerActionAsync());
            RootGrid.Focus(FocusState.Programmatic);
        }

        private async System.Threading.Tasks.Task SetSelectedPenaltyAsync(Penalty penalty)
        {
            if (_appService is null || SolvesList.SelectedItem is not SolveListItem item)
            {
                return;
            }

            Render(await _appService.SetPenaltyAsync(item.Id, penalty));
            RootGrid.Focus(FocusState.Programmatic);
        }

        private void Render(TimerAppSnapshot snapshot, bool refreshList = true)
        {
            _lastSnapshot = snapshot;
            _isRendering = true;
            RenderSessions(snapshot);
            ScrambleText.Text = snapshot.CurrentScramble;
            TimerText.Text = FormatTime(snapshot.Timer.Elapsed, _settings.DecimalPlaces);
            InspectionText.Text = FormatInspection(snapshot.Timer);
            ApplyTimerVisualState(snapshot.Timer);
            ApplyImmersiveTimerLayout(snapshot.Timer);

            Ao5Text.Text = FormatNullableTime(snapshot.Statistics.AverageOf5, _settings.DecimalPlaces);
            Ao12Text.Text = FormatNullableTime(snapshot.Statistics.AverageOf12, _settings.DecimalPlaces);
            CountText.Text = string.Format(_strings.CountFormat, snapshot.Statistics.Count);

            if (refreshList)
            {
                RenderSolves(snapshot);
            }

            _isRendering = false;
        }

        private void RenderSessions(TimerAppSnapshot snapshot)
        {
            var currentId = snapshot.CurrentSession.Id;
            var existingIds = _sessionItems.Select(item => item.Id).ToArray();
            var nextIds = snapshot.Sessions.Select(session => session.Id).ToArray();

            if (!existingIds.SequenceEqual(nextIds))
            {
                _sessionItems.Clear();
                foreach (var session in snapshot.Sessions)
                {
                    _sessionItems.Add(new SessionListItem
                    {
                        Id = session.Id,
                        Name = session.Name,
                        Puzzle = session.Puzzle
                    });
                }
            }
            else
            {
                for (var index = 0; index < snapshot.Sessions.Count; index++)
                {
                    var session = snapshot.Sessions[index];
                    if (_sessionItems[index].Name != session.Name || _sessionItems[index].Puzzle != session.Puzzle)
                    {
                        _sessionItems[index] = new SessionListItem
                        {
                            Id = session.Id,
                            Name = session.Name,
                            Puzzle = session.Puzzle
                        };
                    }
                }
            }

            SessionComboBox.SelectedItem = _sessionItems.FirstOrDefault(item => item.Id == currentId);
        }

        private void RenderSolves(TimerAppSnapshot snapshot)
        {
            var selectedId = (SolvesList.SelectedItem as SolveListItem)?.Id;
            var orderedSolves = snapshot.Solves
                .OrderBy(solve => solve.CreatedAt)
                .ToArray();
            var items = orderedSolves
                .Select((solve, index) => new SolveListItem
                {
                    Id = solve.Id,
                    Number = (index + 1).ToString(),
                    Time = FormatSolveTime(solve, _settings.DecimalPlaces),
                    Penalty = FormatPenalty(solve.Penalty),
                    AverageOf5 = FormatNullableTime(
                        StatisticsCalculator.CalculateAverageOf(orderedSolves.Take(index + 1), 5),
                        _settings.DecimalPlaces),
                    AverageOf12 = FormatNullableTime(
                        StatisticsCalculator.CalculateAverageOf(orderedSolves.Take(index + 1), 12),
                        _settings.DecimalPlaces),
                    Solve = solve
                })
                .Reverse()
                .ToArray();

            _solveItems.Clear();
            foreach (var item in items)
            {
                _solveItems.Add(item);
            }

            SolvesList.SelectedItem = _solveItems.FirstOrDefault(item => item.Id == selectedId)
                ?? _solveItems.FirstOrDefault();
        }

        private string FormatInspection(TimerSnapshot snapshot)
        {
            return snapshot.Phase == TimerPhase.Inspecting
                ? string.Format(_strings.InspectionRemainingFormat, Math.Ceiling(snapshot.InspectionRemaining.TotalSeconds))
                : string.Empty;
        }

        private void ApplyTimerVisualState(TimerSnapshot snapshot)
        {
            var targetScale = _isReadyToStart || snapshot.Phase == TimerPhase.Running ? 1.06 : 1;
            if (Math.Abs(_currentTimerScale - targetScale) > 0.001)
            {
                AnimateTimerScale(targetScale);
                _currentTimerScale = targetScale;
            }

            TimerText.Foreground = _isReadyToStart
                ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
                : Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush;
        }

        private void ApplyImmersiveTimerLayout(TimerSnapshot snapshot)
        {
            var isImmersive = _isReadyToStart || snapshot.Phase == TimerPhase.Running;
            var contextVisibility = isImmersive ? Visibility.Collapsed : Visibility.Visible;

            ScrambleText.Visibility = contextVisibility;
            InspectionText.Visibility = contextVisibility;
            StatsPanel.Visibility = contextVisibility;
        }

        private void AnimateTimerScale(double targetScale)
        {
            var storyboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromMilliseconds(140));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var scaleX = new DoubleAnimation
            {
                To = targetScale,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(scaleX, TimerTextScale);
            Storyboard.SetTargetProperty(scaleX, nameof(ScaleTransform.ScaleX));

            var scaleY = new DoubleAnimation
            {
                To = targetScale,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(scaleY, TimerTextScale);
            Storyboard.SetTargetProperty(scaleY, nameof(ScaleTransform.ScaleY));

            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);
            storyboard.Begin();
        }

        private static bool StartsOnKeyUp(TimerPhase phase)
        {
            return phase is TimerPhase.Idle or TimerPhase.Inspecting or TimerPhase.Stopped;
        }

        private void ShowPage(FrameworkElement page)
        {
            var wasVisible = page.Visibility == Visibility.Visible;
            TimerPage.Visibility = ReferenceEquals(page, TimerPage) ? Visibility.Visible : Visibility.Collapsed;
            SolvesPage.Visibility = ReferenceEquals(page, SolvesPage) ? Visibility.Visible : Visibility.Collapsed;
            SettingsPage.Visibility = ReferenceEquals(page, SettingsPage) ? Visibility.Visible : Visibility.Collapsed;
            page.Visibility = Visibility.Visible;

            if (!wasVisible)
            {
                AnimatePageEntrance(page);
            }
        }

        private static void AnimatePageEntrance(FrameworkElement page)
        {
            page.Opacity = 0;
            page.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            page.RenderTransform = new ScaleTransform { ScaleX = 0.985, ScaleY = 0.985 };

            var storyboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromMilliseconds(160));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

            var opacity = new DoubleAnimation
            {
                To = 1,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(opacity, page);
            Storyboard.SetTargetProperty(opacity, nameof(UIElement.Opacity));
            storyboard.Children.Add(opacity);

            if (page.RenderTransform is ScaleTransform scale)
            {
                var scaleX = new DoubleAnimation { To = 1, Duration = duration, EasingFunction = easing };
                Storyboard.SetTarget(scaleX, scale);
                Storyboard.SetTargetProperty(scaleX, nameof(ScaleTransform.ScaleX));
                storyboard.Children.Add(scaleX);

                var scaleY = new DoubleAnimation { To = 1, Duration = duration, EasingFunction = easing };
                Storyboard.SetTarget(scaleY, scale);
                Storyboard.SetTargetProperty(scaleY, nameof(ScaleTransform.ScaleY));
                storyboard.Children.Add(scaleY);
            }

            storyboard.Begin();
        }

        private void SwitchScramble(Windows.System.VirtualKey key)
        {
            if (_appService is null || _lastSnapshot is null || _lastSnapshot.Timer.Phase == TimerPhase.Running)
            {
                return;
            }

            _isReadyToStart = false;
            _isSpaceDown = false;
            var snapshot = key == Windows.System.VirtualKey.Left
                ? _appService.MoveToPreviousScramble()
                : _appService.MoveToNextScramble();
            Render(snapshot, refreshList: false);
            RootGrid.Focus(FocusState.Programmatic);
        }

        private static string FormatSolveTime(Solve solve, int decimalPlaces)
        {
            return solve.Penalty == Penalty.Dnf
                ? "DNF"
                : FormatTime(solve.EffectiveDuration ?? solve.Duration, decimalPlaces);
        }

        private static string FormatNullableTime(TimeSpan? time, int decimalPlaces)
        {
            return time is null ? "--" : FormatTime(time.Value, decimalPlaces);
        }

        private static string FormatTime(TimeSpan time, int decimalPlaces)
        {
            var fraction = decimalPlaces == 3
                ? time.Milliseconds.ToString("000")
                : (time.Milliseconds / 10).ToString("00");

            return time.TotalMinutes >= 1
                ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{fraction}"
                : $"{(int)time.TotalSeconds}.{fraction}";
        }

        private static string FormatPenalty(Penalty penalty)
        {
            return penalty switch
            {
                Penalty.None => "",
                Penalty.PlusTwo => "+2",
                Penalty.Dnf => "DNF",
                _ => ""
            };
        }

        private void RenderSettings()
        {
            _isApplyingSettings = true;
            InspectionSwitch.IsOn = _settings.UseInspection;
            PrecisionComboBox.SelectedIndex = _settings.DecimalPlaces == 3 ? 1 : 0;
            ThemeComboBox.SelectedIndex = _settings.Theme switch
            {
                AppThemePreference.Light => 1,
                AppThemePreference.Dark => 2,
                _ => 0
            };
            LanguageComboBox.SelectedIndex = _settings.Language == AppLanguagePreference.English ? 1 : 0;
            _isApplyingSettings = false;
        }

        private void ApplyLanguage()
        {
            TimerNavItem.Content = _strings.TimerNav;
            SolvesNavItem.Content = _strings.SolvesNav;
            if (RootGrid.SettingsItem is NavigationViewItem settingsItem)
            {
                settingsItem.Content = _strings.SettingsNav;
            }

            SessionComboBox.Header = _strings.SessionHeader;
            NewSessionButton.Content = _strings.NewSession;
            RenameSessionButton.Content = _strings.RenameSession;
            ArchiveSessionButton.Content = _strings.ArchiveSession;
            TimeColumnText.Text = _strings.TimeColumn;
            PenaltyColumnText.Text = _strings.PenaltyColumn;
            ClearPenaltyButton.Content = _strings.ClearPenalty;
            DeleteButton.Content = _strings.Delete;
            SettingsTitleText.Text = _strings.SettingsTitle;
            InspectionSwitch.Header = _strings.InspectionHeader;
            PrecisionComboBox.Header = _strings.PrecisionHeader;
            CentisecondsItem.Content = _strings.Centiseconds;
            MillisecondsItem.Content = _strings.Milliseconds;
            ThemeComboBox.Header = _strings.ThemeHeader;
            SystemThemeItem.Content = _strings.SystemTheme;
            LightThemeItem.Content = _strings.LightTheme;
            DarkThemeItem.Content = _strings.DarkTheme;
            LanguageComboBox.Header = _strings.LanguageHeader;
            ChineseLanguageItem.Content = _strings.ChineseLanguage;
            EnglishLanguageItem.Content = _strings.EnglishLanguage;

            if (_lastSnapshot is not null)
            {
                Render(_lastSnapshot, refreshList: false);
            }
        }

        private void ApplySettingsFromControls()
        {
            if (_isApplyingSettings)
            {
                return;
            }

            _settings = new AppSettings
            {
                UseInspection = InspectionSwitch.IsOn,
                DecimalPlaces = PrecisionComboBox.SelectedIndex == 1 ? 3 : 2,
                Theme = ThemeComboBox.SelectedIndex switch
                {
                    1 => AppThemePreference.Light,
                    2 => AppThemePreference.Dark,
                    _ => AppThemePreference.System
                },
                Language = LanguageComboBox.SelectedIndex == 1
                    ? AppLanguagePreference.English
                    : AppLanguagePreference.Chinese
            };

            _settingsService.Save(_settings);
            _strings = LocalizedStrings.For(_settings.Language);
            ApplyLanguage();
            ApplyTheme(_settings.Theme);

            if (_appService is not null)
            {
                Render(_appService.ApplySettings(_settings));
            }
        }

        private async System.Threading.Tasks.Task<string?> ShowSessionNameDialogAsync(string title, string defaultName)
        {
            var textBox = new TextBox
            {
                Text = defaultName,
                MinWidth = 320,
                PlaceholderText = _strings.SessionNamePlaceholder
            };

            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = title,
                Content = textBox,
                PrimaryButtonText = _strings.Save,
                CloseButtonText = _strings.Cancel,
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            var name = textBox.Text.Trim();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private void ApplyTheme(AppThemePreference theme)
        {
            RootGrid.RequestedTheme = theme switch
            {
                AppThemePreference.Light => ElementTheme.Light,
                AppThemePreference.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }
}
