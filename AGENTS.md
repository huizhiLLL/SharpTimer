# AGENTS.md

## Project

SharpTimer 是一个支持智能魔方的原生 Windows 桌面魔方计时器。当前本地手动计时闭环已经建立，智能魔方 BLE 已有 MoYu32 系列接入入口，后续重点是继续验证实机体验、稳定智能打乱推进，并逐步扩展更多智能魔方协议。

当前项目使用：

- .NET 8
- C#
- WinUI 3 / Windows App SDK
- XAML
- SQLite
- xUnit 测试

当前工程结构：

- `SharpTimer.App`：WinUI 3 客户端，负责界面、输入事件和展示状态。
- `SharpTimer.Core`：平台无关的计时状态机、成绩模型、罚时模型和统计计算。
- `SharpTimer.Storage`：SQLite schema、迁移入口和仓储实现。
- `SharpTimer.Bluetooth`：Windows BLE 智能魔方接入、协议抽象和 MoYu32 连接入口。
- `SharpTimer.Tests`：核心计时、统计、模型和存储测试。
- `docs/`：架构、路线图和阶段状态说明。
- `ref/`：外部参考资料，仅在需要迁移或比对行为时使用。

当前已经支持基础手动计时、观察、判罚、session 管理、SQLite 持久化、基础统计、主题/背景材质/中英文设置，以及 MoYu32 系列智能魔方的连接、智能打乱推进、READY 后首转起表和复原完成保存成绩。

重要本地参考：

- `ref/WinUI-Gallery`：官方 WinUI Gallery 示例源码，是前端控件选择、XAML 写法、WinUI API 使用、样式资源和交互模式的优先参考。
- `ref/smartcube-web-bluetooth`：智能魔方 BLE 协议迁移参考。
- `ref/cstimer`：计时状态、观察、罚时、统计、session 概念和智能魔方协议行为参考。

## Core Rule

优先使用官方 WinUI 3 / Windows App SDK 控件、API、样式资源和交互模式。

不要手写伪 Fluent 控件、伪卡片系统、自制按钮、自制弹窗、自制导航、自制开关或 CSS 风格的样式系统，除非项目里已经有明确文档化的可复用组件，并且现有官方控件无法满足需求。

实现 UI 前先确认：

1. 用户真正要完成的行为是什么。
2. WinUI 3 是否已有合适官方控件或模式。
3. `ref/WinUI-Gallery` 是否有对应控件、页面模式、样式资源或 API 示例。
4. 当前项目是否已经有可复用实现。
5. 是否能用最简单的官方控件组合完成。
6. 是否需要同步更新 `docs/`。

## Documentation First

在开始功能开发、修复或较大改动前，先阅读 `docs/` 下的相关文档：

- `docs/architecture.md`：项目分层、核心模型、SQLite schema、BLE 边界。
- `docs/roadmap.md`：当前阶段、后续阶段和范围判断。

如果 `docs/` 与代码不一致：

- 代码事实优先用于判断当前行为。
- 进度、阶段、方案类内容应同步更新到文档。
- README 只写项目特点、使用方式和稳定事实，不写临时进度流水账。

参考优先级：

1. 当前项目代码
2. `docs/` 文档
3. `ref/WinUI-Gallery` 官方本地示例，适用于 WinUI 控件、XAML、样式资源、窗口和交互 API
4. `ref/` 中其他领域参考资料
5. Microsoft Learn、Windows App SDK 文档和线上 WinUI Gallery 资料

## Architecture Boundaries

保持分层清楚：

- 计时规则、观察时间、罚时判定和统计规则放在 `SharpTimer.Core`。
- 智能魔方打乱推进中不依赖 Windows BLE 的规则部分优先放在 `SharpTimer.Core`，保持可测试。
- SQLite schema、迁移和 SQL 访问放在 `SharpTimer.Storage`。
- Windows BLE 扫描、连接、通知订阅、协议解析和设备命令放在 `SharpTimer.Bluetooth`。
- WinUI 输入事件、界面渲染和本地设置串联放在 `SharpTimer.App`。
- 跨层编排放在 `SharpTimer.App.Services`，不要把核心规则直接塞进 XAML code-behind。
- 新增核心规则时优先补 `SharpTimer.Tests`。

当前 `MainWindow.xaml.cs` 仍承担较多界面编排工作。小改动可以沿用现状；如果新增较复杂页面、状态或流程，应优先抽到 service、ViewModel 或独立视图文件中，逐步减轻 `MainWindow`。

## WinUI 3 Controls

优先使用这些官方控件：

- `NavigationView`：应用导航。
- `CommandBar` / `AppBarButton`：页面动作和工具栏。
- `Button` / `ToggleButton` / `HyperlinkButton`：基础操作。
- `ListView` / `GridView` / `ItemsRepeater`：列表和网格数据。
- `DataTemplate`：列表项展示。
- `ContentDialog`：确认、重命名、新建等模态交互。
- `InfoBar` / `TeachingTip`：轻量提示和引导。
- `NumberBox` / `TextBox` / `ComboBox` / `ToggleSwitch` / `CheckBox`：输入。
- `MenuFlyout` / `Flyout`：上下文菜单和轻量操作。
- `ProgressRing` / `ProgressBar`：加载状态。
- `Grid` / `StackPanel` / `ScrollViewer`：布局。
- `Border`：只用于简单容器或视觉分组，不要扩展成自制组件体系。

实现或调整控件前，优先在 `ref/WinUI-Gallery/WinUIGallery` 中查找同类示例。常用入口包括：

- `ref/WinUI-Gallery/WinUIGallery/MainWindow.xaml`：官方 Gallery 自身的窗口、导航和应用框架写法。
- `ref/WinUI-Gallery/WinUIGallery/Controls`：示例页包装、辅助控件和设计指导控件。
- `ref/WinUI-Gallery/WinUIGallery/Styles`：官方示例里的样式资源组织方式。
- `ref/WinUI-Gallery/WinUIGallery/Pages` 和 `ref/WinUI-Gallery/WinUIGallery/Samples`：具体控件示例，按控件名用 `rg` 查找。

引用 WinUI Gallery 时，只借鉴官方控件组合、API 调用和资源用法；不要直接搬入 Gallery 专用的演示框架、示例代码展示控件或与 SharpTimer 无关的复杂结构。

可以接受的当前模式：

- 主界面继续使用官方 `NavigationView`。
- 成绩列表继续使用 `ListView` + `DataTemplate`。
- 设置项继续使用 `ToggleSwitch`、`ComboBox` 等官方输入控件。
- 简单统计块可以用 `Border` + `ThemeResource` 做轻量分组，但不要变成可点击自制卡片控件。

禁止优先采用的做法：

- 用 `Border + TextBlock` 自制按钮。
- 用 `Grid + PointerPressed` 自制可点击列表项。
- 手写模态遮罩替代 `ContentDialog`。
- 手写侧边栏替代 `NavigationView`。
- 用矩形和动画重做 `ToggleSwitch`。
- 为常规控件硬编码整套颜色、圆角和阴影。

## Styling Rules

样式应贴近 Windows 11 原生体验：

- 优先使用 `ThemeResource`。
- 优先使用 WinUI 内置排版、颜色和控件样式。
- 尊重亮色、暗色和高对比度模式。
- 避免硬编码颜色；确实需要时说明局部原因。
- 避免为普通控件创建大体量自定义模板。
- 不要引入 CSS 式设计系统或伪 Fluent 组件库。
- 页面布局可以简洁，但键盘操作和屏幕阅读器名称不能被牺牲。

## XAML And Code-Behind

- UI 结构优先写在 XAML。
- code-behind 保持薄一些，主要处理 WinUI 事件桥接、焦点和调用服务。
- 状态、规则、存储和可测试逻辑放到服务或 Core/Storage。
- 列表使用 `ObservableCollection`。
- 绑定方式跟随现有项目风格，避免同一局部混用多套复杂模式。
- 当前项目未引入 CommunityToolkit.Mvvm；除非收益明确，不要只为小功能新增依赖。

## WinUI 3 API Correctness

- 使用 `Microsoft.UI.Xaml` 命名空间，不使用 `Windows.UI.Xaml`。
- UI 线程调度使用 `Microsoft.UI.Dispatching.DispatcherQueue`。
- 窗口相关能力使用 Windows App SDK 适配的 `Window` / `AppWindow` API。
- 不要复制 UWP-only API，除非已确认 WinUI 3 可用。
- 不要使用 WPF、MAUI 或 Avalonia API。
- 不要把 Web/CSS 思路直接套进 XAML。
- 对不确定的控件属性、视觉状态、窗口 API 或资源键，先查 `ref/WinUI-Gallery` 的官方示例，再决定是否需要查线上文档。

## Timer Behavior

计时器交互要优先稳定：

- 空格键是核心输入路径，改动后必须确认开始观察、开始复原、停止计时仍可用。
- 15 秒观察、`+2`、`DNF` 逻辑属于核心规则，应在 `SharpTimer.Core` 保持可测试。
- UI 刷新不能阻塞计时状态机。
- 删除、归档、罚时修改等破坏性或半破坏性操作应使用官方确认控件。
- 数据持久化路径和 SQLite schema 变更必须同步测试和文档。

## Smart Cube Behavior

智能魔方相关改动要优先保证手动计时路径不回退：

- MoYu32 是当前已经接入的智能魔方系列，新增 BLE 行为前先确认不会破坏现有 MoYu32 连接、断开、状态同步和打乱推进流程。
- 未完成打乱时，智能魔方转动只推进打乱进度或给出纠错提示，不应提前启动计时。
- 只有进入智能魔方 `READY` 后，首个有效转动才应启动复原计时。
- 运行中回到复原态且本轮已经发生过转动后，才应自动停止并保存成绩。
- BLE 协议兼容性不确定时，优先隔离在 `SharpTimer.Bluetooth`，不要把厂商特例散落到 UI code-behind。
- 涉及协议、加密、坐标系、打乱推进或复原判定时，应优先补 `SharpTimer.Tests` 中的平台无关测试。

## README And Assets

README 用于展示稳定功能和项目特点：

- `README.md` 是默认中文入口。
- `README-en.md` 是英文版，应严格同步 `README.md` 的结构和含义，不额外扩写。
- README 顶部使用 `SharpTimer.App/Assets/Square150x150Logo.scale-200.png` 作为居中图标。
- README 的应用截图放在 `.github/assets/sharptimer-main.png`，引用路径保持仓库相对路径。
- README 只写稳定功能、技术栈、预览和致谢，不写临时进度流水账。

## Testing And Verification

完成代码改动后，至少按影响范围验证：

- 核心规则变更：运行 `dotnet test SharpTimer.slnx`。
- 存储变更：补充或更新 `SharpTimer.Tests/Storage` 测试。
- UI/XAML 变更：运行 `dotnet build SharpTimer.slnx`，并尽量手动确认窗口能启动。
- 文档或 README 变更：检查链接和项目阶段描述是否仍准确。

常用命令：

```powershell
dotnet restore SharpTimer.slnx
dotnet build SharpTimer.slnx
dotnet test SharpTimer.slnx
```

## Git And Safety

- 不要回滚用户已有改动，除非用户明确要求。
- 大量删除文件、风险性脚本、推送代码或部署前，先说明计划、影响文件和风险，并等待确认。
- 小范围文档、代码和测试更新可直接执行。
- 提交信息使用中文开发者视角。

## Review Checklist

完成 UI 改动前检查：

- 是否使用了合适的官方 WinUI 控件。
- 是否避免了伪控件、伪卡片系统和手写弹窗。
- 是否没有混入 WPF/UWP-only 命名空间。
- 是否没有硬编码会破坏亮暗主题或高对比度的颜色。
- 键盘操作是否仍然可用，尤其是空格计时。
- 屏幕阅读器名称是否在需要处设置。
- 计时 UI 是否保持响应。
- 构建和相关测试是否通过。

完成较大功能、架构、接口或数据结构变更后检查：

- `docs/architecture.md` 是否需要同步。
- `docs/roadmap.md` 是否需要同步。
- `README.md` 的稳定功能介绍是否需要更新。
