namespace SharpTimer.App.Services;

public sealed record LocalizedStrings
{
    public required string TimerNav { get; init; }
    public required string SolvesNav { get; init; }
    public required string SettingsNav { get; init; }
    public required string SessionHeader { get; init; }
    public required string NewSession { get; init; }
    public required string RenameSession { get; init; }
    public required string ArchiveSession { get; init; }
    public required string TimeColumn { get; init; }
    public required string PenaltyColumn { get; init; }
    public required string ClearPenalty { get; init; }
    public required string Delete { get; init; }
    public required string SettingsTitle { get; init; }
    public required string SettingsDescription { get; init; }
    public required string InspectionHeader { get; init; }
    public required string PrecisionHeader { get; init; }
    public required string Centiseconds { get; init; }
    public required string Milliseconds { get; init; }
    public required string ThemeHeader { get; init; }
    public required string SystemTheme { get; init; }
    public required string LightTheme { get; init; }
    public required string DarkTheme { get; init; }
    public required string LanguageHeader { get; init; }
    public required string ChineseLanguage { get; init; }
    public required string EnglishLanguage { get; init; }
    public required string InspectionRemainingFormat { get; init; }
    public required string CountFormat { get; init; }
    public required string NewSessionDialogTitle { get; init; }
    public required string NewSessionDefaultName { get; init; }
    public required string RenameSessionDialogTitle { get; init; }
    public required string SessionNamePlaceholder { get; init; }
    public required string Save { get; init; }
    public required string Cancel { get; init; }
    public required string ArchiveSessionDialogTitle { get; init; }
    public required string ArchiveSessionDialogContent { get; init; }
    public required string Archive { get; init; }
    public required string BluetoothTitle { get; init; }
    public required string BluetoothScanningMessage { get; init; }
    public required string BluetoothUnknownDevice { get; init; }
    public required string BluetoothUnknownProtocol { get; init; }
    public required string BluetoothNoServices { get; init; }
    public required string BluetoothServicesSummaryFormat { get; init; }
    public required string BluetoothSignalFormat { get; init; }
    public required string BluetoothConnectingMessage { get; init; }
    public required string BluetoothConnectFailedFormat { get; init; }
    public required string BluetoothConnectedMessage { get; init; }
    public required string BluetoothDisconnectedMessage { get; init; }
    public required string BluetoothDisconnect { get; init; }
    public required string BluetoothBatteryFormat { get; init; }
    public required string BluetoothBatteryUnknown { get; init; }
    public required string BluetoothWaitingState { get; init; }
    public required string BluetoothSmartModeFormat { get; init; }
    public required string BluetoothLastMoveFormat { get; init; }
    public required string BluetoothSolvedState { get; init; }
    public required string BluetoothStateSynced { get; init; }

    public static LocalizedStrings For(AppLanguagePreference language)
    {
        return language == AppLanguagePreference.English ? English : Chinese;
    }

    private static readonly LocalizedStrings Chinese = new()
    {
        TimerNav = "计时",
        SolvesNav = "成绩",
        SettingsNav = "设置",
        SessionHeader = "Session",
        NewSession = "新建",
        RenameSession = "重命名",
        ArchiveSession = "归档",
        TimeColumn = "时间",
        PenaltyColumn = "判罚",
        ClearPenalty = "清除罚时",
        Delete = "删除",
        SettingsTitle = "设置",
        SettingsDescription = "本地偏好会立即生效，并保存在当前 Windows 用户数据里",
        InspectionHeader = "15 秒观察",
        PrecisionHeader = "显示精度",
        Centiseconds = "百分秒",
        Milliseconds = "毫秒",
        ThemeHeader = "主题",
        SystemTheme = "跟随系统",
        LightTheme = "亮色",
        DarkTheme = "暗色",
        LanguageHeader = "语言",
        ChineseLanguage = "中文",
        EnglishLanguage = "English",
        InspectionRemainingFormat = "观察剩余 {0:0}s",
        CountFormat = "{0} 次",
        NewSessionDialogTitle = "新建 session",
        NewSessionDefaultName = "Session name",
        RenameSessionDialogTitle = "重命名 session",
        SessionNamePlaceholder = "例如 Main、OH、练习 A",
        Save = "保存",
        Cancel = "取消",
        ArchiveSessionDialogTitle = "归档当前 session",
        ArchiveSessionDialogContent = "归档后不会出现在 session 列表里，成绩仍保存在本地数据库中。",
        Archive = "归档",
        BluetoothTitle = "智能魔方",
        BluetoothScanningMessage = "附近 BLE 广播设备会逐步出现在列表中。",
        BluetoothUnknownDevice = "未知设备",
        BluetoothUnknownProtocol = "未知",
        BluetoothNoServices = "未广播服务",
        BluetoothServicesSummaryFormat = "{0} 个服务",
        BluetoothSignalFormat = "{0} dBm",
        BluetoothConnectingMessage = "正在连接魔方...",
        BluetoothConnectFailedFormat = "连接失败：{0}",
        BluetoothConnectedMessage = "已进入智能魔方模式",
        BluetoothDisconnectedMessage = "蓝牙魔方已断开",
        BluetoothDisconnect = "断开连接",
        BluetoothBatteryFormat = "电量：{0}%",
        BluetoothBatteryUnknown = "电量：--",
        BluetoothWaitingState = "等待魔方状态...",
        BluetoothSmartModeFormat = "智能魔方模式：{0}",
        BluetoothLastMoveFormat = "最近转动：{0}",
        BluetoothSolvedState = "已复原",
        BluetoothStateSynced = "状态已同步"
    };

    private static readonly LocalizedStrings English = new()
    {
        TimerNav = "Timer",
        SolvesNav = "Solves",
        SettingsNav = "Settings",
        SessionHeader = "Session",
        NewSession = "New",
        RenameSession = "Rename",
        ArchiveSession = "Archive",
        TimeColumn = "Time",
        PenaltyColumn = "Penalty",
        ClearPenalty = "Clear penalty",
        Delete = "Delete",
        SettingsTitle = "Settings",
        SettingsDescription = "Local preferences apply immediately and are saved to your Windows user data.",
        InspectionHeader = "15-second inspection",
        PrecisionHeader = "Precision",
        Centiseconds = "Centiseconds",
        Milliseconds = "Milliseconds",
        ThemeHeader = "Theme",
        SystemTheme = "Use system setting",
        LightTheme = "Light",
        DarkTheme = "Dark",
        LanguageHeader = "Language",
        ChineseLanguage = "中文",
        EnglishLanguage = "English",
        InspectionRemainingFormat = "Inspection left {0:0}s",
        CountFormat = "{0} solves",
        NewSessionDialogTitle = "New session",
        NewSessionDefaultName = "Session name",
        RenameSessionDialogTitle = "Rename session",
        SessionNamePlaceholder = "For example Main, OH, Practice A",
        Save = "Save",
        Cancel = "Cancel",
        ArchiveSessionDialogTitle = "Archive current session",
        ArchiveSessionDialogContent = "Archived sessions disappear from the session list, but their solves stay in the local database.",
        Archive = "Archive",
        BluetoothTitle = "Smart cube",
        BluetoothScanningMessage = "Nearby BLE advertisements will appear in the list.",
        BluetoothUnknownDevice = "Unknown device",
        BluetoothUnknownProtocol = "Unknown",
        BluetoothNoServices = "No advertised services",
        BluetoothServicesSummaryFormat = "{0} services",
        BluetoothSignalFormat = "{0} dBm",
        BluetoothConnectingMessage = "Connecting cube...",
        BluetoothConnectFailedFormat = "Connection failed: {0}",
        BluetoothConnectedMessage = "Smart cube mode is active",
        BluetoothDisconnectedMessage = "Bluetooth cube disconnected",
        BluetoothDisconnect = "Disconnect",
        BluetoothBatteryFormat = "Battery: {0}%",
        BluetoothBatteryUnknown = "Battery: --",
        BluetoothWaitingState = "Waiting for cube state...",
        BluetoothSmartModeFormat = "Smart cube mode: {0}",
        BluetoothLastMoveFormat = "Last move: {0}",
        BluetoothSolvedState = "Solved",
        BluetoothStateSynced = "State synced"
    };
}
