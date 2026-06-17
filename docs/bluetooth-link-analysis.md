# SharpTimer 蓝牙链路问题分析

本文件分析 `SharpTimer.Bluetooth` 智能魔方 BLE 链路的明显 bug 和影响实际体验的问题，并给出修复方向。模块边界见 `docs/architecture.md`，后续计划见 `docs/roadmap.md`。本文件是代码静态分析结论，未做真机长时间挂测；涉及真机表现的判断会标注「需真机验证」。

## 链路结构速览

- `WindowsBleSmartCubeScanner`：`BluetoothLEAdvertisementWatcher` 主动扫描，广播事件转 `SmartCubeDeviceInfo`。
- `WindowsBleSmartCubeConnector.ConnectAsync`：识别协议后创建 MoYu32 / GAN / QiYi 连接并 `InitializeAsync`。
- 三套连接实现 `ISmartCubeConnection`：各自订阅通知特征 `ValueChanged`，解析后通过 `EventReceived` 抛 `SmartCubeEvent`。
- App 层（`MainWindow.xaml.cs`）：`_smartCubeKeepAliveTimer` 每 60 秒发一次 `RequestBattery` 作为保活；`SmartCubeConnection_EventReceived` 经 `DispatcherQueue` 回主线程渲染。

## 明显问题（按严重度）

### P0：静默断连不可感知（即用户问的「无操作自动断联」）

这是当前链路最严重的体验问题，根因明确：

1. 三套连接（`Moyu32SmartCubeConnection`、`WindowsBleGanSmartCubeConnection`、`WindowsBleQiYiSmartCubeConnection`）**都没有订阅 `BluetoothLEDevice.ConnectionStatusChanged`**。`SmartCubeDisconnectEvent` 只在两种情况下发出：应用主动调用 `DisconnectAsync`，或 GAN 收到特定包（`0xEA` / move buffer 溢出）。
2. 智能魔方普遍在一段时间无转动后进入睡眠以省电（睡眠由「无物理转动」触发，**不是由 BLE 有无流量触发**）。魔方睡眠或走远 / 关机时，BLE 链路在系统层被断开。
3. 此时没有任何代码路径把这次底层断链转成 `SmartCubeDisconnectEvent`。

结果：

- UI 一直停留在「已连接」状态，`ConnectedCubePanel` 仍显示，3D 预览停在最后一帧，打乱区不再更新。
- 用户拿起魔方转动毫无反应，且不会自动重连、不会重新扫描，必须手动断开再重连。
- 这正是「放一会儿就连不上了，还看不出来」的现象来源。

注：60 秒的 `RequestBattery` 保活能在一定程度上维持 BLE 链路活跃（需真机验证不同适配器表现），但**无法阻止魔方因无转动而自身睡眠**，因此无法根治该问题；真正缺的是「断链可被感知」。

### P1：保活写入失败被静默吞掉，死连接永不回收

`MainWindow.xaml.cs` 的 `SmartCubeKeepAliveTimer_Tick`：

```csharp
try { await connection.SendCommandAsync(SmartCubeCommand.RequestBattery); }
catch { }
```

链路已断时，这次写入会抛异常，但被空 `catch` 吞掉，计时器继续每 60 秒对一个已死连接写入。这本是**唯一能间接发现断链的位置**，却被丢弃。各连接内部的 `SendRequestAsync` 同样在 `IsConnectionClosing()` 为 false 但底层已断时静默失败（`WriteValueAsync` 抛异常路径未触发断连事件）。

### P1：完全没有重连机制

`docs/roadmap.md` 已把「打磨断开重连」列为近期重点，但当前代码**零重连**：任何断连（无论主动、被动、异常）后都只能手动重新扫描连接。配合 P0，体验上就是「断了既不知道、也不自己回来」。

### P1：GAN move buffer 溢出会在使用中强制断连

`WindowsBleGanSmartCubeConnection.EvictMoveBuffer` 和 `ParseGen4`：

```csharp
if (_moveBuffer.Count > 16) { _ = DisconnectAsync(); }
```

当 Gen4 的转动序号（serial）因丢包持续对不上、缓冲区堆积超过 16 个时，直接断开连接。这意味着一次较严重的 BLE 丢包 / 乱序可能在**正常使用甚至复原中途**把魔方踢下线，且无重连兜底。属于「为容错引入的副作用反而更伤体验」。需真机验证触发频率。

### P2：QiYi 保活会重跑握手探测，可能阻塞

QiYi 的 `SendCommandAsync(RequestBattery)` 走 `SendHelloAsync`。若 `_helloReceived` 仍为 false，会持 `_helloLock` 遍历 MAC 候选，每个等待最多 900ms。极端情况下（握手未成功又触发保活）单次保活可能阻塞数秒。正常握手成功后只发单次 hello，影响小。

### P2：断连事件与解绑存在竞态窗口（影响小）

连接内部从 BLE 回调线程调用 `EventReceived?.Invoke(...)`，App 在 `DisconnectSmartCubeAsync` 里先解绑再 `DisposeAsync`。多数路径有 `IsConnectionClosing()` 与 `_lifetimeLock` 保护，竞态概率低，但 `EventReceived` 的读取与解绑跨线程无锁，理论上存在临界窗口。当前未见崩溃证据，列为观察项。

## 修复方向

### 第一优先：让断链可感知（治 P0 / P1）

- 在三套连接的 `InitializeAsync` 里订阅 `_device.ConnectionStatusChanged`，当状态变为 `Disconnected` 时，统一走一条收敛路径发出 `SmartCubeDisconnectEvent`（复用现有 `IsConnectionClosing` / `_lifetimeLock`，避免与主动断开重复发事件）。这是最小、最直接的根因修复。
- App 层收到 `SmartCubeDisconnectEvent` 后，已有清理逻辑（`RenderSmartCubeEventAsync` 的 `SmartCubeDisconnectEvent` 分支），但要确认它能区分「主动断开」与「意外掉线」，给意外掉线一个明确 UI 提示（如状态文字 + 重新扫描入口）。
- 保活 `Tick` 的空 `catch` 改为：写入失败时触发一次断链判定（或直接发断连事件），而不是无限对死连接重试。

### 第二优先：重连（补 roadmap 既定目标）

- 在意外断链后，提供有限次自动重连（按地址 `BluetoothLEDevice.FromBluetoothAddressAsync` 重连 + 重新 `InitializeAsync`），并在 UI 上反馈「正在重连」。
- 重连退避要有上限，避免对已关机 / 走远的魔方无限重试。
- 自动重连逻辑放在 `SharpTimer.Bluetooth` 内部或 App 的 `SmartCubeSessionController`（见 `docs/ui-optimization-plan.md` 的编排抽离计划），不要散落进 `MainWindow.xaml.cs`，符合「厂商兼容性逻辑隔离在 Bluetooth 层」的边界约束。

### 第三优先：GAN 溢出降级

- 把 `_moveBuffer.Count > 16` 的硬断连改为先尝试「重置序号 / 重新请求 facelets 重建状态」，仅在确实无法恢复时才断连，降低使用中途掉线概率。

### 观察项

- QiYi 保活与握手探测：握手成功后避免再次进入候选遍历分支（确认 `_helloReceived` 守卫覆盖所有保活路径）。
- `EventReceived` 跨线程读取与解绑的临界窗口：如后续出现偶发异常再加锁收敛。

## 影响面与边界

- 上述修复集中在 `SharpTimer.Bluetooth` 各连接实现，以及 `MainWindow.xaml.cs` 中保活与断连处理少量编排，不触碰 `SharpTimer.Core` 计时规则。
- `SmartCubeDisconnectEvent` 已是现有事件类型，新增订阅复用它即可，不改 `ISmartCubeConnection` 公共接口。
- 手动计时（空格路径）与智能魔方链路相互独立，断连修复不应影响手动计时。

## 验证建议

- 单元 / 集成层：BLE 强依赖真机，平台无关部分（如断连状态机、重连退避计数）可在 `SharpTimer.Tests` 补可测逻辑。
- 真机验证：
  - 连接后静置使魔方睡眠，确认 UI 能在合理时间内显示断开而非假死。
  - 断电 / 走出范围，确认能感知断链并触发重连或明确提示。
  - 复原中途制造丢包（如瞬间远离），确认 GAN 不会轻易硬断连。
- 每次改动后 `dotnet build SharpTimer.slnx`；UI 编排改动需启动验证并确认空格计时不受影响。

## 结论

用户反馈的「一段时间无操作自动断联且无感知」确属真实缺陷，根因是**三套连接均未监听底层 `ConnectionStatusChanged`，魔方自身睡眠导致的 BLE 掉线无法被转成断连事件**，叠加保活异常被静默吞掉和零重连，形成「断了不知道、也不自己回来」的体验。优先补「断链可感知」是性价比最高的修复，重连和 GAN 溢出降级次之。

## 实现进度（2026-06-17）

### 已完成：P0 断链可感知

在三套连接实现（`WindowsBleGanSmartCubeConnection`、`WindowsBleQiYiSmartCubeConnection`、`Moyu32SmartCubeConnection`）中：

1. **在 `InitializeAsync` 最后订阅 `_device.ConnectionStatusChanged`**：握手和通知订阅成功后，注册系统层断连监听。
2. **添加 `Device_ConnectionStatusChanged` 处理方法**：当 `ConnectionStatus` 变为 `Disconnected` 时，通过 `_lifetimeLock` 保护检查 `_isDisconnecting` 和 `_isDisposed`，避免与主动断开重复发事件；若确认为意外断链，设置 `_isDisconnecting` 并发出 `SmartCubeDisconnectEvent`。
3. **在 `DisconnectAsync` 取消订阅**：主动断开时，在 `_device.Dispose()` 前先解绑 `ConnectionStatusChanged`，与 `ValueChanged` 处理对称。

在 `SmartCubeSessionController.KeepAliveTimer_Tick` 中：

4. **改进保活异常处理**：`SendCommandAsync(RequestBattery)` 抛异常时（写入失败，链路已断），停止计时器并主动清理连接，触发 `DisposeAsync`，避免对死连接无限重试。

**验证状态**：代码已编译通过（`dotnet build SharpTimer.slnx` 成功），待真机验证以下场景：
- 连接后静置使魔方自身睡眠，确认 UI 能在合理时间内显示断开状态。
- 魔方关机或走出范围，确认能感知断链并触发 `SmartCubeDisconnectEvent`。
- 保活写入失败时，确认连接能正确清理而非假死。

### 待实现：P1 重连机制

按 `docs/roadmap.md` 既定目标，意外断链后提供有限次自动重连（按地址 `BluetoothLEDevice.FromBluetoothAddressAsync` 重连 + 重新 `InitializeAsync`），在 UI 上反馈「正在重连」，并设置退避上限避免对已关机设备无限重试。重连逻辑放在 `SmartCubeSessionController` 或新增独立控制器，不散落进其他层。

### 待实现：P1 GAN 溢出降级

将 `_moveBuffer.Count > 16` 的硬断连改为先尝试重置序号或重新请求 facelets 重建状态，仅在确实无法恢复时才断连，降低使用中途掉线概率。

### 待实现：P2 QiYi 保活优化

确认 `_helloReceived` 守卫覆盖所有保活路径，握手成功后避免再次进入 MAC 候选遍历分支，防止保活阻塞数秒。

