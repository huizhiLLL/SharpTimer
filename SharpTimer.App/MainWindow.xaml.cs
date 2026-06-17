using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SharpTimer.App.Rendering;
using SharpTimer.Bluetooth;
using SharpTimer.App.Services;
using SharpTimer.App.ViewModels;
using SharpTimer.Core.Models;
using SharpTimer.Core.Statistics;
using SharpTimer.Core.SmartCubes;
using SharpTimer.Core.Timer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Graphics;
using Windows.Storage;

namespace SharpTimer.App
{
    public sealed partial class MainWindow : Window
    {
        private readonly ObservableCollection<SolveListItem> _solveItems = new();
        private readonly ObservableCollection<SessionListItem> _sessionItems = new();
        private readonly ObservableCollection<BluetoothDeviceListItem> _bluetoothDeviceItems = new();
        private readonly SmartCubeProtocolRegistry _bluetoothProtocolRegistry = SmartCubeKnownProtocols.CreateDefaultRegistry();
        private readonly BluetoothDeviceListItemFactory _bluetoothDeviceListItemFactory;
        private readonly SmartCubeScrambleTracker _smartCubeScrambleTracker = new();
        private readonly DispatcherTimer _uiTimer = new();
        private readonly DispatcherTimer _smartCubeKeepAliveTimer = new();
        private readonly AppSettingsService _settingsService = new();
        private TimerAppService? _appService;
        private WindowsBleSmartCubeScanner? _bluetoothScanner;
        private ISmartCubeConnection? _smartCubeConnection;
        private TimerAppSnapshot? _lastSnapshot;
        private AppSettings _settings = new();
        private LocalizedStrings _strings = LocalizedStrings.For(AppLanguagePreference.Chinese);
        private bool _isRendering;
        private bool _isApplyingSettings;
        private bool _isSpaceDown;
        private bool _isReadyToStart;
        private bool _smartCubeSolveHasMove;
        private bool _smartCubeReadyToStart;
        private bool _smartCubeHasLocalMoveState;
        private string? _smartCubeFacelets;
        private string? _scrambleTextRenderKey;
        private double _currentTimerScale = 1;
        private const int InitialWindowWidth = 2000;
        private const int InitialWindowHeight = 1200;
        private const int InitialWindowTopOffset = 10;
        private const double NormalSmartCubePreviewOffsetY = 100;
        private const double ImmersiveSmartCubePreviewOffsetY = 0;

        public MainWindow()
        {
            _bluetoothDeviceListItemFactory = new BluetoothDeviceListItemFactory(_bluetoothProtocolRegistry);
            InitializeComponent();
            ConfigureTitleBar();
            ApplyInitialWindowPlacement();

            SolvesList.ItemsSource = _solveItems;
            SessionComboBox.ItemsSource = _sessionItems;
            BluetoothDevicesList.ItemsSource = _bluetoothDeviceItems;
            SmartCubePreview.OpenRequested += SmartCubePreview_OpenRequested;
            SmartCubePreview.InteractionCompleted += SmartCubePreview_InteractionCompleted;
            AppRoot.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);
            AppRoot.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(RootGrid_KeyUp), true);
            RootGrid.Loaded += RootGrid_Loaded;
            Closed += MainWindow_Closed;

            _uiTimer.Interval = TimeSpan.FromMilliseconds(33);
            _uiTimer.Tick += UiTimer_Tick;
            _smartCubeKeepAliveTimer.Interval = TimeSpan.FromSeconds(60);
            _smartCubeKeepAliveTimer.Tick += SmartCubeKeepAliveTimer_Tick;
        }

        private void ApplyInitialWindowPlacement()
        {
            var initialSize = new SizeInt32(InitialWindowWidth, InitialWindowHeight);
            AppWindow.Resize(initialSize);

            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            var centeredX = workArea.X + (workArea.Width - initialSize.Width) / 2;
            var centeredY = workArea.Y + (workArea.Height - initialSize.Height) / 2;
            var targetX = Math.Max(workArea.X, centeredX);
            var targetY = Math.Max(workArea.Y, centeredY - InitialWindowTopOffset);
            AppWindow.Move(new PointInt32(targetX, targetY));
        }

        private void ConfigureTitleBar()
        {
            Title = "SharpTimer";
            AppWindow.Title = "SharpTimer";
            AppWindow.SetIcon("Assets/SharpTimer.ico");
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarDragRegion);

            AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            AppWindow.TitleBar.ButtonHoverBackgroundColor = Colors.Transparent;
        }

        private void TitleBarPaneToggleButton_Click(object sender, RoutedEventArgs e)
        {
            RootGrid.IsPaneOpen = !RootGrid.IsPaneOpen;
            FocusTimerInput();
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _bluetoothScanner?.Dispose();
            _bluetoothScanner = null;
            SmartCubePreview.StopAnimation();
            if (_smartCubeConnection is not null)
            {
                await _smartCubeConnection.DisposeAsync();
            }
        }

        private async void SmartCubeKeepAliveTimer_Tick(object? sender, object e)
        {
            var connection = _smartCubeConnection;
            if (connection is null)
            {
                _smartCubeKeepAliveTimer.Stop();
                return;
            }

            try
            {
                await connection.SendCommandAsync(SmartCubeCommand.RequestBattery);
            }
            catch
            {
            }
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
            ApplyBackdropMaterial(_settings.BackdropMaterial);
            _appService = new TimerAppService(databasePath, _settings);

            var snapshot = await _appService.InitializeAsync();
            RootGrid.SelectedItem = TimerNavItem;
            ShowPage(TimerPage);
            Render(snapshot);
            RenderSettings();
            _uiTimer.Start();
            DispatcherQueue.TryEnqueue(FocusTimerInput);
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

            if (IsTextInputSource(e.OriginalSource))
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

            if (IsTextInputSource(e.OriginalSource))
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

            if (ReferenceEquals(args.SelectedItem, TimerNavItem))
            {
                DispatcherQueue.TryEnqueue(FocusTimerInput);
            }
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

        private void InspectionSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private async void SolvesList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is SolveListItem item)
            {
                SolvesList.SelectedItem = item;
                await ShowSolveDetailsAsync(item);
            }
        }

        private void PrecisionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private void BackdropMaterialComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySettingsFromControls();
        }

        private void BluetoothButton_Click(object sender, RoutedEventArgs e)
        {
            RootGrid.Focus(FocusState.Programmatic);
        }

        private void BluetoothFlyout_Opened(object sender, object e)
        {
            if (_smartCubeConnection is not null)
            {
                RenderSmartCubeConnection();
                return;
            }

            _bluetoothDeviceItems.Clear();
            StartSmartCubeScan();
        }

        private void BluetoothFlyout_Closed(object sender, object e)
        {
            if (_smartCubeConnection is null)
            {
                StopBluetoothScan();
            }
        }

        private async void BluetoothDevicesList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not BluetoothDeviceListItem item)
            {
                return;
            }

            StopBluetoothScan();
            BluetoothFlyoutStatusText.Text = _strings.BluetoothConnectingMessage;
            BluetoothScanProgress.IsIndeterminate = true;
            BluetoothDevicesList.IsEnabled = false;
            BluetoothRetryScanButton.Visibility = Visibility.Collapsed;
            try
            {
                SmartCubePreview.ResetView();
                _smartCubeConnection = await WindowsBleSmartCubeConnector.ConnectAsync(item.Device);
                _smartCubeConnection.EventReceived += SmartCubeConnection_EventReceived;
                _smartCubeKeepAliveTimer.Start();
                RenderSmartCubeConnection();
                await _smartCubeConnection.SendCommandAsync(SmartCubeCommand.RequestBattery);
                await _smartCubeConnection.SendCommandAsync(SmartCubeCommand.RequestFacelets);
            }
            catch (Exception ex)
            {
                BluetoothFlyoutStatusText.Text = string.Format(_strings.BluetoothConnectFailedFormat, ex.Message);
                BluetoothScanProgress.IsIndeterminate = false;
                BluetoothDevicesList.IsEnabled = true;
                BluetoothRetryScanButton.Visibility = Visibility.Visible;
            }
        }

        private void BluetoothRetryScanButton_Click(object sender, RoutedEventArgs e)
        {
            _bluetoothDeviceItems.Clear();
            StartSmartCubeScan();
            RootGrid.Focus(FocusState.Programmatic);
        }

        private async void DisconnectCubeButton_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectSmartCubeAsync();
        }

        private void ResetCubeStateButton_Click(object sender, RoutedEventArgs e)
        {
            ResetSmartCubeLocalState();
            RootGrid.Focus(FocusState.Programmatic);
        }

        private void ResetCubeOrientationButton_Click(object sender, RoutedEventArgs e)
        {
            SmartCubePreview.ResetOrientationToDefault();
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

        private async System.Threading.Tasks.Task SetSolvePenaltyAsync(Guid solveId, Penalty penalty)
        {
            if (_appService is null)
            {
                return;
            }

            Render(await _appService.SetPenaltyAsync(solveId, penalty));
            RootGrid.Focus(FocusState.Programmatic);
        }

        private void Render(TimerAppSnapshot snapshot, bool refreshList = true)
        {
            _lastSnapshot = snapshot;
            _isRendering = true;
            RenderSessions(snapshot);
            if (_smartCubeConnection is null)
            {
                SetScrambleTextPlain(snapshot.CurrentScramble);
            }

            SyncSmartCubeScramble(snapshot);
            TimerText.Text = FormatTime(snapshot.Timer.Elapsed, _settings.DecimalPlaces);
            InspectionText.Text = FormatInspection(snapshot.Timer);
            ApplyTimerVisualState(snapshot.Timer);
            ApplyImmersiveTimerLayout(snapshot.Timer);

            BestText.Text = FormatNullableTime(snapshot.Statistics.Best, _settings.DecimalPlaces);
            Ao5Text.Text = FormatNullableTime(snapshot.Statistics.AverageOf5, _settings.DecimalPlaces);
            Ao12Text.Text = FormatNullableTime(snapshot.Statistics.AverageOf12, _settings.DecimalPlaces);
            TimerCountText.Text = snapshot.Statistics.Count.ToString();
            CountText.Text = string.Format(_strings.CountFormat, snapshot.Statistics.Count);
            AnalysisAo5Text.Text = FormatNullableTime(snapshot.Statistics.AverageOf5, _settings.DecimalPlaces);
            AnalysisAo12Text.Text = FormatNullableTime(snapshot.Statistics.AverageOf12, _settings.DecimalPlaces);
            AnalysisCountText.Text = snapshot.Statistics.Count.ToString();

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

            var primaryBrush = GetThemeBrush("TextFillColorPrimaryBrush");
            var secondaryBrush = GetThemeBrush("TextFillColorSecondaryBrush");
            var personalBestBrush = GetThemeBrush("PersonalBestBrush");
            var bestSingle = (TimeSpan?)null;
            var bestAverageOf5 = (TimeSpan?)null;
            var bestAverageOf12 = (TimeSpan?)null;
            var items = orderedSolves
                .Select((solve, index) =>
                {
                    var averageOf5 = StatisticsCalculator.CalculateAverageOf(orderedSolves.Take(index + 1), 5);
                    var averageOf12 = StatisticsCalculator.CalculateAverageOf(orderedSolves.Take(index + 1), 12);
                    var isSinglePersonalBest = IsNewPersonalBest(solve.EffectiveDuration, ref bestSingle);
                    var isAverageOf5PersonalBest = IsNewPersonalBest(averageOf5, ref bestAverageOf5);
                    var isAverageOf12PersonalBest = IsNewPersonalBest(averageOf12, ref bestAverageOf12);

                    return new SolveListItem
                    {
                        Id = solve.Id,
                        Number = (index + 1).ToString(),
                        Time = FormatSolveTime(solve, _settings.DecimalPlaces),
                        TimeForeground = isSinglePersonalBest ? personalBestBrush : primaryBrush,
                        AverageOf5 = FormatNullableTime(averageOf5, _settings.DecimalPlaces),
                        AverageOf5Foreground = isAverageOf5PersonalBest ? personalBestBrush : secondaryBrush,
                        AverageOf12 = FormatNullableTime(averageOf12, _settings.DecimalPlaces),
                        AverageOf12Foreground = isAverageOf12PersonalBest ? personalBestBrush : secondaryBrush,
                        Solve = solve
                    };
                })
                .Reverse()
                .ToArray();

            EmptySolvesPanel.Visibility = items.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            _solveItems.Clear();
            foreach (var item in items)
            {
                _solveItems.Add(item);
            }

            SolvesList.SelectedItem = _solveItems.FirstOrDefault(item => item.Id == selectedId)
                ?? _solveItems.FirstOrDefault();
        }

        private static bool IsNewPersonalBest(TimeSpan? value, ref TimeSpan? best)
        {
            if (value is null)
            {
                return false;
            }

            if (best is null || value.Value < best.Value)
            {
                best = value.Value;
                return true;
            }

            return false;
        }

        private static Brush GetThemeBrush(string resourceKey)
        {
            return Application.Current.Resources[resourceKey] as Brush
                ?? new SolidColorBrush(Microsoft.UI.Colors.Black);
        }

        private async System.Threading.Tasks.Task ShowSolveDetailsAsync(SolveListItem item)
        {
            if (_appService is null)
            {
                return;
            }

            var content = new StackPanel
            {
                Spacing = 16
            };
            content.Children.Add(new TextBlock
            {
                Text = string.Format(_strings.SolveDetailsTitleFormat, item.Number),
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            content.Children.Add(CreateDetailRow(_strings.TimeColumn, FormatSolveDetailTime(item.Solve, _settings.DecimalPlaces)));
            content.Children.Add(CreateDetailRow(_strings.SolveScrambleLabel, string.IsNullOrWhiteSpace(item.Solve.Scramble) ? "--" : item.Solve.Scramble));
            content.Children.Add(CreateDetailRow(_strings.SolveReplayLabel, _strings.SolveReplayUnavailable));
            content.Children.Add(CreateDetailRow(_strings.SolveCreatedAtLabel, item.Solve.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));

            ContentDialog? detailsDialog = null;
            Penalty? selectedPenalty = null;
            var deleteRequested = false;
            var actionButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            actionButtons.Children.Add(CreatePenaltyButton("+2", Penalty.PlusTwo, penalty =>
            {
                selectedPenalty = penalty;
                detailsDialog?.Hide();
            }));
            actionButtons.Children.Add(CreatePenaltyButton("DNF", Penalty.Dnf, penalty =>
            {
                selectedPenalty = penalty;
                detailsDialog?.Hide();
            }));
            actionButtons.Children.Add(CreatePenaltyButton(_strings.NoPenalty, Penalty.None, penalty =>
            {
                selectedPenalty = penalty;
                detailsDialog?.Hide();
            }));

            var deleteButton = new Button
            {
                Content = _strings.Delete
            };
            deleteButton.Click += (_, _) =>
            {
                deleteRequested = true;
                detailsDialog?.Hide();
            };
            actionButtons.Children.Add(deleteButton);
            content.Children.Add(actionButtons);

            detailsDialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Content = new ScrollViewer
                {
                    MaxHeight = 620,
                    Content = content
                },
                CloseButtonText = _strings.Cancel,
                DefaultButton = ContentDialogButton.Close
            };

            await detailsDialog.ShowAsync();
            if (selectedPenalty is not null)
            {
                await SetSolvePenaltyAsync(item.Id, selectedPenalty.Value);
                return;
            }

            if (deleteRequested)
            {
                await DeleteSolveWithConfirmationAsync(item.Id);
                return;
            }

            RootGrid.Focus(FocusState.Programmatic);
        }

        private async System.Threading.Tasks.Task DeleteSolveWithConfirmationAsync(Guid solveId)
        {
            if (!await ConfirmDeleteSolveAsync())
            {
                RootGrid.Focus(FocusState.Programmatic);
                return;
            }

            if (_appService is null)
            {
                return;
            }

            Render(await _appService.DeleteSolveAsync(solveId));
            RootGrid.Focus(FocusState.Programmatic);
        }

        private async System.Threading.Tasks.Task<bool> ConfirmDeleteSolveAsync()
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = _strings.DeleteSolveDialogTitle,
                Content = _strings.DeleteSolveDialogContent,
                PrimaryButtonText = _strings.Delete,
                CloseButtonText = _strings.Cancel,
                DefaultButton = ContentDialogButton.Close
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private static FrameworkElement CreateDetailRow(string label, string value)
        {
            var grid = new Grid
            {
                ColumnSpacing = 12
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelText = new TextBlock
            {
                Text = label,
                Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush,
                VerticalAlignment = VerticalAlignment.Top
            };

            var valueText = new TextBlock
            {
                Text = value,
                TextWrapping = TextWrapping.WrapWholeWords,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(valueText, 1);

            grid.Children.Add(labelText);
            grid.Children.Add(valueText);
            return grid;
        }

        private static string FormatSolveDetailTime(Solve solve, int decimalPlaces)
        {
            var time = FormatTime(solve.Duration, decimalPlaces);
            var penalty = FormatPenalty(solve.Penalty);
            return string.IsNullOrEmpty(penalty) ? time : $"{time} {penalty}";
        }

        private static Button CreatePenaltyButton(string text, Penalty penalty, Action<Penalty> selectPenalty)
        {
            var button = new Button { Content = text };
            button.Click += (_, _) =>
            {
                selectPenalty(penalty);
            };
            return button;
        }

        private string FormatInspection(TimerSnapshot snapshot)
        {
            return snapshot.Phase == TimerPhase.Inspecting
                ? string.Format(_strings.InspectionRemainingFormat, Math.Ceiling(snapshot.InspectionRemaining.TotalSeconds))
                : string.Empty;
        }

        private void ApplyTimerVisualState(TimerSnapshot snapshot)
        {
            var targetScale = _isReadyToStart || _smartCubeReadyToStart || snapshot.Phase == TimerPhase.Running ? 1.06 : 1;
            if (Math.Abs(_currentTimerScale - targetScale) > 0.001)
            {
                AnimateTimerScale(targetScale);
                _currentTimerScale = targetScale;
            }

            TimerText.Foreground = _isReadyToStart || _smartCubeReadyToStart
                ? GetThemeBrush("ScrambleNextBrush")
                : GetThemeBrush("TextFillColorPrimaryBrush");
        }

        private void ApplyImmersiveTimerLayout(TimerSnapshot snapshot)
        {
            var isImmersive = _isReadyToStart || _smartCubeReadyToStart || snapshot.Phase == TimerPhase.Running;
            var contextVisibility = isImmersive ? Visibility.Collapsed : Visibility.Visible;

            ScrambleText.Visibility = contextVisibility;
            InspectionText.Visibility = contextVisibility;
            StatsPanel.Visibility = contextVisibility;
            SmartCubePreviewOffset.Y = isImmersive
                ? ImmersiveSmartCubePreviewOffsetY
                : NormalSmartCubePreviewOffsetY;
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

        private void FocusTimerInput()
        {
            if (!AppRoot.Focus(FocusState.Programmatic))
            {
                RootGrid.Focus(FocusState.Programmatic);
            }
        }

        private static bool IsTextInputSource(object source)
        {
            for (var current = source as DependencyObject; current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is TextBox or PasswordBox or RichEditBox)
                {
                    return true;
                }
            }

            return false;
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
            _smartCubeReadyToStart = false;
            var snapshot = key == Windows.System.VirtualKey.Left
                ? _appService.MoveToPreviousScramble()
                : _appService.MoveToNextScramble();
            Render(snapshot, refreshList: false);
            RootGrid.Focus(FocusState.Programmatic);
        }

        private WindowsBleSmartCubeScanner CreateBluetoothScanner()
        {
            var scanner = new WindowsBleSmartCubeScanner();
            scanner.DeviceDiscovered += BluetoothScanner_DeviceDiscovered;
            return scanner;
        }

        private void BluetoothScanner_DeviceDiscovered(object? sender, SmartCubeDeviceInfo device)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (IsSmartCubeNameMatch(device))
                {
                    UpsertBluetoothDevice(device);
                }
            });
        }

        private void StartSmartCubeScan()
        {
            try
            {
                _bluetoothScanner ??= CreateBluetoothScanner();
                _bluetoothScanner.Start();
                BluetoothFlyoutStatusText.Text = _strings.BluetoothScanningMessage;
                BluetoothScanProgress.IsIndeterminate = true;
                BluetoothDevicesList.IsEnabled = true;
                BluetoothDevicesList.Visibility = Visibility.Visible;
                BluetoothRetryScanButton.Visibility = Visibility.Collapsed;
                ConnectedCubePanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                BluetoothFlyoutStatusText.Text = ex.Message;
                BluetoothScanProgress.IsIndeterminate = false;
            }
        }

        private void StopBluetoothScan()
        {
            try
            {
                _bluetoothScanner?.Stop();
            }
            finally
            {
                BluetoothScanProgress.IsIndeterminate = false;
            }
        }

        private void UpsertBluetoothDevice(SmartCubeDeviceInfo device)
        {
            var item = CreateBluetoothDeviceListItem(device);
            var existing = _bluetoothDeviceItems
                .Select((value, index) => new { value, index })
                .FirstOrDefault(entry => entry.value.Address == item.Address);

            if (existing is null)
            {
                _bluetoothDeviceItems.Add(item);
            }
            else
            {
                var merged = existing.value.Device.MergeAdvertisement(device);
                _bluetoothDeviceItems[existing.index] = CreateBluetoothDeviceListItem(merged);
            }
        }

        private BluetoothDeviceListItem CreateBluetoothDeviceListItem(SmartCubeDeviceInfo device)
        {
            return _bluetoothDeviceListItemFactory.Create(device, _strings);
        }

        private bool IsSmartCubeNameMatch(SmartCubeDeviceInfo device)
        {
            return _bluetoothProtocolRegistry.Protocols
                .Any(protocol => protocol.NameFilters.Any(filter => filter.Matches(device.Name)));
        }

        private void SmartCubeConnection_EventReceived(object? sender, SmartCubeEvent e)
        {
            if (!ReferenceEquals(sender, _smartCubeConnection))
            {
                return;
            }

            DispatcherQueue.TryEnqueue(async () =>
            {
                if (ReferenceEquals(sender, _smartCubeConnection))
                {
                    await RenderSmartCubeEventAsync(e);
                }
            });
        }

        private async System.Threading.Tasks.Task RenderSmartCubeEventAsync(SmartCubeEvent e)
        {
            switch (e)
            {
                case SmartCubeBatteryEvent battery:
                    ConnectedCubeBatteryText.Text = string.Format(_strings.BluetoothBatteryFormat, battery.BatteryLevel);
                    break;
                case SmartCubeFaceletsEvent facelets:
                    await HandleSmartCubeFaceletsEventAsync(facelets);
                    break;
                case SmartCubeMoveEvent move:
                    await HandleSmartCubeMoveEventAsync(move);
                    break;
                case SmartCubeGyroEvent gyro:
                    SmartCubePreview.SetOrientation(
                        gyro.Quaternion.X,
                        gyro.Quaternion.Y,
                        gyro.Quaternion.Z,
                        gyro.Quaternion.W);
                    break;
                case SmartCubeDisconnectEvent:
                    _smartCubeKeepAliveTimer.Stop();
                    _smartCubeConnection = null;
                    _smartCubeSolveHasMove = false;
                    _smartCubeReadyToStart = false;
                    _smartCubeHasLocalMoveState = false;
                    _smartCubeFacelets = null;
                    _smartCubeScrambleTracker.Reset();
                    _scrambleTextRenderKey = null;
                    SmartCubePreview.ResetView();
                    SmartCubePreview.Visibility = Visibility.Collapsed;
                    ConnectedCubePanel.Visibility = Visibility.Collapsed;
                    BluetoothFlyoutStatusText.Text = _strings.BluetoothDisconnectedMessage;
                    break;
            }
        }

        private async System.Threading.Tasks.Task HandleSmartCubeMoveEventAsync(SmartCubeMoveEvent move)
        {
            var hasLocalFacelets = TryApplySmartCubeLocalMove(move.Move);

            if (_lastSnapshot?.Timer.Phase == TimerPhase.Running)
            {
                _smartCubeSolveHasMove = true;
                if (hasLocalFacelets && ThreeByThreeFacelets.IsSolvedIgnoringRotation(_smartCubeFacelets!) && _appService is not null)
                {
                    _smartCubeSolveHasMove = false;
                    _smartCubeReadyToStart = false;
                    Render(await _appService.StopSmartCubeSolveAsync());
                    SyncSmartCubeScramble(_lastSnapshot);
                }
                else if (!hasLocalFacelets)
                {
                    await RequestSmartCubeFaceletsAsync();
                }

                return;
            }

            if (_smartCubeReadyToStart)
            {
                _smartCubeReadyToStart = false;
                _smartCubeSolveHasMove = true;
                if (_appService is not null)
                {
                    Render(await _appService.HandleSmartCubeMoveAsync());
                }

                if (!hasLocalFacelets)
                {
                    await RequestSmartCubeFaceletsAsync();
                }

                return;
            }

            EnsureSmartCubeScramble(_lastSnapshot);
            var scrambleSnapshot = hasLocalFacelets
                ? _smartCubeScrambleTracker.Current
                : _smartCubeScrambleTracker.ApplyMove(move.Move);
            _smartCubeHasLocalMoveState = scrambleSnapshot.CurrentFacelets is not null;
            if (ThreeByThreeFacelets.IsValidState(scrambleSnapshot.CurrentFacelets ?? string.Empty))
            {
                _smartCubeFacelets = scrambleSnapshot.CurrentFacelets;
                RenderSmartCubePreview(_smartCubeFacelets);
            }

            ApplySmartCubeScrambleSnapshot(scrambleSnapshot);
            if (!hasLocalFacelets)
            {
                await RequestSmartCubeFaceletsAsync();
            }
        }

        private bool TryApplySmartCubeLocalMove(string move)
        {
            if (!ThreeByThreeFacelets.IsValidState(_smartCubeFacelets ?? string.Empty))
            {
                return false;
            }

            try
            {
                var currentFacelets = _smartCubeFacelets!;
                var nextFacelets = ThreeByThreeFacelets.ApplyMove(currentFacelets, move);
                _smartCubeScrambleTracker.UpdateFacelets(currentFacelets);
                var scrambleSnapshot = _smartCubeScrambleTracker.ApplyMove(move);
                _smartCubeFacelets = ThreeByThreeFacelets.IsValidState(scrambleSnapshot.CurrentFacelets ?? string.Empty)
                    ? scrambleSnapshot.CurrentFacelets!
                    : nextFacelets;
                SmartCubePreview.PlayMove(currentFacelets, _smartCubeFacelets, move);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async System.Threading.Tasks.Task HandleSmartCubeFaceletsEventAsync(SmartCubeFaceletsEvent facelets)
        {
            SmartCubePreview.Visibility = Visibility.Visible;
            var shouldUseFaceletsState = facelets.IsAuthoritative
                || !_smartCubeHasLocalMoveState
                || _lastSnapshot?.Timer.Phase == TimerPhase.Running;
            if (shouldUseFaceletsState)
            {
                _smartCubeFacelets = facelets.Facelets;
                RenderSmartCubePreview(facelets.Facelets);
                if (facelets.IsAuthoritative)
                {
                    _smartCubeHasLocalMoveState = false;
                }
            }

            var solved = ThreeByThreeFacelets.IsSolvedIgnoringRotation(facelets.Facelets);

            if (solved && _smartCubeSolveHasMove && _lastSnapshot?.Timer.Phase == TimerPhase.Running && _appService is not null)
            {
                _smartCubeSolveHasMove = false;
                _smartCubeReadyToStart = false;
                Render(await _appService.StopSmartCubeSolveAsync());
                SyncSmartCubeScramble(_lastSnapshot);
                return;
            }

            if (_lastSnapshot?.Timer.Phase != TimerPhase.Running)
            {
                EnsureSmartCubeScramble(_lastSnapshot);
                var scrambleSnapshot = _smartCubeHasLocalMoveState
                    ? _smartCubeScrambleTracker.Current
                    : _smartCubeScrambleTracker.UpdateFacelets(facelets.Facelets);
                ApplySmartCubeScrambleSnapshot(scrambleSnapshot);
            }
        }

        private async System.Threading.Tasks.Task RequestSmartCubeFaceletsAsync()
        {
            var connection = _smartCubeConnection;
            if (connection is null)
            {
                return;
            }

            try
            {
                await connection.SendCommandAsync(SmartCubeCommand.RequestFacelets);
            }
            catch
            {
            }
        }

        private void RenderSmartCubeConnection()
        {
            if (_smartCubeConnection is null)
            {
                ConnectedCubePanel.Visibility = Visibility.Collapsed;
                BluetoothDevicesList.IsEnabled = true;
                BluetoothDevicesList.Visibility = Visibility.Visible;
                BluetoothRetryScanButton.Visibility = Visibility.Collapsed;
                BluetoothFlyoutStatusText.Text = _strings.BluetoothScanningMessage;
                return;
            }

            BluetoothDevicesList.IsEnabled = true;
            BluetoothDevicesList.Visibility = Visibility.Collapsed;
            BluetoothRetryScanButton.Visibility = Visibility.Collapsed;
            BluetoothScanProgress.IsIndeterminate = false;
            ConnectedCubePanel.Visibility = Visibility.Visible;
            SmartCubePreview.Visibility = Visibility.Visible;
            ConnectedCubeNameText.Text = _smartCubeConnection.DeviceName;
            ConnectedCubeBatteryText.Text = _strings.BluetoothBatteryUnknown;
            SyncSmartCubeScramble(_lastSnapshot);
            if (ThreeByThreeFacelets.IsValidState(_smartCubeFacelets ?? string.Empty))
            {
                RenderSmartCubePreview(_smartCubeFacelets);
            }
            else
            {
                RenderSmartCubePreview(null);
            }
            BluetoothFlyoutStatusText.Text = _strings.BluetoothConnectedMessage;
        }

        private async System.Threading.Tasks.Task DisconnectSmartCubeAsync()
        {
            var connection = _smartCubeConnection;
            if (connection is null)
            {
                return;
            }

            StopBluetoothScan();
            _smartCubeKeepAliveTimer.Stop();
            _smartCubeConnection = null;
            connection.EventReceived -= SmartCubeConnection_EventReceived;
            _smartCubeSolveHasMove = false;
            _smartCubeReadyToStart = false;
            _smartCubeHasLocalMoveState = false;
            _smartCubeFacelets = null;
            SmartCubePreview.StopAnimation();
            SmartCubePreview.ResetView();
            _smartCubeScrambleTracker.Reset();
            _scrambleTextRenderKey = null;
            ConnectedCubePanel.Visibility = Visibility.Collapsed;
            SmartCubePreview.Visibility = Visibility.Collapsed;
            _bluetoothDeviceItems.Clear();
            StartSmartCubeScan();
            await connection.DisposeAsync();
        }

        private void ResetSmartCubeLocalState()
        {
            _smartCubeFacelets = ThreeByThreeFacelets.Solved;
            _smartCubeSolveHasMove = false;
            _smartCubeReadyToStart = false;
            _smartCubeHasLocalMoveState = false;
            SmartCubePreview.ResetViewAngles();
            RenderSmartCubePreview(_smartCubeFacelets);
            SyncSmartCubeScramble(_lastSnapshot);
        }

        private void SyncSmartCubeScramble(TimerAppSnapshot? snapshot)
        {
            if (_smartCubeConnection is null || snapshot is null || snapshot.Timer.Phase == TimerPhase.Running)
            {
                return;
            }

            if (_smartCubeScrambleTracker.SetScramble(snapshot.CurrentScramble))
            {
                _smartCubeHasLocalMoveState = false;
                _scrambleTextRenderKey = null;
            }

            if (_smartCubeHasLocalMoveState)
            {
                ApplySmartCubeScrambleSnapshot(_smartCubeScrambleTracker.Current);
            }
            else if (ThreeByThreeFacelets.IsValidState(_smartCubeFacelets ?? string.Empty))
            {
                ApplySmartCubeScrambleSnapshot(_smartCubeScrambleTracker.UpdateFacelets(_smartCubeFacelets!));
            }
            else
            {
                ApplySmartCubeScrambleSnapshot(_smartCubeScrambleTracker.Current);
            }
        }

        private void EnsureSmartCubeScramble(TimerAppSnapshot? snapshot)
        {
            if (_smartCubeConnection is null || snapshot is null || snapshot.Timer.Phase == TimerPhase.Running)
            {
                return;
            }

            if (_smartCubeScrambleTracker.SetScramble(snapshot.CurrentScramble))
            {
                _smartCubeHasLocalMoveState = false;
                _scrambleTextRenderKey = null;
            }
        }

        private void ApplySmartCubeScrambleSnapshot(SmartCubeScrambleSnapshot snapshot)
        {
            if (_lastSnapshot?.Timer.Phase == TimerPhase.Running)
            {
                return;
            }

            _smartCubeReadyToStart = snapshot.IsReady;
            RenderSmartCubeScrambleText(snapshot);

            ApplyTimerVisualState(_lastSnapshot?.Timer ?? new TimerSnapshot(TimerPhase.Idle, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, Penalty.None, null, null));
            if (_lastSnapshot is not null)
            {
                ApplyImmersiveTimerLayout(_lastSnapshot.Timer);
            }
        }

        private void RenderSmartCubeScrambleText(SmartCubeScrambleSnapshot snapshot)
        {
            var runs = ScrambleTextPresenter.BuildSmartCubeRuns(
                snapshot,
                _strings.BluetoothScrambleRestoreRequired,
                _lastSnapshot?.CurrentScramble ?? string.Empty);
            var renderKey = "smart:" + string.Join("|", runs.Select(run => $"{run.Role}:{run.Text}"));
            if (_scrambleTextRenderKey == renderKey)
            {
                return;
            }

            _scrambleTextRenderKey = renderKey;
            ScrambleText.Inlines.Clear();

            foreach (var run in runs)
            {
                AddScrambleRun(run.Text, GetScrambleRunBrush(run.Role));
            }
        }

        private void AddScrambleRun(string text, Brush brush)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (ScrambleText.Inlines.Count > 0)
            {
                ScrambleText.Inlines.Add(new Run { Text = " " });
            }

            ScrambleText.Inlines.Add(new Run
            {
                Text = text,
                Foreground = brush
            });
        }

        private void SetScrambleTextPlain(string text)
        {
            var renderKey = "plain:" + text;
            if (_scrambleTextRenderKey == renderKey)
            {
                return;
            }

            _scrambleTextRenderKey = renderKey;
            ScrambleText.Inlines.Clear();
            ScrambleText.Inlines.Add(new Run
            {
                Text = text,
                Foreground = GetPrimaryTextBrush()
            });
        }

        private Brush GetScrambleRunBrush(ScrambleTextRole role)
        {
            return role switch
            {
                ScrambleTextRole.Next => GetNextScrambleBrush(),
                ScrambleTextRole.Correction => GetCorrectionScrambleBrush(),
                _ => GetPrimaryTextBrush()
            };
        }

        private Brush GetPrimaryTextBrush()
        {
            return GetThemeBrush("TextFillColorPrimaryBrush");
        }

        private Brush GetNextScrambleBrush()
        {
            return GetThemeBrush("ScrambleNextBrush");
        }

        private Brush GetCorrectionScrambleBrush()
        {
            return GetThemeBrush("ScrambleCorrectionBrush");
        }

        private void SmartCubePreview_OpenRequested(object? sender, EventArgs e)
        {
            BluetoothFlyout.ShowAt(BluetoothButton);
        }

        private void SmartCubePreview_InteractionCompleted(object? sender, EventArgs e)
        {
            FocusTimerInput();
        }

        private void RenderSmartCubePreview(string? facelets)
        {
            SmartCubePreview.SetFacelets(facelets);
        }

        private static string FormatSolveTime(Solve solve, int decimalPlaces)
        {
            return solve.Penalty switch
            {
                Penalty.Dnf => "DNF",
                Penalty.PlusTwo => $"({FormatTime(solve.EffectiveDuration ?? solve.Duration, decimalPlaces)}+)",
                _ => FormatTime(solve.Duration, decimalPlaces)
            };
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
            try
            {
                InspectionSwitch.IsOn = _settings.UseInspection;
                SetSelectedIndex(PrecisionComboBox, _settings.DecimalPlaces == 3 ? 1 : 0);
                SetSelectedIndex(ThemeComboBox, _settings.Theme switch
                {
                    AppThemePreference.Light => 1,
                    AppThemePreference.Dark => 2,
                    _ => 0
                });
                SetSelectedIndex(BackdropMaterialComboBox, _settings.BackdropMaterial switch
                {
                    AppBackdropMaterialPreference.MicaAlt => 1,
                    AppBackdropMaterialPreference.Acrylic => 2,
                    _ => 0
                });
                SetSelectedIndex(LanguageComboBox, _settings.Language == AppLanguagePreference.English ? 1 : 0);
            }
            finally
            {
                _isApplyingSettings = false;
            }
        }

        private void ApplyLanguage()
        {
            TimerNavItem.Content = _strings.TimerNav;
            SolvesNavItem.Content = _strings.SolvesNav;
            if (RootGrid.SettingsItem is NavigationViewItem settingsItem)
            {
                settingsItem.Content = _strings.SettingsNav;
            }

            ToolTipService.SetToolTip(RenameSessionButton, _strings.RenameSession);
            ToolTipService.SetToolTip(NewSessionButton, _strings.NewSession);
            ToolTipService.SetToolTip(ArchiveSessionButton, _strings.Delete);
            AutomationProperties.SetName(TitleBarPaneToggleButton, _strings.TitleBarToggleNavigation);
            AutomationProperties.SetName(BluetoothButton, _strings.BluetoothButtonName);
            AutomationProperties.SetName(BluetoothDevicesList, _strings.BluetoothDevicesListName);
            AutomationProperties.SetName(SmartCubePreview, _strings.SmartCubePreviewName);
            AutomationProperties.SetName(RenameSessionButton, _strings.RenameSession);
            AutomationProperties.SetName(NewSessionButton, _strings.NewSession);
            AutomationProperties.SetName(ArchiveSessionButton, _strings.Delete);
            TimeColumnText.Text = _strings.TimeColumn;
            BestLabelText.Text = _strings.BestLabel;
            TimerCountLabelText.Text = _strings.AnalysisCountLabel;
            AnalysisCountLabelText.Text = _strings.AnalysisCountLabel;
            EmptySolvesTitleText.Text = _strings.EmptySolvesTitle;
            EmptySolvesDescriptionText.Text = _strings.EmptySolvesDescription;
            BluetoothFlyoutStatusText.Text = _strings.BluetoothScanningMessage;
            BluetoothRetryScanButton.Content = _strings.BluetoothRetryScan;
            ResetCubeStateButton.Content = _strings.BluetoothResetCubeState;
            ResetCubeOrientationButton.Content = _strings.BluetoothResetCubeOrientation;
            DisconnectCubeButton.Content = _strings.BluetoothDisconnect;
            SettingsTitleText.Text = _strings.SettingsTitle;
            SettingsDescriptionText.Text = _strings.SettingsDescription;
            SettingsTimingSectionTitleText.Text = _strings.SettingsTimingSectionTitle;
            SettingsTimingSectionDescriptionText.Text = _strings.SettingsTimingSectionDescription;
            SettingsAppearanceSectionTitleText.Text = _strings.SettingsAppearanceSectionTitle;
            SettingsAppearanceSectionDescriptionText.Text = _strings.SettingsAppearanceSectionDescription;
            SettingsLanguageSectionTitleText.Text = _strings.SettingsLanguageSectionTitle;
            SettingsLanguageSectionDescriptionText.Text = _strings.SettingsLanguageSectionDescription;
            InspectionSwitch.Header = _strings.InspectionHeader;
            PrecisionComboBox.Header = _strings.PrecisionHeader;
            CentisecondsItem.Content = _strings.Centiseconds;
            MillisecondsItem.Content = _strings.Milliseconds;
            ThemeComboBox.Header = _strings.ThemeHeader;
            SystemThemeItem.Content = _strings.SystemTheme;
            LightThemeItem.Content = _strings.LightTheme;
            DarkThemeItem.Content = _strings.DarkTheme;
            BackdropMaterialComboBox.Header = _strings.BackdropMaterialHeader;
            MicaMaterialItem.Content = _strings.MicaMaterial;
            MicaAltMaterialItem.Content = _strings.MicaAltMaterial;
            AcrylicMaterialItem.Content = _strings.AcrylicMaterial;
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

            var previousTheme = _settings.Theme;
            var previousBackdropMaterial = _settings.BackdropMaterial;
            var previousLanguage = _settings.Language;
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
                BackdropMaterial = BackdropMaterialComboBox.SelectedIndex switch
                {
                    1 => AppBackdropMaterialPreference.MicaAlt,
                    2 => AppBackdropMaterialPreference.Acrylic,
                    _ => AppBackdropMaterialPreference.Mica
                },
                Language = LanguageComboBox.SelectedIndex == 1
                    ? AppLanguagePreference.English
                    : AppLanguagePreference.Chinese
            };

            _settingsService.Save(_settings);
            if (_settings.Language != previousLanguage)
            {
                _strings = LocalizedStrings.For(_settings.Language);
                ApplyLanguage();
                RenderSettings();
            }

            if (_settings.Theme != previousTheme)
            {
                ApplyTheme(_settings.Theme);
            }

            if (_settings.BackdropMaterial != previousBackdropMaterial)
            {
                ApplyBackdropMaterial(_settings.BackdropMaterial);
            }

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

        private static void SetSelectedIndex(ComboBox comboBox, int selectedIndex)
        {
            if (comboBox.SelectedIndex == selectedIndex)
            {
                comboBox.SelectedIndex = -1;
            }

            comboBox.SelectedIndex = selectedIndex;
        }

        private void ApplyBackdropMaterial(AppBackdropMaterialPreference material)
        {
            try
            {
                SystemBackdrop = material switch
                {
                    AppBackdropMaterialPreference.MicaAlt when MicaController.IsSupported() =>
                        new MicaBackdrop { Kind = MicaKind.BaseAlt },
                    AppBackdropMaterialPreference.Acrylic when DesktopAcrylicController.IsSupported() =>
                        new DesktopAcrylicBackdrop(),
                    _ when MicaController.IsSupported() =>
                        new MicaBackdrop { Kind = MicaKind.Base },
                    _ => null
                };
            }
            catch
            {
                SystemBackdrop = null;
            }
        }
    }
}
