# 智能魔方能力差距：SharpTimer vs DCTimer-BLE

本文只比较智能魔方能力维度，用于判断 SharpTimer 距离 `ref/DCTimer-Android-BLE` 还有哪些差距，以及后续补齐顺序。项目整体状态见 `docs/project.md`，架构边界见 `docs/architecture.md`。

## 当前基线

SharpTimer 当前已经具备智能魔方训练计时闭环：

- 支持 MoYu32、GAN v2/v3/v4、QiYi 系列智能魔方的基础连接、转动事件、facelets、battery 和断链事件。
- 支持智能打乱推进、READY 后首转起表、复原完成自动停止并保存成绩。
- 保存智能魔方成绩摘要字段：`MoveSequence`、`MoveCount`、`Tps`、`ReconstructionMethod`、`SolveMetaJson`。
- `solve_meta_json` 已记录 `rawMoves`、`prettySolve` 和 `phases`，可支撑后续重新分析和统计。
- 设置页已支持选择 `CFOP` / `Roux` 分段方式。
- 详情页已展示分段解法和分段表：阶段、用时、步数、TPS。

DCTimer-BLE 的智能魔方能力更成熟，主要优势在于实机验证、分段算法细节、方向设置、异常恢复和训练反馈完整度。

## 协议与 BLE 链路

SharpTimer 已有：

- BLE 扫描、连接、断开、保活和有限自动重连。
- GAN v4 history request、facelets 重同步兜底、move buffer。
- QiYi 历史转动补偿，并按时间戳拆分当前步和 future history 步。
- App 层本地 facelets 推演，用于转动动画、复原判断和减少频繁请求状态。

相对 DCTimer-BLE 的差距：

- 真机长测样本不足。DCTimer-BLE 的 QiYi / GAN / MoYu 链路经过更多实际训练场景验证。
- 缺帧恢复策略还不够系统化。SharpTimer 已有 GAN / QiYi 的补偿入口，但缺少统一的“缺失步估算、状态兜底、复原后校验”诊断模型。
- history 补回 move 的时间估算还比较基础。DCTimer-BLE 明确避免用固定极小 delta 影响成绩，SharpTimer 需要继续验证各协议缺帧时的 delta / elapsed 是否稳定。
- 断链、重连和睡眠恢复的 UI 诊断不够细。当前更多是内部恢复，用户和调试者缺少清晰原因提示。

建议优先级：

1. 对 MoYu32、GAN v4、QiYi 各做长测脚本和问题记录。
2. 为缺帧、future history、facelets resync 增加可测试的事件样例。
3. 增加 BLE 诊断日志或调试视图，记录协议、序号、history 请求和状态重同步。

## 转动序列与复盘数据

SharpTimer 已有：

- 使用 `SmartCubeSolveCapture` 持续记录 move history。
- READY 时标记 solve 起点，首转开始时能包含首步。
- 停止计时时截取本次 solve，并保存 raw move metadata。
- `MoveSequence` 保存整理后的展示序列，`rawMoves` 保存更细粒度事件。

相对 DCTimer-BLE 的差距：

- DCTimer-BLE 的 `SmartCube` 模型维护 `rawData + preIdx + solveStartState`，与协议状态更新结合更紧密；SharpTimer 目前 capture 更独立，优点是边界清楚，但还需要更多实机场景验证。
- SharpTimer 尚未保存 `solveStartState` 到 meta。后续如果要对旧成绩重新按不同方法分段，只有 scramble 推导的起始状态；若用户修改打乱或发生状态恢复偏差，重建可靠性会下降。
- 目前没有保存设备名、协议、固件版本等诊断字段到 solve meta。DCTimer-BLE 更强调硬件数据质量排查。

建议优先级：

1. 在 `solve_meta_json` 中加入 `startFacelets`、`deviceName`、`deviceProtocol`。
2. 增加“重新分析旧成绩”的内部 API：从 `rawMoves + startFacelets + method` 重新生成 phases。
3. 详情页增加复制完整解法和复制 raw meta 的调试入口。

## 解法分段算法

SharpTimer 已有：

- CFOP 分段：`Cross / F2L 1 / F2L 2 / F2L 3 / F2L 4 / OLL / PLL`。
- Roux 分段：`FB / SB / CMLL / L6E`。
- 使用 DCTimer-BLE 的 mask/progress 思路，根据 facelets 状态推进阶段。
- 结果写入 `solve_meta_json.phases`。

相对 DCTimer-BLE 的差距：

- DCTimer-BLE 有更完整的方向处理。SharpTimer 当前只使用所有整魔方方向变体寻找 progress，但没有用户可选的 solve orientation。
- DCTimer-BLE 对 slice combo 有专门处理窗口。SharpTimer 当前保存的是普通 `URFDLB` 转动合并，尚未把近似同时的反向层转转换为 `M/E/S` 类展示。
- DCTimer-BLE 的 `prettySolve` 更接近用户复盘文本，包含阶段注释和统计文本。SharpTimer 当前只做基础阶段文本。
- SharpTimer 的分段测试仍偏基础，缺少真实 CFOP / Roux 解法样例的 goldens。
- 当前阶段 bucket 算法还需要用真实数据验证边界：特别是 F2L pair 顺序、OLL/PLL 边界、Roux CMLL/L6E 边界。

建议优先级：

1. 加入 solve orientation 设置，至少支持默认、白底、黄底和自动。
2. 移植 DCTimer-BLE 的 slice combo 逻辑，保留 raw moves，同时生成更适合展示的 pretty moves。
3. 引入真实解法样例测试，覆盖 CFOP、Roux、带 AUF、带 slice、带取消步的情况。
4. 为 `SmartCubeSolveReconstruction` 增加“重建失败/低置信度”的标记，避免误导用户。

## 详情页与训练反馈

SharpTimer 已有：

- 详情页展示时间、步数、TPS、打乱、分段解法、日期、备注。
- 详情页展示分段表：阶段、用时、步数、TPS。

相对 DCTimer-BLE 的差距：

- 已按阶段展示 `prettySolve`，但分段表行还不能单独展开 / 折叠。
- 没有高亮最慢段、最低 TPS 段、占比异常段。
- 没有完整 pretty solve 视图和复制入口。
- 没有 alg.cubing.net 或类似复盘链接。
- 备注仍是纯文本，没有和分段、解法修正联动。

建议优先级：

1. 详情页支持复制完整解法。
2. 分段表增加占比、最慢段和最低 TPS 高亮。
3. 分段行可展开显示该段 moves。
4. 增加复盘链接或导出文本。

## 统计页与筛选

SharpTimer 已有：

- 基础成绩统计：best、worst、mean、completed。
- 趋势和分布图。
- 每条智能魔方成绩已保存步数、TPS 和 phases。

相对 DCTimer-BLE 的差距：

- 统计页还没有使用智能魔方复盘数据。
- 没有平均 TPS、最佳 TPS、平均步数、最少步数。
- 没有分段均值、最慢阶段趋势、阶段 TPS 趋势。
- 没有按 CFOP / Roux、是否含 rawMoves、是否含 phases 的筛选。
- 没有智能魔方成绩与手动成绩的明确筛选视图；虽然手动路径已降级，但历史数据仍可能混合。

建议优先级：

1. 增加智能魔方统计摘要：平均 TPS、最佳 TPS、平均步数、最少步数。
2. 增加分段均值：Cross/F2L/OLL/PLL 或 FB/SB/CMLL/L6E。
3. 趋势图支持时间、TPS、步数、分段耗时切换。
4. 增加数据筛选：智能魔方、有分段、有 rawMoves、方法类型。

## 设置与产品控制

SharpTimer 已有：

- 打乱推进样式、打乱字体、虚拟魔方大小。
- 解法分段方式：CFOP / Roux。

相对 DCTimer-BLE 的差距：

- 没有 solve orientation 设置。
- 没有分段重建策略设置，例如是否识别 slice、是否自动按方法识别、是否忽略最后 AUF。
- 没有设备诊断相关设置或调试开关。
- 没有按设备协议保存偏好。

建议优先级：

1. 加入 solve orientation 设置。
2. 加入复盘显示偏好：简洁 / 完整 / 调试。
3. 加入智能魔方诊断模式，显示协议事件和重同步信息。

## 数据兼容性

SharpTimer 已有：

- 结构化字段和 JSON 扩展字段并存，schema 目前不需要为分段继续加列。
- `solve_meta_json` 可以继续扩展。

相对 DCTimer-BLE 的差距：

- `solve_meta_json` 还没有正式版本化文档。
- 旧成绩重新生成 phases 的迁移策略未定义。
- 若未来调整 method id、phase 字段或 raw move 结构，需要兼容读取旧 JSON。

建议优先级：

1. 文档化 `solve_meta_json` v1 schema。
2. 读取 meta 时允许缺字段和旧字段。
3. 提供后台或手动入口，对含 rawMoves 的旧成绩补生成 phases。

## 推荐补齐顺序

1. 完善 `solve_meta_json`：加入 `startFacelets`、设备信息、schema version 说明。
2. 移植 DCTimer-BLE 的 solve orientation 和 slice combo 逻辑。
3. 扩充真实解法样例测试，先让分段算法可信。
4. 增强详情页复盘体验：pretty solve、展开阶段 moves、复制。
5. 增强统计页：TPS、步数、分段均值和趋势。
6. 做 MoYu32 / GAN / QiYi 真机长测，把协议恢复和时间估算补齐。

## 阶段判断

SharpTimer 当前已经完成智能魔方训练工具的核心数据闭环：连接、打乱、起停、保存转动序列、自动分段、详情展示。

距离 DCTimer-BLE 的主要差距不在“有没有字段”或“能不能分段”，而在成熟度：真实设备兼容性、异常恢复、方向和 slice 处理、分段算法样例覆盖、复盘展示深度，以及统计页对这些数据的利用。
