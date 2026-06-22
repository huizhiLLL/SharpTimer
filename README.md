<h4 align="right"><strong><a href="README-en.md">English</a></strong> | 简体中文</h4>

<div align="center">
  <img src=".github/assets/sharptimer-logo.png" alt="SharpTimer logo" width="128" height="128" />

  <h1>SharpTimer</h1>
    
  <p>
    基于 WinUI 3 的 Windows 原生魔方计时器，支持智能魔方
  </p>
  
  <p>
    <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
    <img alt="C#" src="https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white" />
    <img alt="WinUI 3" src="https://img.shields.io/badge/WinUI-3-0078D4?style=for-the-badge&logo=windows&logoColor=white" />
    <img alt="SQLite" src="https://img.shields.io/badge/SQLite-local-003B57?style=for-the-badge&logo=sqlite&logoColor=white" />
    <img alt="xUnit" src="https://img.shields.io/badge/xUnit-tests-5E2B97?style=for-the-badge" />
    <img alt="License GPL-3.0" src="https://img.shields.io/badge/License-GPL--3.0-blue?style=for-the-badge" />
  </p>
</div>

![SharpTimer 主界面](.github/assets/sharptimer-main.png)

---

### 特点

- 原生 Windows 桌面体验，界面基于 WinUI 3 / Windows App SDK
- 支持空格计时、观察、判罚、成绩 session 管理等基础计时功能
- 已支持 Moyu32 系列智能魔方的计时接入（智能打乱推进）
- 提供亮/暗主题、Mica / Mica Alt / Acrylic 背景材质和中英切换

### 测试

```powershell
.\scripts\package-test.ps1
```

运行还原、测试、打包和压缩

### 许可证

GPL-3.0

### 致谢

- `WinUI-Gallery`：官方 WinUI Gallery 示例，前端参考
- `smartcube-web-bluetooth`：智能魔方蓝牙协议参考
- `cstimer`：智能魔方蓝牙协议、基础计时功能参考
