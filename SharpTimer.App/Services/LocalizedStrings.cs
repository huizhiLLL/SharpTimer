namespace SharpTimer.App.Services;

public sealed record LocalizedStrings
{
    public required string TimerNav { get; init; }
    public required string SolvesNav { get; init; }
    public required string SettingsNav { get; init; }
    public required string NewSession { get; init; }
    public required string RenameSession { get; init; }
    public required string ArchiveSession { get; init; }
    public required string SessionActions { get; init; }
    public required string TimeColumn { get; init; }
    public required string Delete { get; init; }
    public required string Close { get; init; }
    public required string BestLabel { get; init; }
    public required string WorstLabel { get; init; }
    public required string MeanLabel { get; init; }
    public required string CompletedCountLabel { get; init; }
    public required string BestAverageOf5Label { get; init; }
    public required string BestAverageOf12Label { get; init; }
    public required string AnalysisCountLabel { get; init; }
    public required string SolveTrendTitle { get; init; }
    public required string SolveDistributionTitle { get; init; }
    public required string SolveChartEmptyText { get; init; }
    public required string SolveDetailsTitleFormat { get; init; }
    public required string SolveTimeLabel { get; init; }
    public required string SolveScrambleLabel { get; init; }
    public required string SolveReplayLabel { get; init; }
    public required string SolveReplayUnavailable { get; init; }
    public required string SolveMoveCountLabel { get; init; }
    public required string SolveTpsLabel { get; init; }
    public required string SolveCommentLabel { get; init; }
    public required string SolveCommentPlaceholder { get; init; }
    public required string EmptySolvesTitle { get; init; }
    public required string EmptySolvesDescription { get; init; }
    public required string DeleteSolveDialogTitle { get; init; }
    public required string DeleteSolveDialogContent { get; init; }
    public required string SettingsTitle { get; init; }
    public required string SettingsDescription { get; init; }
    public required string SettingsTimingSectionTitle { get; init; }
    public required string SettingsTimingSectionDescription { get; init; }
    public required string SettingsAppearanceSectionTitle { get; init; }
    public required string SettingsAppearanceSectionDescription { get; init; }
    public required string SettingsLanguageSectionTitle { get; init; }
    public required string SettingsLanguageSectionDescription { get; init; }
    public required string TitleBarToggleNavigation { get; init; }
    public required string BluetoothButtonName { get; init; }
    public required string BluetoothDevicesListName { get; init; }
    public required string SmartCubePreviewName { get; init; }
    public required string InspectionHeader { get; init; }
    public required string PrecisionHeader { get; init; }
    public required string Centiseconds { get; init; }
    public required string Milliseconds { get; init; }
    public required string ThemeHeader { get; init; }
    public required string SystemTheme { get; init; }
    public required string LightTheme { get; init; }
    public required string DarkTheme { get; init; }
    public required string BackdropMaterialHeader { get; init; }
    public required string MicaMaterial { get; init; }
    public required string MicaAltMaterial { get; init; }
    public required string AcrylicMaterial { get; init; }
    public required string LanguageHeader { get; init; }
    public required string ChineseLanguage { get; init; }
    public required string EnglishLanguage { get; init; }
    public required string SmartCubeSectionTitle { get; init; }
    public required string ScrambleProgressStyleHeader { get; init; }
    public required string ScrambleProgressHideCompleted { get; init; }
    public required string ScrambleProgressDimCompleted { get; init; }
    public required string SolveMethodHeader { get; init; }
    public required string SolveMethodCfop { get; init; }
    public required string SolveMethodRoux { get; init; }
    public required string ScrambleFontSizeHeader { get; init; }
    public required string SmartCubePreviewSizeHeader { get; init; }
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
    public required string BluetoothScanningMessage { get; init; }
    public required string BluetoothUnknownDevice { get; init; }
    public required string BluetoothUnknownProtocol { get; init; }
    public required string BluetoothNoServices { get; init; }
    public required string BluetoothServicesSummaryFormat { get; init; }
    public required string BluetoothConnectingMessage { get; init; }
    public required string BluetoothConnectFailedFormat { get; init; }
    public required string BluetoothRetryScan { get; init; }
    public required string BluetoothConnectedMessage { get; init; }
    public required string BluetoothDisconnectedMessage { get; init; }
    public required string BluetoothDisconnect { get; init; }
    public required string BluetoothResetCubeState { get; init; }
    public required string BluetoothResetCubeOrientation { get; init; }
    public required string BluetoothBatteryFormat { get; init; }
    public required string BluetoothBatteryUnknown { get; init; }
    public required string BluetoothScrambleReady { get; init; }
    public required string BluetoothScrambleRestoreRequired { get; init; }
    public required string SolvePhasesLabel { get; init; }
    public required string SolvePhaseTimeColumn { get; init; }
    public required string SolvePhaseMovesColumn { get; init; }
    public required string SolvePhaseTpsColumn { get; init; }

    public static LocalizedStrings For(AppLanguagePreference language)
    {
        return language == AppLanguagePreference.English ? English : Chinese;
    }

    private static readonly LocalizedStrings Chinese = new()
    {
        TimerNav = "计时",
        SolvesNav = "成绩",
        SettingsNav = "设置",
        NewSession = "新建",
        RenameSession = "重命名",
        ArchiveSession = "归档",
        SessionActions = "Session 操作",
        TimeColumn = "时间",
        Delete = "删除",
        Close = "关闭",
        BestLabel = "Best",
        WorstLabel = "Worst",
        MeanLabel = "Average",
        CompletedCountLabel = "Solves",
        BestAverageOf5Label = "Best Ao5",
        BestAverageOf12Label = "Best Ao12",
        AnalysisCountLabel = "次数",
        SolveTrendTitle = "趋势",
        SolveDistributionTitle = "分布",
        SolveChartEmptyText = "有效成绩不足",
        SolveDetailsTitleFormat = "Solves {0}",
        SolveTimeLabel = "时间",
        SolveScrambleLabel = "打乱",
        SolveReplayLabel = "解法",
        SolveReplayUnavailable = "无",
        SolveMoveCountLabel = "步数",
        SolveTpsLabel = "TPS",
        SolveCommentLabel = "备注",
        SolveCommentPlaceholder = "添加复盘备注",
        EmptySolvesTitle = "还没有成绩",
        EmptySolvesDescription = "连接智能魔方并完成打乱后，READY 首转会自动开始计时。",
        DeleteSolveDialogTitle = "删除这条成绩",
        DeleteSolveDialogContent = "删除后无法从应用内恢复。",
        SettingsTitle = "设置",
        SettingsDescription = "本地偏好会立即生效，并保存在当前 Windows 用户数据里",
        SettingsTimingSectionTitle = "智能计时",
        SettingsTimingSectionDescription = "控制智能魔方观察和成绩显示方式。",
        SettingsAppearanceSectionTitle = "外观",
        SettingsAppearanceSectionDescription = "选择窗口主题和背景材质。",
        SettingsLanguageSectionTitle = "语言",
        SettingsLanguageSectionDescription = "切换界面语言。",
        TitleBarToggleNavigation = "展开或折叠导航",
        BluetoothButtonName = "智能魔方蓝牙",
        BluetoothDevicesListName = "智能魔方设备",
        SmartCubePreviewName = "智能魔方预览",
        InspectionHeader = "15 秒观察",
        PrecisionHeader = "显示精度",
        Centiseconds = "百分秒",
        Milliseconds = "毫秒",
        ThemeHeader = "主题",
        SystemTheme = "跟随系统",
        LightTheme = "亮色",
        DarkTheme = "暗色",
        BackdropMaterialHeader = "背景材质",
        MicaMaterial = "Mica",
        MicaAltMaterial = "Mica Alt",
        AcrylicMaterial = "Acrylic",
        LanguageHeader = "语言",
        ChineseLanguage = "中文",
        EnglishLanguage = "English",
        SmartCubeSectionTitle = "智能魔方",
        ScrambleProgressStyleHeader = "打乱推进样式",
        ScrambleProgressHideCompleted = "自动消失",
        ScrambleProgressDimCompleted = "变浅保留",
        SolveMethodHeader = "解法分段",
        SolveMethodCfop = "CFOP",
        SolveMethodRoux = "Roux",
        ScrambleFontSizeHeader = "打乱字体",
        SmartCubePreviewSizeHeader = "虚拟魔方大小",
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
        BluetoothScanningMessage = "自动扫描附近的蓝牙设备...",
        BluetoothUnknownDevice = "未知设备",
        BluetoothUnknownProtocol = "未知",
        BluetoothNoServices = "未广播服务",
        BluetoothServicesSummaryFormat = "{0} 个服务",
        BluetoothConnectingMessage = "正在连接魔方...",
        BluetoothConnectFailedFormat = "连接失败：{0}",
        BluetoothRetryScan = "重新扫描",
        BluetoothConnectedMessage = "已连接智能魔方",
        BluetoothDisconnectedMessage = "智能魔方已断开",
        BluetoothDisconnect = "断开连接",
        BluetoothResetCubeState = "重置状态",
        BluetoothResetCubeOrientation = "重置姿态",
        BluetoothBatteryFormat = "电量：{0}%",
        BluetoothBatteryUnknown = "电量：--",
        BluetoothScrambleReady = "READY",
        BluetoothScrambleRestoreRequired = "请先复原魔方",
        SolvePhasesLabel = "分段",
        SolvePhaseTimeColumn = "Time",
        SolvePhaseMovesColumn = "Moves",
        SolvePhaseTpsColumn = "TPS"
    };

    private static readonly LocalizedStrings English = new()
    {
        TimerNav = "Timer",
        SolvesNav = "Solves",
        SettingsNav = "Settings",
        NewSession = "New",
        RenameSession = "Rename",
        ArchiveSession = "Archive",
        SessionActions = "Session actions",
        TimeColumn = "Time",
        Delete = "Delete",
        Close = "Close",
        BestLabel = "Best",
        WorstLabel = "Worst",
        MeanLabel = "Average",
        CompletedCountLabel = "Completed",
        BestAverageOf5Label = "Best Ao5",
        BestAverageOf12Label = "Best Ao12",
        AnalysisCountLabel = "Solves",
        SolveTrendTitle = "Trend",
        SolveDistributionTitle = "Distribution",
        SolveChartEmptyText = "Not enough valid solves",
        SolveDetailsTitleFormat = "Solves {0}",
        SolveTimeLabel = "Time",
        SolveScrambleLabel = "Scramble",
        SolveReplayLabel = "Solution",
        SolveReplayUnavailable = "Nothing",
        SolveMoveCountLabel = "Moves",
        SolveTpsLabel = "TPS",
        SolveCommentLabel = "Note",
        SolveCommentPlaceholder = "Add review notes",
        EmptySolvesTitle = "No solves yet",
        EmptySolvesDescription = "Connect a smart cube, finish the scramble, then the first READY turn starts timing automatically.",
        DeleteSolveDialogTitle = "Delete this solve",
        DeleteSolveDialogContent = "Deleted solves cannot be restored from the app.",
        SettingsTitle = "Settings",
        SettingsDescription = "Local preferences apply immediately and are saved to your Windows user data.",
        SettingsTimingSectionTitle = "Smart timing",
        SettingsTimingSectionDescription = "Control smart cube inspection and solve time display.",
        SettingsAppearanceSectionTitle = "Appearance",
        SettingsAppearanceSectionDescription = "Choose the window theme and backdrop material.",
        SettingsLanguageSectionTitle = "Language",
        SettingsLanguageSectionDescription = "Switch the interface language.",
        TitleBarToggleNavigation = "Toggle navigation",
        BluetoothButtonName = "Smart cube Bluetooth",
        BluetoothDevicesListName = "Smart cube devices",
        SmartCubePreviewName = "Smart cube preview",
        InspectionHeader = "15-second inspection",
        PrecisionHeader = "Precision",
        Centiseconds = "Centiseconds",
        Milliseconds = "Milliseconds",
        ThemeHeader = "Theme",
        SystemTheme = "Use system setting",
        LightTheme = "Light",
        DarkTheme = "Dark",
        BackdropMaterialHeader = "Background material",
        MicaMaterial = "Mica",
        MicaAltMaterial = "Mica Alt",
        AcrylicMaterial = "Acrylic",
        LanguageHeader = "Language",
        ChineseLanguage = "中文",
        EnglishLanguage = "English",
        SmartCubeSectionTitle = "Smart cube",
        ScrambleProgressStyleHeader = "Scramble progress style",
        ScrambleProgressHideCompleted = "Hide completed moves",
        ScrambleProgressDimCompleted = "Dim completed moves",
        SolveMethodHeader = "Solve method",
        SolveMethodCfop = "CFOP",
        SolveMethodRoux = "Roux",
        ScrambleFontSizeHeader = "Scramble font size",
        SmartCubePreviewSizeHeader = "Virtual cube size",
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
        BluetoothScanningMessage = "Nearby BLE advertisements will appear in the list.",
        BluetoothUnknownDevice = "Unknown device",
        BluetoothUnknownProtocol = "Unknown",
        BluetoothNoServices = "No advertised services",
        BluetoothServicesSummaryFormat = "{0} services",
        BluetoothConnectingMessage = "Connecting cube...",
        BluetoothConnectFailedFormat = "Connection failed: {0}",
        BluetoothRetryScan = "Scan again",
        BluetoothConnectedMessage = "Smart cube connected",
        BluetoothDisconnectedMessage = "Bluetooth cube disconnected",
        BluetoothDisconnect = "Disconnect",
        BluetoothResetCubeState = "Reset state",
        BluetoothResetCubeOrientation = "Reset posture",
        BluetoothBatteryFormat = "Battery: {0}%",
        BluetoothBatteryUnknown = "Battery: --",
        BluetoothScrambleReady = "READY",
        BluetoothScrambleRestoreRequired = "Solve the cube first",
        SolvePhasesLabel = "Phases",
        SolvePhaseTimeColumn = "Time",
        SolvePhaseMovesColumn = "Moves",
        SolvePhaseTpsColumn = "TPS"
    };
}
