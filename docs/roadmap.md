# SharpTimer 路线图

## 当前状态

SharpTimer 已具备可用的本地计时闭环，并已接入 MoYu32 系列智能魔方的第一版 BLE 计时流程。

已稳定或基本可用的能力：

- 手动计时、观察、`+2`、`DNF`。
- session 管理、成绩保存、罚时修改、删除和基础统计。
- SQLite 本地持久化。
- 亮/暗主题、背景材质、显示精度和中英文界面设置。
- MoYu32 智能魔方连接、智能打乱推进、READY 后首转起表和复原完成保存成绩。

## 近期重点

- 实机验证 MoYu32 的连接稳定性、断开重连、状态同步延迟和成绩保存体验。
- 优化智能打乱推进的纠错提示，让用户更容易理解下一步操作。
- 补充智能魔方相关平台无关测试，尤其是打乱推进、READY 判定和复原判定。
- 继续整理 `MainWindow.xaml.cs` 中较重的界面编排，把复杂状态逐步抽到 service、ViewModel 或独立视图文件。

## 后续计划

### 体验补齐

- 增加导入导出。
- 增强成绩编辑：备注、打乱文本修改、批量操作。
- 增加更多筛选和统计视图，例如 ao50、ao100、session trend。
- 优化设置项和启动行为。

### 智能魔方扩展

- 在 MoYu32 稳定后，再扩展 GAN、Giiker、GoCube、QiYi 等协议。
- 为不同厂商协议建立更清晰的注册、识别和诊断机制。
- 保持 BLE 兼容性逻辑在 `SharpTimer.Bluetooth` 内部，避免影响手动计时路径。

### 架构整理

- 逐步减轻 `MainWindow.xaml.cs`。
- 对复杂 UI 状态引入更清晰的 service 或 ViewModel 边界。
- 保持 Core / Storage / Bluetooth 的测试覆盖优先级高于 UI 细节。

## 参考资料

- `ref/WinUI-Gallery`：WinUI 控件、样式和窗口 API 参考。
- `ref/smartcube-web-bluetooth`：智能魔方 BLE 协议参考。
- `ref/cstimer`：计时规则、统计、session 和智能魔方行为参考。
