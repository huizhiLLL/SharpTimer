# SharpTimer 路线图

## 当前阶段判断

先做本地手动计时器完整可用，再接入智能魔方蓝牙。原因是 BLE 协议和 Windows 权限调试不确定性较高，提前接入会拖慢基础体验闭环。

前端实现统一以 WinUI 3 官方控件和 Windows App SDK API 为主。新增或调整界面时，优先查本地官方示例 `ref/WinUI-Gallery`，用它作为控件选择、XAML 结构、样式资源和交互模式的重要参考。

## 第一阶段：本地手动计时器

目标：不用智能硬件也能完整记录和复盘成绩。

- 建立 `SharpTimer.Core`，实现计时状态机、成绩模型、罚时模型和基础统计。
- 建立 `SharpTimer.Tests`，覆盖状态机、成绩罚时和统计规则。
- 建立 `SharpTimer.Storage`，固定 SQLite v1 schema、迁移入口和 session/solve 仓储。
- 建立 `SharpTimer.App.Services`，串联状态机、SQLite 仓储、默认 session 和统计快照。
- WinUI 3 主界面使用官方 `NavigationView` 侧边栏结构，拆分主计时、成绩列表和设置。
- UI 控件和页面模式对照 `ref/WinUI-Gallery`，避免自制伪 Fluent 控件。
- 页面切换使用轻量进入动画；严格 DrillIn 过渡留到页面拆分后接入。
- WinUI 3 主界面支持按下空格进入 Ready、松开空格开始观察/复原、运行中按下空格停止计时。
- 主计时区显示三阶打乱，支持左右方向键切换打乱，并在成绩保存时记录当前打乱。
- 支持 15 秒观察、`+2`、`DNF`。
- 支持 session 创建、切换、重命名和归档。
- 支持少量本地设置：是否启用观察、显示精度、亮/暗主题、中英文界面语言；默认英文、亮色、百分秒。
- 使用 SQLite 保存 session 和 solve。
- 展示成绩列表、best、mean、ao5、ao12。

## 第二阶段：存储与体验补齐

目标：让本地计时器更接近日常可用。

- 扩展 `SharpTimer.Storage`，支持更多设置项和导入导出。
- 添加成绩编辑：修改罚时、删除成绩、备注、打乱文本。
- 添加更多基础设置：默认项目、启动行为、显示偏好。
- 对成绩编辑、导入导出、设置页等新 UI，优先复用 WinUI Gallery 中的官方 `ContentDialog`、`CommandBar`、`InfoBar`、输入控件和列表模式。
- 扩展测试覆盖存储迁移和 UI 输入编排。

## 第三阶段：智能魔方 BLE

目标：在本地计时稳定后接入智能魔方。

- 添加 `SharpTimer.Bluetooth`。
- 参考 `ref/smartcube-web-bluetooth` 迁移 GAN、Giiker、GoCube、MoYu、QiYi 等协议。
- 设计 `ISmartCubeProtocol`、`SmartCubeEvent`、`SmartCubeConnection`。
- 先做设备扫描和连接诊断界面，再做转动事件与计时器联动；诊断界面优先参考 `ref/WinUI-Gallery` 中列表、状态提示、进度和设置类控件示例。

## 第四阶段：统计增强

目标：提供更接近专业计时器的复盘能力。

- 增加 ao50、ao100、session trend。
- 增加按项目、日期、session 的筛选。
- 增加导入导出。
- 视情况增加打乱生成器和训练工具。
