# SharpTimer 架构设计

## 目标

SharpTimer 是一个支持智能魔方的 Windows 原生魔方计时器。本文件只记录技术结构、模块边界和关键设计约束；项目当前状态见 `docs/project.md`，后续计划见 `docs/roadmap.md`。

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

- `SharpTimer.Core` 放平台无关规则：手动计时状态机、成绩模型、罚时、统计、三阶打乱生成和智能打乱推进。
- `SharpTimer.Storage` 放 SQLite schema、迁移和仓储实现，App 不直接拼 SQL。
- `SharpTimer.Bluetooth` 放 BLE 扫描、连接、通知订阅、协议解析和设备命令。
- `SharpTimer.App` 放 WinUI 事件、界面渲染、本地设置和跨层编排。
- `SharpTimer.Tests` 优先覆盖 Core、Storage，以及不依赖真实蓝牙设备的智能魔方规则。

## 数据模型

核心模型：

- `Solve`：一次复原成绩，包含原始用时、罚时、session、打乱、备注和时间戳。
- `Penalty`：`None`、`PlusTwo`、`Dnf`。
- `ManualTimerStateMachine`：手动计时状态机。
- `SmartCubeScrambleTracker`：平台无关的智能魔方打乱推进器。
- `StatisticsCalculator`：计算 best、mean、ao5、ao12。

SQLite 当前使用 v1 schema：

- `sessions`：session 基本信息、项目代号、归档状态和排序。
- `solves`：成绩用时、罚时、打乱、备注和所属 session。
- `schema_migrations`：记录 schema 版本。

## UI 与 WinUI 约束

- 主界面使用官方 `NavigationView`，包含主计时、成绩列表、成绩分析区和设置区域。
- 常规操作优先使用 WinUI 官方控件，如 `Button`、`ListView`、`ContentDialog`、`ToggleSwitch`、`ComboBox`。
- 样式优先使用 `ThemeResource` 和 Windows App SDK 能力，避免硬编码整套伪 Fluent 视觉系统。
- UI 控件、窗口 API 或样式资源不确定时，优先查 `ref/WinUI-Gallery`。

## BLE 边界

`SharpTimer.Bluetooth` 负责和 Windows BLE API 交互。厂商协议差异、加密、通知包解析、历史补偿和连接细节应隔离在该项目中。

`SharpTimer.Core` 只保留可测试的智能魔方规则，例如打乱推进、READY 判定和复原判定。App 层只负责把 BLE 事件转成界面状态和计时动作。

后续扩展 Giiker、GoCube 等设备，或继续增强 GAN / QiYi 实机兼容性时，应优先新增协议实现和平台无关测试，不把厂商特例散落到 `MainWindow.xaml.cs`。
