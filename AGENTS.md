# AGENTS.md

## Project

SharpTimer 是一个支持智能魔方的原生 Windows 桌面魔方计时器。

当前项目使用：

- .NET 8 / C#
- WinUI 3 / Windows App SDK / XAML
- SQLite
- Windows BLE API
- xUnit

当前主要模块：

- `SharpTimer.App`：WinUI 3 客户端，负责界面、输入事件和展示状态。
- `SharpTimer.Core`：平台无关的计时状态机、成绩模型、罚时模型、统计和智能打乱推进规则。
- `SharpTimer.Storage`：SQLite schema、迁移入口和仓储实现。
- `SharpTimer.Bluetooth`：Windows BLE 智能魔方接入、协议抽象和 MoYu32 连接入口。
- `SharpTimer.Tests`：核心计时、统计、模型和存储测试。
- `docs/`：架构、路线图和阶段状态说明。
- `ref/`：外部参考资料，仅在迁移或比对行为时使用。

当前稳定能力包括基础手动计时、观察、判罚、session 管理、SQLite 持久化、基础统计、主题/背景材质/中英文设置，以及 MoYu32 系列智能魔方的连接和智能打乱推进。

## Development Rules

- 开始功能开发、修复或较大改动前，先阅读 `docs/architecture.md` 和 `docs/roadmap.md`。
- 代码事实优先于文档；如果发现文档与代码不一致，完成改动时同步更新相关 `docs/`。
- 优先使用官方 WinUI 3 / Windows App SDK 控件、API、样式资源和交互模式。
- UI 控件、窗口 API、布局样式或 Fluent 交互不确定时，先查 `ref/WinUI-Gallery`；涉及窗口生命周期、通知、部署或 Windows App SDK 场景时，再参考官方 WindowsAppSDK samples 或 Microsoft Learn。
- 不要手写伪 Fluent 控件、自制按钮、自制弹窗、自制导航或 CSS 风格设计系统。
- 小改动可以沿用现有 `MainWindow.xaml.cs` 编排；新增复杂状态、流程或跨层逻辑时，优先抽到 service、ViewModel 或独立视图文件。

## WinUI App Rules

- SharpTimer 是现有 WinUI 3 项目，不要按新项目脚手架重建结构；除非会枝明确要求，不运行会改动开发机环境的 WinUI 安装、配置或 `winget configure` 流程。
- 修改 `SharpTimer.App` 前，先判断任务属于界面设计、实现、排障、性能、可访问性、本地化、打包或启动验证，并按对应范围查最窄的参考资料。
- 保持当前打包模型和项目文件约束；涉及 MSIX、证书、部署、直接运行路径或启动方式时，先明确 packaged / unpackaged 影响，不要混用两套假设。
- grouped command surface 优先使用官方 `CommandBar`、`AppBarButton`、`MenuFlyout`、`TeachingTip`、`ContentDialog` 等 WinUI 控件；只有官方控件无法清晰表达时，才考虑组合控件或 CommunityToolkit。
- 自定义视觉时优先组合、模板化或重设内置控件样式；避免额外套壳的 `Border`、双层卡片、硬编码亮色/暗色、伪 Fluent 颜色系统。
- 主题相关改动默认同时支持亮色、暗色和当前背景材质，优先使用 `ThemeResource`、系统画刷和 Windows App SDK backdrop 能力。
- 布局改动要明确宽屏、中等窗口和窄窗口行为，尤其是 `NavigationView`、内容 padding、成绩列表、设置项和底部/侧边操作区。
- 集合和滚动区域要明确滚动所有权；页面已有垂直滚动时，避免嵌套集合控件抢滚动或导致内容不可见。
- 键盘输入、可访问性和本地化要随 UI 改动一起检查：空格计时路径不能被普通控件焦点破坏，交互控件应有清楚的 `AutomationProperties.Name`，新增文本需兼顾中英文。
- 遇到 `MSB3073`、`XamlCompiler.exe`、XAML 生成失败或启动异常时，优先回到当前 WinUI 模板基线比对项目文件、XAML 结构、资源字典和打包配置，再做最小修复。

## Architecture Boundaries

- 计时规则、观察、罚时、统计和平台无关的智能打乱推进逻辑放在 `SharpTimer.Core`。
- SQLite schema、迁移和 SQL 访问放在 `SharpTimer.Storage`。
- BLE 扫描、连接、通知订阅、协议解析和设备命令放在 `SharpTimer.Bluetooth`。
- WinUI 输入事件、界面渲染、本地设置和跨层编排放在 `SharpTimer.App` / `SharpTimer.App.Services`。
- 新增核心规则、存储行为或平台无关智能魔方逻辑时，优先补 `SharpTimer.Tests`。

## Behavior Constraints

- 空格键是核心手动计时路径，改动后必须确认开始观察、开始复原、停止计时仍可用。
- 15 秒观察、`+2`、`DNF` 逻辑属于核心规则，应保持可测试。
- 智能魔方未完成打乱时只推进打乱或提示纠错，不应提前启动计时。
- 智能魔方只有进入 `READY` 后，首个有效转动才应启动复原计时。
- BLE 厂商兼容性特例应隔离在 `SharpTimer.Bluetooth`，不要散落到 UI code-behind。
- 删除、归档、罚时修改等破坏性或半破坏性操作应使用官方确认控件。

## README

- `README.md` 是默认中文入口。
- `README-en.md` 是英文版，应严格同步 `README.md` 的结构和含义。
- README 顶部图标使用 `SharpTimer.App/Assets/Square150x150Logo.scale-200.png`。
- README 应只写稳定功能、技术栈、预览和致谢，不写临时进度流水账。

## Verification

按影响范围验证：

- 核心规则变更：运行 `dotnet test SharpTimer.slnx`。
- 存储变更：补充或更新 `SharpTimer.Tests/Storage` 测试。
- UI/XAML 变更：运行 `dotnet build SharpTimer.slnx`，并尽量启动 App，确认出现响应式顶层窗口、窗口标题正确且核心页面可见；只看到进程启动不算完整验证。
- UI 交互变更：除构建和启动外，手动确认空格开始观察、开始复原、停止计时仍可用，并检查改动涉及的亮/暗主题、中英文文本和窄窗口布局。
- XAML 编译或启动排障：修复后至少重新运行 `dotnet build SharpTimer.slnx`；如果问题发生在启动阶段，还要真实启动验证。
- 文档或 README 变更：检查链接、图片路径和项目描述是否准确。

常用命令：

```powershell
dotnet restore SharpTimer.slnx
dotnet build SharpTimer.slnx
dotnet test SharpTimer.slnx
```

打包命令：

```
.\scripts\package-release.ps1
```

## Git And Safety

- 不要回滚用户已有改动，除非用户明确要求。
- 大量删除文件、风险性脚本、推送代码或部署前，先说明计划、影响文件和风险，并等待确认。
- 小范围文档、代码和测试更新可直接执行。
- 提交信息使用中文开发者视角。
