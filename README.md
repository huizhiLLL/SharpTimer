<div align="center">
  <img src="SharpTimer.App/Assets/Square150x150Logo.scale-200.png" alt="SharpTimer logo" width="128" height="128" />

  <h1>SharpTimer</h1>

  <p>
    支持智能魔方的 Windows 原生计时器
  </p>

  <p>
    <strong>中文</strong>
    ·
    <a href="README-en.md">English</a>
  </p>

  <p>
    <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
    <img alt="C#" src="https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white" />
    <img alt="WinUI 3" src="https://img.shields.io/badge/WinUI-3-0078D4?style=for-the-badge&logo=windows&logoColor=white" />
    <img alt="SQLite" src="https://img.shields.io/badge/SQLite-local-003B57?style=for-the-badge&logo=sqlite&logoColor=white" />
    <img alt="xUnit" src="https://img.shields.io/badge/xUnit-tests-5E2B97?style=for-the-badge" />
  </p>
</div>

SharpTimer 是一个基于 .NET 8、WinUI 3 和 SQLite 的 Windows 原生魔方计时器，具有基本的计时功能，并且支持 Moyu32 系列智能魔方

### 预览

![SharpTimer 主界面](.github/assets/sharptimer-main.png)

### 特点

- 原生 Windows 桌面体验，界面基于 WinUI 3 / Windows App SDK
- 支持空格计时、观察、判罚、成绩 session 管理等基础计时功能
- 已支持 Moyu32 系列智能魔方的计时接入（智能打乱推进）
- 提供亮/暗主题、Mica / Mica Alt / Acrylic 背景材质和中英切换

### 技术栈

| 分类 | 技术 |
| --- | --- |
| 客户端 | WinUI 3, Windows App SDK, XAML |
| 运行时 | .NET 8 |
| 语言 | C# |
| 存储 | SQLite |
| 蓝牙 | Windows BLE API |
| 测试 | xUnit |

### 致谢

- `ref/WinUI-Gallery`：官方 WinUI Gallery 示例，前端重要参考
- `ref/smartcube-web-bluetooth`：智能魔方蓝牙协议参考
- `ref/cstimer`：智能魔方蓝牙协议、基础计时功能参考
