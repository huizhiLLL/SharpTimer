# SharpTimer 架构设计

## 目标

SharpTimer 是一个专为智能魔方训练打造的 Windows 原生桌面计时器。本文件只记录技术结构、模块边界和关键设计约束；项目当前状态见 `docs/project.md`，后续计划见 `docs/roadmap.md`。

## 项目结构

```text
SharpTimer
├─ SharpTimer.App          WinUI 3 客户端
├─ SharpTimer.Core         平台无关的计时、统计和智能打乱推进规则
├─ SharpTimer.Storage      SQLite schema、迁移和仓储
├─ SharpTimer.Bluetooth    Windows BLE 智能魔方接入
├─ SharpTimer.Tests        核心逻辑和存储测试
├─ docs                    项目文档
└─ ref                     外部参考资料
```

## 分层原则

- `SharpTimer.Core` 放平台无关规则：计时状态机、成绩模型、罚时、统计、三阶打乱生成和智能打乱推进。
- `SharpTimer.Storage` 放 SQLite schema、迁移和仓储实现，App 不直接拼 SQL。
- `SharpTimer.Bluetooth` 放 BLE 扫描、连接、通知订阅、协议解析和设备命令。
- `SharpTimer.App` 放 WinUI 事件、界面渲染、本地设置和跨层编排。
- `SharpTimer.Tests` 优先覆盖 Core、Storage，以及不依赖真实蓝牙设备的智能魔方规则。

## 智能魔方预览

智能魔方实时预览位于 `SharpTimer.App/Rendering`：

- `SmartCubePreviewControl` 封装预览 Canvas、拖动视角、点击入口事件、动画节拍和渲染调用。
- `SmartCubePreviewRenderer` 根据 `facelets`、拖动视角、可选 gyro 姿态和可选转动动画生成 Canvas 图形。
- `MainWindow.xaml.cs` 只负责把 BLE / Core 事件转成 `SetFacelets(...)`、`PlayMove(...)`、`ResetView()` 等控件调用，并处理点击预览后打开蓝牙 Flyout。

当前预览使用 WinUI `Canvas`、`Path` 和 `Polygon` 实现伪 3D 投影。静止状态保留圆角视觉，转动动画期间使用轻量 `Polygon` 并跟随 `CompositionTarget.Rendering` 更新，避免每帧重建大量 XAML 元素。gyro 姿态作为魔方本体旋转输入，拖动视角继续作为观察偏移。后续如需真实 3D，应优先保持控件外部接口不变，在控件内部替换为 `SwapChainPanel + Direct3D` 或其他渲染实现。

## 数据模型

核心模型：

- `Solve`：一次复原成绩，包含原始用时、罚时、session、打乱、备注、来源、可选智能魔方转动序列、步数、TPS、复盘元数据和时间戳。
- `Penalty`：`None`、`PlusTwo`、`Dnf`。
- `ManualTimerStateMachine`：通用计时状态机，当前同时支撑智能魔方首转起表和备用手动输入路径。
- `SmartCubeScrambleTracker`：平台无关的智能魔方打乱推进器。
- `StatisticsCalculator`：计算 best、mean、ao5、ao12。

SQLite 当前使用 v2 schema：

- `sessions`：session 基本信息、项目代号、归档状态和排序。
- `solves`：成绩用时、罚时、来源、打乱、备注、智能魔方解法摘要和所属 session。
- `schema_migrations`：记录 schema 版本。

## UI 与 WinUI 约束

- 主界面使用官方 `NavigationView`，三个页面已拆为独立 `UserControl`（`TimerView`、`SolvesView`、`SettingsView`），包含智能魔方计时、成绩列表、成绩分析区和设置区域。
- 三页均已接入响应式布局（窄 < 720、中 720–1100、宽 > 1100），计时字号、页面 padding、成绩页分栏 / 堆叠、统计卡片排布随窗口宽度自适应。
- 智能魔方预览作为独立 `UserControl` 接入主计时页，避免把预览交互和动画状态继续堆在主窗口 code-behind。
- 智能打乱推进显示由 `ScrambleTextPresenter` 负责，App 设置可在隐藏已完成步骤和变浅保留已完成步骤之间切换。
- 智能魔方连接编排已抽到 `SmartCubeSessionController`（BLE 扫描、连接、断开、保活和设备事件转发）。
- 常规操作优先使用 WinUI 官方控件，如 `Button`、`ListView`、`ContentDialog`、`ToggleSwitch`、`ComboBox`、`MenuFlyout`。
- 样式优先使用 `ThemeResource` 和 Windows App SDK 能力，语义画刷（PersonalBest / ScrambleNext / ScrambleCorrectionBrush）已 token 化并支持亮暗主题切换。
- UI 控件、窗口 API 或样式资源不确定时，优先查 `ref/WinUI-Gallery`。

## 发布模型

- 当前默认发布路线是 WinUI 3 unpackaged self-contained 普通 exe：`WindowsPackageType=None`、`WindowsAppSDKSelfContained=true`。
- `scripts/package-release.ps1` 是默认一键发布入口，会生成便携版 zip；本机安装 Inno Setup 6 时会额外生成普通安装包 exe。
- `scripts/package-test.ps1` 保留为历史 MSIX 测试包脚本。重新启用 MSIX 时需要显式传入或恢复 `EnableMsixTooling=true`，并重新处理证书、签名和安装信任链。

## BLE 边界

`SharpTimer.Bluetooth` 负责和 Windows BLE API 交互。厂商协议差异、加密、通知包解析、历史补偿和连接细节应隔离在该项目中。

BLE 链路约束：

- 三套连接实现都要订阅底层 `ConnectionStatusChanged`，被动断链必须能发出 `SmartCubeDisconnectEvent` 并释放 GATT service、characteristic 和 device 资源。
- BLE 写入必须串行化，并把 `GattCommunicationStatus` 非 `Success` 作为失败处理；初始化请求、保活、ACK、状态请求和历史请求不能互相抢写。
- `SmartCubeSessionController` 负责扫描、连接、断开、保活、有限自动重连和设备事件转发；WinUI 层只负责展示连接状态和处理用户操作。
- App 层本地重置魔方状态后，应把 solved facelets 作为新的本地推演基准；GAN v3/v4 等协议随后发来的非权威 facelets 不能覆盖本地重置状态。
- 协议补偿优先恢复状态而不是直接断链：GAN Gen4 gap / overflow 走 history 或 facelets 重同步，QiYi 状态包按时间戳拆分当前步和未来步后再交给 App。
- WinUI `DispatcherTimer` 等 UI 线程对象只能在 UI Dispatcher 上启停，BLE 回调线程不得直接操作 UI 线程资源。

`SharpTimer.Core` 优先保留可测试的智能魔方规则，例如打乱推进、READY 判定、复原判定和计时状态转换。App 层只负责把 BLE 事件转成界面状态和计时动作。手动输入路径只作为备用入口，不应驱动新的产品设计。

后续扩展 Giiker、GoCube 等设备，或继续增强 GAN / QiYi 实机兼容性时，应优先新增协议实现和平台无关测试，不把厂商特例散落到 `MainWindow.xaml.cs`。
