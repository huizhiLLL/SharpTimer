# SharpTimer 架构设计

## 目标

SharpTimer 是 Windows 原生魔方计时器。第一阶段先完成本地手动计时闭环，包含空格开始/停止、15 秒观察、罚时、成绩保存、session 管理和基础统计；第二阶段再接入智能魔方 BLE。

## 项目结构

```text
SharpTimer
├─ SharpTimer.App          WinUI 3 客户端
├─ SharpTimer.Core         计时器核心模型、状态机、统计
├─ SharpTimer.Storage      SQLite schema、迁移和仓储
├─ SharpTimer.Tests        核心逻辑测试
└─ SharpTimer.Bluetooth    Windows BLE 智能魔方接入、协议抽象和 MoYu32 连接入口
```

## 分层原则

- `SharpTimer.App` 只负责界面、输入事件和展示状态，不直接承载计时规则。
- `SharpTimer.App` 当前使用 WinUI 官方 `NavigationView` 作为侧边栏容器，将主计时、成绩列表和设置拆成三个视图区域；智能魔方连接入口放在主计时页右上角蓝牙按钮中。
- `SharpTimer.App` 的前端控件、XAML 写法、WinUI API 调用和样式资源优先参考本地官方示例 `ref/WinUI-Gallery`，再结合 SharpTimer 当前结构做最小适配。
- `SharpTimer.App.Services` 提供很薄的应用服务层，负责串联 `ManualTimerStateMachine`、SQLite 仓储、默认 session、统计快照和本地 UI 设置。
- `SharpTimer.Core` 保持平台无关，提供计时状态机、`Solve` 成绩模型、`Penalty` 罚时模型和统计计算。
- `SharpTimer.Storage` 封装 SQLite schema、迁移和仓储接口，不让 App 直接拼 SQL。
- `SharpTimer.Tests` 覆盖核心计时规则和统计规则，优先保障平台无关逻辑稳定。
- `SharpTimer.Bluetooth` 把 Web Bluetooth 参考协议逐步迁移为 C# 协议层。当前提供事件、命令、协议注册、默认服务 UUID、按名称过滤的扫描器和 MoYu32 连接入口；`SharpTimer.App` 通过主计时页蓝牙按钮连接智能魔方，并把 MoYu32 转动和状态事件接入计时流程与状态预览。

## 前端参考边界

`ref/WinUI-Gallery` 是本项目最重要的 WinUI 前端参考资料，适用于：

- 选择官方控件和页面交互模式。
- 查询控件属性、事件、视觉状态、资源键和 XAML 写法。
- 对照 `NavigationView`、`ListView`、`ContentDialog`、`InfoBar`、`TeachingTip`、`CommandBar`、输入控件和窗口相关 API 的官方示例。
- 学习官方样式资源组织方式，避免手写伪 Fluent 组件。

使用时只借鉴与 SharpTimer 功能直接相关的控件组合和 API。不要直接搬入 WinUI Gallery 的演示框架、示例代码展示控件、页面索引系统或与计时器无关的大型结构。

UI 实现判断顺序：

1. 先看 SharpTimer 现有 UI 和服务边界。
2. 再查 `ref/WinUI-Gallery` 中同类官方示例。
3. 必要时再查 Microsoft Learn / Windows App SDK 文档。
4. 最后才考虑自定义控件或样式，并在改动说明中解释原因。

## 第一版核心模型

- `Solve`：一次复原成绩，包含原始用时、罚时、session、打乱、备注和创建时间。
- `Penalty`：`None`、`PlusTwo`、`Dnf` 三种罚时状态。
- `ManualTimerStateMachine`：手动计时状态机，当前支持 `Idle`、`Inspecting`、`Running`、`Stopped`。
- `ThreeByThreeScrambleGenerator`：生成三阶 WCA 记号打乱，当前使用 25 步随机转动并规避连续同轴转动；后续可替换为完整 random-state 求解器。
- `SmartCubeScrambleTracker`：平台无关的智能魔方打乱推进器，按当前三阶打乱生成与 DCTimer/min2phase 坐标一致的前缀目标状态，跟踪实际魔方状态、半步 `R2/U2` 进度、偏离后的反向纠错和超长纠错锁定提示；目标打乱态与复原态判定会忽略整体魔方朝向。
- `StatisticsCalculator`：计算 best、mean、ao5、ao12。
- `TimerAppService`：应用服务层，初始化本地数据库，确保默认 session，处理 session 创建/切换/重命名/归档、主计时动作、罚时修改、成绩删除和统计快照。
- `AppSettingsService`：WinUI 本地设置服务，保存是否启用观察、显示精度、主题偏好和界面语言。该类设置不进入 SQLite schema。
- `LocalizedStrings`：App 层轻量中英文字符串表，用于导航、设置、成绩列表、计时状态和基础对话框文案。

默认本地偏好为英文界面、亮色主题、百分秒显示和启用 15 秒观察。主界面页面切换使用轻量进入动画；如需严格使用 WinUI `DrillInNavigationTransitionInfo`，应先把各页面拆成独立 `Page` 或 `UserControl`，避免在运行时搬动已有 XAML 控件树。

## SQLite v1 schema

当前 schema 由 `SharpTimer.Storage` 的 `SharpTimerDatabase` 初始化，并通过 `schema_migrations` 记录版本。

### sessions

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | `TEXT PRIMARY KEY` | session 的 GUID |
| `name` | `TEXT NOT NULL` | session 名称 |
| `puzzle` | `TEXT NOT NULL` | 项目代号，第一版默认 `333` |
| `created_at` | `TEXT NOT NULL` | UTC ISO 8601 创建时间 |
| `updated_at` | `TEXT NOT NULL` | UTC ISO 8601 更新时间 |
| `is_archived` | `INTEGER NOT NULL` | 0/1 归档标记 |
| `sort_order` | `INTEGER NOT NULL` | session 排序 |

### solves

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | `TEXT PRIMARY KEY` | solve 的 GUID |
| `session_id` | `TEXT NOT NULL` | 所属 session，外键级联删除 |
| `duration_ms` | `INTEGER NOT NULL` | 原始用时，毫秒 |
| `penalty` | `INTEGER NOT NULL` | 0=None，1=+2，2=DNF |
| `scramble` | `TEXT NULL` | 打乱文本 |
| `comment` | `TEXT NULL` | 备注 |
| `created_at` | `TEXT NOT NULL` | UTC ISO 8601 创建时间 |
| `updated_at` | `TEXT NOT NULL` | UTC ISO 8601 更新时间 |

索引：

- `idx_sessions_active_sort`：用于 active session 列表。
- `idx_solves_session_created`：用于按 session 加载成绩列表。
- `idx_solves_created`：用于后续全局时间线查询。

## 观察与罚时规则

手动计时的启动交互参考 cstimer：空闲、观察或已停止状态下按住空格只进入 `Ready` 展示态，松开空格才开始观察或开始复原；复原运行中按下空格立即停止并保存成绩。

Ready 状态和运行状态进入沉浸式计时布局，仅保留计时数字；打乱、观察提示和统计块会临时隐藏。Ready 时计时数字以短过渡放大并变为绿色；进入运行后保持同一放大终点，运行和停止状态不额外显示状态文字。

主计时区显示当前三阶打乱。左右方向键切换上一条/下一条打乱；成绩停止保存时，会把当前打乱写入 `solves.scramble`，并自动生成下一条。

默认启用 15 秒观察。观察开始后：

- 15 秒内开始复原：无罚时。
- 超过 15 秒且不超过 17 秒开始复原：`+2`。
- 超过 17 秒开始复原：`DNF`。

统计中的 `+2` 计入有效成绩，`DNF` 不参与 best 和 mean。ao5/ao12 按窗口成绩去掉最好和最差；若窗口内 DNF 超过 1 个，则该平均为 DNF，用 `null` 表示。

## BLE 迁移边界

`ref/smartcube-web-bluetooth` 中的 `smartcube/types.ts`、`protocol.ts`、`connect.ts` 适合迁移为以下形态：

- `ISmartCubeProtocol`：协议解码、设备识别和事件输出。
- `SmartCubeEvent`：转动、状态、电量、连接等事件。
- `SmartCubeConnection`：Windows BLE 连接、订阅 characteristic、写入指令。

当前已经建立 `SharpTimer.Bluetooth` 第一段骨架：

- `SmartCubeEvent`：对应参考项目中的 `MOVE`、`FACELETS`、`GYRO`、`BATTERY`、`HARDWARE`、`DISCONNECT` 事件。
- `SmartCubeCommand`：对应请求状态、电量、硬件信息和重置等命令。
- `ISmartCubeConnection`：定义设备名、MAC、协议信息、能力、事件订阅、命令发送和断开连接。
- `ISmartCubeProtocol` 与 `SmartCubeProtocolRegistry`：用于后续按设备名称和 GATT 服务选择具体厂商协议。
- `SmartCubeBluetoothServices`：集中维护 GAN、QiYi、MoYu、Giiker、GoCube 等常见服务 UUID。
- `WindowsBleSmartCubeScanner`：提供 Windows BLE 广播扫描入口；App 层只显示名称匹配的智能魔方设备。
- `WindowsBleSmartCubeConnector`：当前实现 MoYu32 连接、通知订阅、基础电量/状态/转动事件输出；MoYu32 对齐 cstimer 的 `moyu32cube.js`，优先使用广播厂商数据最后 6 字节倒序作为加密 MAC，取不到时使用 `WCU_MY32_5C3A` 这类设备名推导默认 MAC。连接时会逐个候选 MAC 请求强校验包，只有硬件、状态或转动包能确认 key，电量包不再作为 key 有效依据。
  MoYu32 的 20 字节包存在重叠 AES block：加密按前 16 字节再后 16 字节，解密必须按后 16 字节再前 16 字节，保持与 cstimer 一致。
- 主计时页蓝牙 Flyout：点击右上角圆形蓝牙按钮自动扫描，选择设备后进入智能魔方模式；连接后 Flyout 只展示已连接提示、名称、电量、重置本地魔方状态和断开连接操作。
- 主计时区智能魔方展示：连接智能魔方后，三维状态预览固定在主计时区右下角，不使用额外容器或说明文字。打乱文本只展示 move 序列，通过颜色区分下一步打乱和纠错步骤。
- 智能魔方打乱推进：连接后 App 会用当前三阶打乱构建目标状态序列。未完成打乱时，MoYu32 转动只推进打乱进度或生成纠错提示，不会启动计时；实际状态达到当前打乱目标后进入智能魔方 `READY` 展示态。转动事件按本地状态连续推进，推进中不再用延迟 facelets 状态包回写打乱进度；facelets 状态包只在初始同步、新打乱校准和运行结束判定中使用。
- 智能魔方计时：只有在智能魔方 `READY` 后的首个有效转动才会进入复原计时；运行中继续接收转动并请求状态同步；当状态包回到复原态且本轮已经发生过转动时，自动停止计时、保存成绩并生成下一条打乱。连接初始状态即使是复原态也不会触发停止。

下一步应在实机上验证 MoYu32 打乱推进、纠错提示、READY 后首转起表和最后一拧后的状态同步延迟，再根据体验决定是否继续增强本地魔方状态维护与纠错 UI。
