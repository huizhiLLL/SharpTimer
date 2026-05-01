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
└─ SharpTimer.Bluetooth    Windows BLE 智能魔方接入，后续添加
```

## 分层原则

- `SharpTimer.App` 只负责界面、输入事件和展示状态，不直接承载计时规则。
- `SharpTimer.App.Services` 提供很薄的应用服务层，负责串联 `ManualTimerStateMachine`、SQLite 仓储、默认 session、统计快照和本地 UI 设置。
- `SharpTimer.Core` 保持平台无关，提供计时状态机、`Solve` 成绩模型、`Penalty` 罚时模型和统计计算。
- `SharpTimer.Storage` 封装 SQLite schema、迁移和仓储接口，不让 App 直接拼 SQL。
- `SharpTimer.Tests` 覆盖核心计时规则和统计规则，优先保障平台无关逻辑稳定。
- `SharpTimer.Bluetooth` 后续把 Web Bluetooth 参考协议迁移为 C# 协议层，避免和本地计时逻辑耦合。

## 第一版核心模型

- `Solve`：一次复原成绩，包含原始用时、罚时、session、打乱、备注和创建时间。
- `Penalty`：`None`、`PlusTwo`、`Dnf` 三种罚时状态。
- `ManualTimerStateMachine`：手动计时状态机，当前支持 `Idle`、`Inspecting`、`Running`、`Stopped`。
- `StatisticsCalculator`：计算 best、mean、ao5、ao12。
- `TimerAppService`：应用服务层，初始化本地数据库，确保默认 session，处理 session 创建/切换/重命名/归档、主计时动作、罚时修改、成绩删除和统计快照。
- `AppSettingsService`：WinUI 本地设置服务，保存是否启用观察、显示精度和主题偏好。该类设置不进入 SQLite schema。

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

默认启用 15 秒观察。观察开始后：

- 15 秒内开始复原：无罚时。
- 超过 15 秒且不超过 17 秒开始复原：`+2`。
- 超过 17 秒开始复原：`DNF`。

统计中的 `+2` 计入有效成绩，`DNF` 不参与 best 和 mean。ao5/ao12 按窗口成绩去掉最好和最差；若窗口内 DNF 超过 1 个，则该平均为 DNF，用 `null` 表示。

## BLE 后续迁移边界

`ref/smartcube-web-bluetooth` 中的 `smartcube/types.ts`、`protocol.ts`、`connect.ts` 适合迁移为以下形态：

- `ISmartCubeProtocol`：协议解码、设备识别和事件输出。
- `SmartCubeEvent`：转动、状态、电量、连接等事件。
- `SmartCubeConnection`：Windows BLE 连接、订阅 characteristic、写入指令。

这部分放在第二阶段，避免 BLE 权限、驱动和协议调试影响第一阶段可用性。
