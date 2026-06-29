# SharpTimer 路线图

本文件只记录后续计划和方向。当前项目状态见 `docs/project.md`，模块边界见 `docs/architecture.md`。

## 近期重点

- 对 MoYu32、GAN 与 QiYi 做真机长测，重点覆盖静置睡眠、走出范围、快速连续转动、复原中短暂丢包和自动重连后的状态恢复。
- 优化智能打乱推进的纠错提示，让用户更容易理解下一步操作。
- 补充智能魔方相关平台无关测试，尤其是打乱推进、READY 判定和复原判定。
- 继续整理 `MainWindow.xaml.cs` 中较重的界面编排；三页已拆为独立 View，智能魔方连接控制器已抽离，后续可继续拆分智能打乱推进、READY 起表和预览渲染联动。

## 后续计划

### 体验补齐

- 增加导入导出。
- 增强成绩编辑：备注、打乱文本修改、批量操作。
- 完善成绩分析区：ao50、ao100、session trend。
- 优化设置项和启动行为。
- 系统统一 UI padding、spacing 等设计 token（当前已完成语义画刷 token 化和圆角统一，散落字面量可按需收敛）。

### 智能魔方扩展

- 在 MoYu32、GAN 与 QiYi 保持稳定后，继续扩展 Giiker、GoCube 等协议。
- 为不同厂商协议建立更清晰的注册、识别和诊断机制。
- 继续完善 BLE 链路诊断信息，让断链、重连失败、写入失败和协议重同步能在调试时快速定位。
- 保持 BLE 兼容性逻辑在 `SharpTimer.Bluetooth` 内部，避免影响手动计时路径。
- 当前 3D 预览继续沿用 WinUI Canvas 实现；只有在交互或性能重新无法满足需要时，再评估 Win2D、`SwapChainPanel + Direct3D` 等更重路线。

### 架构整理

- 继续减轻 `MainWindow.xaml.cs`；当前三页已拆为独立 View（`TimerView`、`SolvesView`、`SettingsView`），智能魔方连接控制器已抽到 `SmartCubeSessionController`，智能打乱文本着色已抽到 `ScrambleTextPresenter`，蓝牙设备列表项构建已抽到 `BluetoothDeviceListItemFactory`。后续可继续拆分智能打乱推进、READY 起表和预览渲染联动。
- 对复杂 UI 状态引入更清晰的 service 或 ViewModel 边界。
- 保持 `SmartCubePreviewControl` 的外部接口稳定，后续渲染实现升级不应影响 BLE、Core 或主计时流程。
- 保持 Core / Storage / Bluetooth 的测试覆盖优先级高于 UI 细节。

## 参考资料

- `ref/WinUI-Gallery`：WinUI 控件、样式和窗口 API 参考。
- `ref/smartcube-web-bluetooth`：智能魔方 BLE 协议参考。
- `ref/cstimer`：计时规则、统计、session 和智能魔方行为参考。
