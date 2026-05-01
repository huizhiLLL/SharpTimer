using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SharpTimer.App.Services;
using SharpTimer.App.ViewModels;
using SharpTimer.Core.Models;
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
        private AppSettings _settings = new();
        private bool _isRendering;

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
            ApplyTheme(_settings.Theme);
            _appService = new TimerAppService(databasePath, _settings);

            var snapshot = await _appService.InitializeAsync();
            Render(snapshot);
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
            if (e.Key != Windows.System.VirtualKey.Space)
            {
                return;
            }

            e.Handled = true;
            await RunPrimaryTimerActionAsync();
        }

        private async void PrimaryActionButton_Click(object sender, RoutedEventArgs e)
        {
            await RunPrimaryTimerActionAsync();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appService is null)
            {
                return;
            }

            Render(_appService.ResetTimer());
            RootGrid.Focus(FocusState.Programmatic);
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appService is null)
            {
                return;
            }

            var settings = await ShowSettingsDialogAsync(_settings);
            if (settings is null)
            {
                return;
            }

            _settings = settings;
            _settingsService.Save(_settings);
            ApplyTheme(_settings.Theme);
            Render(_appService.ApplySettings(_settings));
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

            var name = await ShowSessionNameDialogAsync("新建 session", "Session name");
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

            var name = await ShowSessionNameDialogAsync("重命名 session", item.Name);
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
                Title = "归档当前 session",
                Content = "归档后不会出现在 session 列表里，成绩仍保存在本地数据库中。",
                PrimaryButtonText = "归档",
                CloseButtonText = "取消",
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
            _isRendering = true;
            RenderSessions(snapshot);
            PhaseText.Text = FormatPhase(snapshot.Timer);
            TimerText.Text = FormatTime(snapshot.Timer.Elapsed, _settings.DecimalPlaces);
            InspectionText.Text = FormatInspection(snapshot.Timer);
            PrimaryActionButton.Content = FormatPrimaryAction(snapshot.Timer.Phase);

            BestText.Text = FormatNullableTime(snapshot.Statistics.Best, _settings.DecimalPlaces);
            MeanText.Text = FormatNullableTime(snapshot.Statistics.Mean, _settings.DecimalPlaces);
            Ao5Text.Text = FormatNullableTime(snapshot.Statistics.AverageOf5, _settings.DecimalPlaces);
            Ao12Text.Text = FormatNullableTime(snapshot.Statistics.AverageOf12, _settings.DecimalPlaces);
            CountText.Text = $"{snapshot.Statistics.Count} 次";

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
            var items = snapshot.Solves
                .Select((solve, index) => new SolveListItem
                {
                    Id = solve.Id,
                    Number = (index + 1).ToString(),
                    Time = FormatSolveTime(solve, _settings.DecimalPlaces),
                    Penalty = FormatPenalty(solve.Penalty),
                    CreatedAt = solve.CreatedAt.ToLocalTime().ToString("HH:mm:ss"),
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

        private static string FormatPhase(TimerAppSnapshot snapshot)
        {
            return FormatPhase(snapshot.Timer);
        }

        private static string FormatPhase(TimerSnapshot snapshot)
        {
            return snapshot.Phase switch
            {
                TimerPhase.Idle => "就绪",
                TimerPhase.Inspecting => snapshot.PendingPenalty switch
                {
                    Penalty.PlusTwo => "观察超时 +2",
                    Penalty.Dnf => "观察超时 DNF",
                    _ => "观察"
                },
                TimerPhase.Running => "计时中",
                TimerPhase.Stopped => "已停止",
                _ => "未知"
            };
        }

        private static string FormatPrimaryAction(TimerPhase phase)
        {
            return phase switch
            {
                TimerPhase.Idle => "开始",
                TimerPhase.Inspecting => "开跑",
                TimerPhase.Running => "停止",
                TimerPhase.Stopped => "再来",
                _ => "开始"
            };
        }

        private static string FormatInspection(TimerSnapshot snapshot)
        {
            return snapshot.Phase == TimerPhase.Inspecting
                ? $"观察剩余 {Math.Ceiling(snapshot.InspectionRemaining.TotalSeconds):0}s"
                : string.Empty;
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

        private async System.Threading.Tasks.Task<string?> ShowSessionNameDialogAsync(string title, string defaultName)
        {
            var textBox = new TextBox
            {
                Text = defaultName,
                MinWidth = 320,
                PlaceholderText = "例如 Main、OH、练习 A"
            };

            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = title,
                Content = textBox,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
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

        private async System.Threading.Tasks.Task<AppSettings?> ShowSettingsDialogAsync(AppSettings settings)
        {
            var inspectionSwitch = new ToggleSwitch
            {
                Header = "15 秒观察",
                IsOn = settings.UseInspection
            };

            var precisionBox = new ComboBox
            {
                Header = "显示精度",
                MinWidth = 220
            };
            precisionBox.Items.Add("百分秒");
            precisionBox.Items.Add("毫秒");
            precisionBox.SelectedIndex = settings.DecimalPlaces == 3 ? 1 : 0;

            var themeBox = new ComboBox
            {
                Header = "主题",
                MinWidth = 220
            };
            themeBox.Items.Add("跟随系统");
            themeBox.Items.Add("亮色");
            themeBox.Items.Add("暗色");
            themeBox.SelectedIndex = settings.Theme switch
            {
                AppThemePreference.Light => 1,
                AppThemePreference.Dark => 2,
                _ => 0
            };

            var panel = new StackPanel
            {
                Spacing = 16
            };
            panel.Children.Add(inspectionSwitch);
            panel.Children.Add(precisionBox);
            panel.Children.Add(themeBox);

            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "设置",
                Content = panel,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return null;
            }

            return new AppSettings
            {
                UseInspection = inspectionSwitch.IsOn,
                DecimalPlaces = precisionBox.SelectedIndex == 1 ? 3 : 2,
                Theme = themeBox.SelectedIndex switch
                {
                    1 => AppThemePreference.Light,
                    2 => AppThemePreference.Dark,
                    _ => AppThemePreference.System
                }
            };
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
