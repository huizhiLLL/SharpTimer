using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Windowing;
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
        private readonly SmartCubeScrambleTracker _smartCubeScrambleTracker = new();
        private readonly DispatcherTimer _uiTimer = new();
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

        private enum ScrambleRunRole
        {
            Primary,
            Next,
            Correction
        }

        private readonly record struct ScrambleDisplayRun(string Text, ScrambleRunRole Role);

        public MainWindow()
        {
            InitializeComponent();
            ApplyInitialWindowPlacement();

            SolvesList.ItemsSource = _solveItems;
            SessionComboBox.ItemsSource = _sessionItems;
            BluetoothDevicesList.ItemsSource = _bluetoothDeviceItems;
            RootGrid.Loaded += RootGrid_Loaded;
            Closed += MainWindow_Closed;

            _uiTimer.Interval = TimeSpan.FromMilliseconds(33);
            _uiTimer.Tick += UiTimer_Tick;
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

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _bluetoothScanner?.Dispose();
            _bluetoothScanner = null;
            if (_smartCubeConnection is not null)
            {
                await _smartCubeConnection.DisposeAsync();
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
            try
            {
                _smartCubeConnection = await WindowsBleSmartCubeConnector.ConnectAsync(item.Device);
                _smartCubeConnection.EventReceived += SmartCubeConnection_EventReceived;
                RenderSmartCubeConnection();
                await _smartCubeConnection.SendCommandAsync(SmartCubeCommand.RequestBattery);
                await _smartCubeConnection.SendCommandAsync(SmartCubeCommand.RequestFacelets);
            }
            catch (Exception ex)
            {
                BluetoothFlyoutStatusText.Text = string.Format(_strings.BluetoothConnectFailedFormat, ex.Message);
                BluetoothScanProgress.IsIndeterminate = false;
            }
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
            if (_smartCubeConnection is null)
            {
                SetScrambleTextPlain(snapshot.CurrentScramble);
            }

            SyncSmartCubeScramble(snapshot);
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
            var targetScale = _isReadyToStart || _smartCubeReadyToStart || snapshot.Phase == TimerPhase.Running ? 1.06 : 1;
            if (Math.Abs(_currentTimerScale - targetScale) > 0.001)
            {
                AnimateTimerScale(targetScale);
                _currentTimerScale = targetScale;
            }

            TimerText.Foreground = _isReadyToStart || _smartCubeReadyToStart
                ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
                : Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush;
        }

        private void ApplyImmersiveTimerLayout(TimerSnapshot snapshot)
        {
            var isImmersive = _isReadyToStart || _smartCubeReadyToStart || snapshot.Phase == TimerPhase.Running;
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
                BluetoothDevicesList.Visibility = Visibility.Visible;
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
                _bluetoothDeviceItems[existing.index] = item;
            }
        }

        private BluetoothDeviceListItem CreateBluetoothDeviceListItem(SmartCubeDeviceInfo device)
        {
            var protocol = _bluetoothProtocolRegistry.ResolveByGatt(device);
            var services = device.ServiceUuids.Count == 0
                ? _strings.BluetoothNoServices
                : string.Join(", ", device.ServiceUuids.Take(3).Select(FormatUuid));
            if (device.ServiceUuids.Count > 3)
            {
                services = string.Format(_strings.BluetoothServicesSummaryFormat, device.ServiceUuids.Count);
            }

            return new BluetoothDeviceListItem
            {
                Device = device,
                Address = FormatBluetoothAddress(device.BluetoothAddress),
                Name = string.IsNullOrWhiteSpace(device.Name) ? _strings.BluetoothUnknownDevice : device.Name,
                Protocol = protocol?.Info.Name ?? _strings.BluetoothUnknownProtocol,
                Services = services,
                LastSeen = device.SeenAt.ToLocalTime().ToString("HH:mm:ss")
            };
        }

        private bool IsSmartCubeNameMatch(SmartCubeDeviceInfo device)
        {
            return _bluetoothProtocolRegistry.Protocols
                .Any(protocol => protocol.NameFilters.Any(filter => filter.Matches(device.Name)));
        }

        private static string FormatBluetoothAddress(ulong address)
        {
            var text = address.ToString("X12");
            return string.Join(":", Enumerable.Range(0, 6).Select(index => text.Substring(index * 2, 2)));
        }

        private static string FormatUuid(Guid uuid)
        {
            return uuid.ToString("D");
        }

        private void SmartCubeConnection_EventReceived(object? sender, SmartCubeEvent e)
        {
            DispatcherQueue.TryEnqueue(async () => await RenderSmartCubeEventAsync(e));
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
                case SmartCubeGyroEvent:
                    break;
                case SmartCubeDisconnectEvent:
                    _smartCubeConnection = null;
                    _smartCubeSolveHasMove = false;
                    _smartCubeReadyToStart = false;
                    _smartCubeHasLocalMoveState = false;
                    _smartCubeFacelets = null;
                    _smartCubeScrambleTracker.Reset();
                    _scrambleTextRenderKey = null;
                    SmartCubePreviewCanvas.Visibility = Visibility.Collapsed;
                    ConnectedCubePanel.Visibility = Visibility.Collapsed;
                    BluetoothFlyoutStatusText.Text = _strings.BluetoothDisconnectedMessage;
                    break;
            }
        }

        private async System.Threading.Tasks.Task HandleSmartCubeMoveEventAsync(SmartCubeMoveEvent move)
        {
            if (_lastSnapshot?.Timer.Phase == TimerPhase.Running)
            {
                _smartCubeSolveHasMove = true;
                await RequestSmartCubeFaceletsAsync();
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

                await RequestSmartCubeFaceletsAsync();
                return;
            }

            EnsureSmartCubeScramble(_lastSnapshot);
            var scrambleSnapshot = _smartCubeScrambleTracker.ApplyMove(move.Move);
            _smartCubeHasLocalMoveState = scrambleSnapshot.CurrentFacelets is not null;
            if (ThreeByThreeFacelets.IsValidState(scrambleSnapshot.CurrentFacelets ?? string.Empty))
            {
                _smartCubeFacelets = scrambleSnapshot.CurrentFacelets;
                RenderSmartCubePreview(_smartCubeFacelets);
            }

            ApplySmartCubeScrambleSnapshot(scrambleSnapshot);
            await RequestSmartCubeFaceletsAsync();
        }

        private async System.Threading.Tasks.Task HandleSmartCubeFaceletsEventAsync(SmartCubeFaceletsEvent facelets)
        {
            SmartCubePreviewCanvas.Visibility = Visibility.Visible;
            var shouldUseFaceletsState = !_smartCubeHasLocalMoveState || _lastSnapshot?.Timer.Phase == TimerPhase.Running;
            if (shouldUseFaceletsState)
            {
                _smartCubeFacelets = facelets.Facelets;
                RenderSmartCubePreview(facelets.Facelets);
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
            if (_smartCubeConnection is null)
            {
                return;
            }

            try
            {
                await _smartCubeConnection.SendCommandAsync(SmartCubeCommand.RequestFacelets);
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
                BluetoothDevicesList.Visibility = Visibility.Visible;
                BluetoothFlyoutStatusText.Text = _strings.BluetoothScanningMessage;
                return;
            }

            BluetoothDevicesList.Visibility = Visibility.Collapsed;
            BluetoothScanProgress.IsIndeterminate = false;
            ConnectedCubePanel.Visibility = Visibility.Visible;
            SmartCubePreviewCanvas.Visibility = Visibility.Visible;
            ConnectedCubeNameText.Text = _smartCubeConnection.DeviceName;
            ConnectedCubeBatteryText.Text = _strings.BluetoothBatteryUnknown;
            SyncSmartCubeScramble(_lastSnapshot);
            if (ThreeByThreeFacelets.IsValidState(_smartCubeFacelets ?? string.Empty))
            {
                RenderSmartCubePreview(_smartCubeFacelets);
            }
            else if (SmartCubePreviewCanvas.Children.Count == 0)
            {
                RenderSmartCubePreview(null);
            }
            BluetoothFlyoutStatusText.Text = _strings.BluetoothConnectedMessage;
        }

        private async System.Threading.Tasks.Task DisconnectSmartCubeAsync()
        {
            if (_smartCubeConnection is null)
            {
                return;
            }

            _smartCubeConnection.EventReceived -= SmartCubeConnection_EventReceived;
            await _smartCubeConnection.DisposeAsync();
            _smartCubeConnection = null;
            _smartCubeSolveHasMove = false;
            _smartCubeReadyToStart = false;
            _smartCubeHasLocalMoveState = false;
            _smartCubeFacelets = null;
            _smartCubeScrambleTracker.Reset();
            _scrambleTextRenderKey = null;
            ConnectedCubePanel.Visibility = Visibility.Collapsed;
            SmartCubePreviewCanvas.Visibility = Visibility.Collapsed;
            BluetoothDevicesList.Visibility = Visibility.Visible;
            BluetoothFlyoutStatusText.Text = _strings.BluetoothDisconnectedMessage;
        }

        private void ResetSmartCubeLocalState()
        {
            _smartCubeFacelets = ThreeByThreeFacelets.Solved;
            _smartCubeSolveHasMove = false;
            _smartCubeReadyToStart = false;
            _smartCubeHasLocalMoveState = false;
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
            var runs = BuildSmartCubeScrambleRuns(snapshot);
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

        private IReadOnlyList<ScrambleDisplayRun> BuildSmartCubeScrambleRuns(SmartCubeScrambleSnapshot snapshot)
        {
            var runs = new List<ScrambleDisplayRun>();
            switch (snapshot.Status)
            {
                case SmartCubeScrambleStatus.Ready:
                    return runs;
                case SmartCubeScrambleStatus.RestoreRequired:
                    runs.Add(new ScrambleDisplayRun(_strings.BluetoothScrambleRestoreRequired, ScrambleRunRole.Correction));
                    return runs;
                case SmartCubeScrambleStatus.Correction:
                    var displayMoves = new List<(string Move, bool IsCorrection)>();
                    foreach (var move in snapshot.CorrectionMoves)
                    {
                        AppendDisplayMove(displayMoves, move, isCorrection: true);
                    }

                    foreach (var move in snapshot.RemainingMoves)
                    {
                        AppendDisplayMove(displayMoves, move, isCorrection: false);
                    }

                    var highlightedNext = false;
                    foreach (var move in displayMoves)
                    {
                        if (move.IsCorrection)
                        {
                            runs.Add(new ScrambleDisplayRun(move.Move, ScrambleRunRole.Correction));
                        }
                        else if (!highlightedNext)
                        {
                            runs.Add(new ScrambleDisplayRun(move.Move, ScrambleRunRole.Next));
                            highlightedNext = true;
                        }
                        else
                        {
                            runs.Add(new ScrambleDisplayRun(move.Move, ScrambleRunRole.Primary));
                        }
                    }

                    return runs;
                case SmartCubeScrambleStatus.Scrambling:
                    for (var index = 0; index < snapshot.RemainingMoves.Count; index++)
                    {
                        runs.Add(new ScrambleDisplayRun(
                            snapshot.RemainingMoves[index],
                            index == 0 ? ScrambleRunRole.Next : ScrambleRunRole.Primary));
                    }

                    return runs;
                default:
                    runs.Add(new ScrambleDisplayRun(_lastSnapshot?.CurrentScramble ?? string.Empty, ScrambleRunRole.Primary));
                    return runs;
            }
        }

        private static void AppendDisplayMove(IList<(string Move, bool IsCorrection)> moves, string move, bool isCorrection)
        {
            if (string.IsNullOrWhiteSpace(move))
            {
                return;
            }

            var normalized = SmartCubeMoveNotation.Normalize(move);
            if (moves.Count == 0 || moves[^1].Move[0] != normalized[0])
            {
                moves.Add((normalized, isCorrection));
                return;
            }

            var last = moves[^1];
            var mergedPower = (GetMovePower(last.Move) + GetMovePower(normalized)) % 4;
            var mergedCorrection = last.IsCorrection || isCorrection;
            moves.RemoveAt(moves.Count - 1);
            if (mergedPower != 0)
            {
                moves.Add((last.Move[0] + GetMoveSuffix(mergedPower), mergedCorrection));
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

        private Brush GetScrambleRunBrush(ScrambleRunRole role)
        {
            return role switch
            {
                ScrambleRunRole.Next => GetNextScrambleBrush(),
                ScrambleRunRole.Correction => GetCorrectionScrambleBrush(),
                _ => GetPrimaryTextBrush()
            };
        }

        private static int GetMovePower(string move)
        {
            return move.Length == 1
                ? 1
                : move[1] == '2'
                    ? 2
                    : 3;
        }

        private static string GetMoveSuffix(int power)
        {
            return power switch
            {
                2 => "2",
                3 => "'",
                _ => string.Empty
            };
        }

        private Brush GetPrimaryTextBrush()
        {
            return Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush
                ?? new SolidColorBrush(Microsoft.UI.Colors.Black);
        }

        private static Brush GetNextScrambleBrush()
        {
            return new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
        }

        private static Brush GetCorrectionScrambleBrush()
        {
            return new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
        }

        private void RenderSmartCubePreview(string? facelets)
        {
            DrawSmartCubePreview(SmartCubePreviewCanvas, facelets);
        }

        private void DrawSmartCubePreview(Canvas canvas, string? facelets)
        {
            canvas.Children.Clear();
            if (string.IsNullOrWhiteSpace(facelets) || facelets.Length != 54)
            {
                DrawEmptySmartCubePreview(canvas);
                return;
            }

            var origin = new Windows.Foundation.Point(52, 48);
            var frontX = new Windows.Foundation.Point(14, 8);
            var frontY = new Windows.Foundation.Point(0, 16);
            var depth = new Windows.Foundation.Point(10, -6);

            DrawCubeFace(canvas, facelets, faceStart: 0, Add(origin, Multiply(depth, 3)), frontX, Multiply(depth, -1));
            DrawCubeFace(canvas, facelets, faceStart: 9, Add(origin, Multiply(frontX, 3)), depth, frontY);
            DrawCubeFace(canvas, facelets, faceStart: 18, origin, frontX, frontY);
        }

        private void DrawEmptySmartCubePreview(Canvas canvas)
        {
            var origin = new Windows.Foundation.Point(52, 48);
            var frontX = new Windows.Foundation.Point(14, 8);
            var frontY = new Windows.Foundation.Point(0, 16);
            var depth = new Windows.Foundation.Point(10, -6);

            DrawCubeFace(canvas, null, faceStart: 0, Add(origin, Multiply(depth, 3)), frontX, Multiply(depth, -1));
            DrawCubeFace(canvas, null, faceStart: 9, Add(origin, Multiply(frontX, 3)), depth, frontY);
            DrawCubeFace(canvas, null, faceStart: 18, origin, frontX, frontY);
        }

        private void DrawCubeFace(
            Canvas canvas,
            string? facelets,
            int faceStart,
            Windows.Foundation.Point origin,
            Windows.Foundation.Point xVector,
            Windows.Foundation.Point yVector)
        {
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var stickerOrigin = Add(Add(origin, Multiply(xVector, column)), Multiply(yVector, row));
                    var color = facelets is null
                        ? new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                        : GetStickerBrush(facelets[faceStart + row * 3 + column]);

                    var polygon = new Polygon
                    {
                        Points =
                        {
                            stickerOrigin,
                            Add(stickerOrigin, xVector),
                            Add(Add(stickerOrigin, xVector), yVector),
                            Add(stickerOrigin, yVector)
                        },
                        Fill = color,
                        Stroke = Application.Current.Resources["TextFillColorTertiaryBrush"] as Brush,
                        StrokeThickness = 1
                    };
                    canvas.Children.Add(polygon);
                }
            }
        }

        private static Windows.Foundation.Point Multiply(Windows.Foundation.Point point, double factor)
        {
            return new Windows.Foundation.Point(point.X * factor, point.Y * factor);
        }

        private static Windows.Foundation.Point Add(Windows.Foundation.Point left, Windows.Foundation.Point right)
        {
            return new Windows.Foundation.Point(left.X + right.X, left.Y + right.Y);
        }

        private static SolidColorBrush GetStickerBrush(char facelet)
        {
            return facelet switch
            {
                'U' => new SolidColorBrush(Microsoft.UI.Colors.White),
                'R' => new SolidColorBrush(Microsoft.UI.Colors.Red),
                'F' => new SolidColorBrush(Microsoft.UI.Colors.LimeGreen),
                'D' => new SolidColorBrush(Microsoft.UI.Colors.Gold),
                'L' => new SolidColorBrush(Microsoft.UI.Colors.Orange),
                'B' => new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue),
                _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
            };
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
            BluetoothFlyoutStatusText.Text = _strings.BluetoothScanningMessage;
            ResetCubeStateButton.Content = _strings.BluetoothResetCubeState;
            DisconnectCubeButton.Content = _strings.BluetoothDisconnect;
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
