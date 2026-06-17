# SharpTimer 前端 UI 优化计划

本文件记录 `SharpTimer.App` 前端界面、交互逻辑和布局的优化方向。当前项目状态见 `docs/project.md`，模块边界见 `docs/architecture.md`，整体路线见 `docs/roadmap.md`。本计划可接受中小型页面重构，但不重建 WinUI 项目结构，不切换核心技术栈。

## 现状总览

当前前端由单个 `MainWindow` 承载全部界面：计时（`TimerPage`）、成绩（`SolvesPage`）、设置（`SettingsPage`）三个页面都是同一个 `Grid Padding="32"` 下的子 `Grid`，靠 `Visibility` 切换，`NavigationView` 只负责选中态。计时驱动、蓝牙扫描连接、智能打乱着色、统计、成绩列表、设置读写、弹窗构建全部集中在 `MainWindow.xaml.cs`（约 1730 行）。

整体可用、Fluent 基调在线，但存在几类系统性问题：无响应式布局、页面职责耦合、设计 token 散落硬编码、部分交互不符合官方控件惯例、信息架构有空洞（分析区是占位、统计区有空列）。

## 分领域问题与优化方案

## 推进状态

截至当前代码状态，已完成：

- 语义画刷 token 化：PB、READY / 下一步、纠错色已从 code-behind 硬编码迁入 `App.xaml` 主题资源。
- 设置页分组：设置页已按计时 / 外观 / 语言分组，并显示 `SettingsDescription`。
- 成绩详情弹窗：自制 overlay 已迁移为 `ContentDialog`，删除成绩前已加确认。
- 可访问性本地化：标题栏按钮、蓝牙入口、设备列表、智能魔方预览等辅助名称已接入 `LocalizedStrings`。
- 响应式断点：页面 padding、计时字号、成绩页分栏 / 堆叠已接入三档 `VisualStateManager`。
- 计时页统计：已补齐 best / Ao5 / Ao12 / 次数，统计卡片在窄屏下 2x2 排布。
- 蓝牙入口：已从绝对定位改为打乱行独立列，去掉 `Margin 72` 硬避让。
- 打乱文本展示：智能打乱文本构建已抽到 `ScrambleTextPresenter`。
- 蓝牙设备展示格式：设备列表项构建已抽到 `BluetoothDeviceListItemFactory`。
- 蓝牙 Flyout 信息补齐：设备列表已展示服务摘要和最近发现时间，连接中会禁用列表，连接失败后提供重新扫描入口。
- 成绩列表空状态：无成绩时显示本地化引导。
- 圆角 token 化：卡片圆角已统一引用 `SharpTimerCardCornerRadius`。
- 智能魔方预览平移：沉浸态下的预览偏移已改由 VisualState 管理。

部分完成 / 待继续：

- padding、spacing 等设计 token 仍未系统统一。
- 成绩分析区仍未落地真实趋势 / 分布 / 指标内容。
- session 操作区仍保持图标按钮排列，未收进 `MenuFlyout`。
- 三页尚未拆为 `TimerView`、`SolvesView`、`SettingsView`。
- 蓝牙 / 智能魔方编排尚未抽到 `SmartCubeSessionController`。

### 1. 响应式布局（最高优先，违反 AGENTS.md 明文约束）

现状：全局零响应式，代码中无任何 `VisualStateManager` / `AdaptiveTrigger`。

- `TimerText FontSize="112"` 硬编码，窄窗口会溢出或挤压。
- 成绩页 `2* / Auto / 3*` 固定三栏，窄窗口下左侧列表被压到不可读，右侧分析区和列表强行并排。
- 外层 `Padding="32"` 固定，窄窗口浪费横向空间。
- 打乱文本 `Margin="0,24,72,0"`，右边 72px 是为给右上角蓝牙按钮让位的魔法数字，窄屏偏移很怪。

方案：

- 三个页面引入 `VisualStateManager` + `AdaptiveTrigger`，定义三档断点（窄 < 720、中 720–1100、宽 > 1100），对应 AGENTS.md 要求的宽屏 / 中等 / 窄窗口。
- 计时页 `TimerText` 字号随断点切换（如 72 / 96 / 112），用 VisualState 控制而非硬编码。
- 成绩页宽屏维持左右分栏；中窄屏改为上下堆叠（列表在上、分析在下），列宽从 `2*/3*` 退化为单列。这是中小型重构的核心一块。
- 外层 padding 随断点变化（窄 16 / 中 24 / 宽 32）。

### 2. 页面架构与 code-behind 拆分（中型重构）

现状：`MainWindow.xaml.cs` 约 1730 行，同时负责计时状态驱动、蓝牙扫描连接事件、智能打乱文本着色、统计渲染、成绩列表构建、设置读写、弹窗构建。三个页面写在一个 XAML 里，`ShowPage` 手动切 `Visibility`。`docs/roadmap.md` 已将「逐步减轻 MainWindow.xaml.cs」列为目标。

方案（渐进，不推翻打包结构）：

- 三个页面抽成独立 `UserControl`：`TimerView`、`SolvesView`、`SettingsView`，放 `SharpTimer.App/Views/`。`MainWindow` 只留 `NavigationView`、标题栏和全局 overlay。
- 蓝牙 / 智能魔方编排（约 400 行）抽到 `SmartCubeSessionController`（`App.Services` 下），与现有 `SmartCubePreviewControl` 拆分思路一致。
- 打乱着色逻辑（`BuildSmartCubeScrambleRuns`、`AddScrambleRun` 等）抽到 `ScrambleTextPresenter` 帮助类。
- 关键约束：空格计时路径目前是 `AppRoot.AddHandler(KeyDownEvent, ...)` 全局监听（`MainWindow.xaml.cs:80`）。拆页时必须保留这条链路在窗口级，不能被子 View 的焦点吞掉，这是 AGENTS.md 的硬性行为约束。

> 该项体量偏大，建议分多次小步提交，每步 `dotnet build` 加启动验证空格起表。

### 3. 设计系统 / 主题 token 化

现状：硬编码散落确认如下。

- `Colors.OrangeRed`（PB 高亮、纠错色）、`Colors.ForestGreen`（READY / next 色）在 code-behind 写死，亮 / 暗主题下对比度不受控，违反「默认同时支持亮暗」。
- overlay 蒙版 `Background="#27000000"` 硬编码（`MainWindow.xaml:609`）。
- 统计卡片 `CornerRadius="8"`、各处 `Padding`、`Spacing` 全是散落字面量。

方案：

- 在 `App.xaml` 建 `ResourceDictionary.ThemeDictionaries`（Light / Dark），定义语义画刷：`PersonalBestBrush`、`ScrambleNextBrush`、`ScrambleCorrectionBrush`、`ReadyTimerBrush`、`OverlayScrimBrush`。code-behind 改为 `Application.Current.Resources[...]` 取，亮暗各给一套。
- 圆角统一引用 `{ThemeResource ControlCornerRadius}` / `OverlayCornerRadius`，去掉散落的 `8`。
- 后续改色只动资源字典一处。

### 4. 计时页（TimerPage）

现状：

- 右上角蓝牙按钮绝对定位叠在打乱文本区，靠 `Margin 72` 硬躲，窄屏易重叠。
- 统计区 `StatsPanel` 定义了 4 列却只用了 2 个（Ao5 / Ao12），右边两列空着，视觉失衡。计时页缺 best、当前次数等常见信息。
- `SmartCubePreview` 用 `TranslateTransform Y=100/0` 在沉浸态平移，是 hack 式定位。

方案：

- 蓝牙入口从悬浮按钮改为放进标题栏右侧或打乱行的独立列（不再用绝对定位加 margin 让位）。
- 统计区补齐为 4 张卡：`best / Ao5 / Ao12 / 次数`，或按断点减列；卡片用统一样式而非各写一遍 `Border`。
- 沉浸态（READY / Running 隐藏打乱和统计）逻辑保留，这是个好设计；但把 `SmartCubePreviewOffset` 的平移改为 VisualState 管理，集中到状态机。

### 5. 成绩页（SolvesPage）

现状：

- 右侧分析区是纯占位：`"趋势图和分布图会放在这里"`（`MainWindow.xaml:526`），且顶部 Ao5 / Ao12 / 次数与左侧、与计时页三处重复展示同样数据。
- 列表无空状态：新 session 没有成绩时只剩表头，无引导。
- session 操作（重命名 / 新建 / 删除）是三个 36x32 图标按钮裸排；删除走 `ContentDialog`（这点 OK），新建 / 重命名用自建 `TextBox` 加 `ContentDialog` 拼装。
- 成绩详情用自制 overlay（`SolveDetailsOverlay` 加手动 `Tapped` 命中判定加手动构建 `StackPanel`），而非官方 `ContentDialog`。AGENTS.md 明确「不要自制弹窗」。

方案：

- 分析区落地真实内容（roadmap 已列）：用轻量自绘折线（趋势）加分布，或先做 best / worst / mean / σ 指标卡，去掉与左侧重复的 Ao5 / Ao12。
- 加 `ListView` 空状态视图（无成绩时显示「按空格或转动魔方开始第一次计时」引导）。
- 成绩详情 overlay 迁移到 `ContentDialog`，删除 `SolveDetailsOverlay` 和手动命中逻辑约 70 行；删除成绩前加确认（AGENTS.md 要求破坏性操作用官方确认控件，目前删除无二次确认）。
- session 操作区可考虑收进 `MenuFlyout` 或保持，但补齐 `ToolTip` / `AutomationProperties`（部分已有）。

### 6. 设置页（SettingsPage）

现状：所有设置项塞在一个 `Border` 卡片里竖排 `ToggleSwitch` 加 4 个 `ComboBox`，无分组、无每项说明。`SettingsDescription` 字符串在 `LocalizedStrings` 里定义了却没在 XAML 用上。

方案：

- 用 WinUI Gallery 标准的设置页式样：`SettingsCard`（或等价官方分组卡片）按「计时 / 外观 / 语言」分组，每项带 Header 加副标题描述。
- 把已有的 `SettingsDescription` 真正显示出来。
- 这是典型中小型页面重构，收益高、风险低。

### 7. 蓝牙 Flyout

现状：固定 `Width="360"`、`ListView MaxHeight="260"`，设备列表项右侧只显示 `Protocol`，但 `BluetoothDeviceListItem` 其实还算了 `Services` / `LastSeen` 未展示。连接失败提示直接覆盖在状态文字上，无重试入口。

方案：

- 列表项补充信号 / 最近发现时间或协议 badge；失败态给「重试」按钮而非只改文字。
- 连接中可禁用列表避免重复点击。

### 8. 可访问性与本地化

现状：

- 标题栏按钮 `AutomationProperties.Name="展开或折叠导航"`、蓝牙按钮 `"智能魔方蓝牙"`、预览控件 `"智能魔方预览"` 硬编码中文，英文环境不切换。
- session 图标按钮虽在 `ApplyLanguage` 里补了 Name，但标题栏 / 蓝牙 / 预览这几个没有。

方案：把这些 `AutomationProperties.Name` 纳入 `LocalizedStrings`，随语言切换。

## 分期落地计划

### 第一阶段（低风险、高收益，先做）

1. `[已完成]` 设计 token 化：抽语义画刷到 `ThemeDictionaries`，替换硬编码 `OrangeRed` / `ForestGreen` / `#27000000`。
2. `[已完成]` 设置页重构为分组 `SettingsCard` 加显示描述。
3. `[已完成]` 成绩详情 overlay 迁移到 `ContentDialog`，删除成绩加确认。
4. `[已完成]` `AutomationProperties` 本地化补齐。

### 第二阶段（响应式，中风险）

5. `[已完成]` 三页 `VisualStateManager` 加 `AdaptiveTrigger` 三档断点；计时字号、外层 padding、成绩页分栏 / 堆叠自适应。
6. `[已完成]` 计时页统计区补齐（best / 次数）、蓝牙入口去绝对定位。

### 第三阶段（架构整理，中型重构，分多次）

7. `[待推进]` 三页拆为独立 `UserControl`；保留窗口级空格监听。
8. `[部分完成]` 蓝牙 / 智能魔方编排抽 `SmartCubeSessionController`；打乱着色抽 presenter。

### 第四阶段（功能补齐）

9. `[部分完成]` 成绩分析区真实趋势 / 分布图；列表空状态。

## 风险与验证

- 空格计时路径是核心硬约束，第二、三阶段每次改动后必须 `dotnet build SharpTimer.slnx` 加真实启动，确认空格开始观察 / 复原 / 停止仍可用。
- 主题相关改动需亮 / 暗加 Mica / Acrylic 都过一遍。
- 响应式改动需在窄 / 中 / 宽三种窗口宽度手动确认。
- 拆页属中型重构，建议小步提交，每步可独立验证回退。
- 不涉及 Core / Storage / Bluetooth 逻辑，不动打包模型，符合「暂不重建项目结构」。
