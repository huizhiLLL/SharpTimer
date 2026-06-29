<h4 align="right"><strong><a href="README-en.md">English</a></strong> | 简体中文</h4>

<div align="center">
  <img src=".github/assets/sharptimer-logo.png" alt="SharpTimer logo" width="128" height="128" />

  <h1>SharpTimer</h1>
    
  <p>
    专为智能魔方打造的 WinUI 3 Windows 原生桌面计时器
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
- 面向智能魔方训练流程：连接设备、智能打乱推进、READY 后首转起表、复原完成自动保存
- 已支持 MoYu32、GAN v2/v3/v4 与 QiYi 系列智能魔方的基础接入
- 保存智能魔方转动序列、步数、TPS 和复盘元数据，为后续分段分析做准备
- 保留轻量手动计时作为备用输入和调试路径，但不作为核心产品方向
- 提供亮/暗主题、Mica / Mica Alt / Acrylic 背景材质和中英切换

### 许可证

GPL-3.0

### 致谢

- `WinUI-Gallery`：官方 WinUI Gallery 示例，前端参考
- `smartcube-web-bluetooth`：智能魔方蓝牙协议参考
- `cstimer`：智能魔方蓝牙协议和计时行为参考
