# SharpTimer

SharpTimer 是一个 Windows 原生魔方计时器，目标是先做好本地手动计时体验，再接入智能魔方蓝牙计时。

## 特点

- Windows 原生客户端，基于 WinUI 3。
- 手动计时核心与界面分离，便于测试和后续接入硬件。
- 支持 15 秒观察、`+2`、`DNF` 等 WCA 风格计时规则。
- 提供基础统计模型：best、mean、ao5、ao12。
- 后续计划使用 SQLite 保存 session 和成绩。
- 后续计划接入 GAN、Giiker、GoCube、MoYu、QiYi 等智能魔方 BLE 协议。

## 技术栈

- .NET 8
- WinUI 3 / Windows App SDK
- SQLite
- Windows BLE API

## 项目结构

```text
SharpTimer
├─ SharpTimer.App          WinUI 3 客户端
├─ SharpTimer.Core         计时器核心模型、状态机、统计
├─ SharpTimer.Storage      SQLite 存储
├─ SharpTimer.Bluetooth    智能魔方 BLE 接入
└─ SharpTimer.Tests        核心逻辑测试
```

## 开发环境

- Windows 10 1809 或更高版本
- Visual Studio 2022，安装 WinUI / Windows App SDK 相关工作负载
- .NET SDK 8 或更高版本

## 常用命令

```powershell
dotnet restore SharpTimer.slnx
dotnet build SharpTimer.slnx
```

## 文档

- [架构设计](docs/architecture.md)
- [路线图](docs/roadmap.md)

## 参考资料

- `ref/smartcube-web-bluetooth`：智能魔方蓝牙协议参考。
- `ref/cstimer`：计时状态、观察时间、罚时、统计和 session 概念参考。
