<h4 align="right">English | <strong><a href="README.md">简体中文</a></strong></h4>

<div align="center">
  <img src=".github/assets/sharptimer-logo.png" alt="SharpTimer logo" width="128" height="128" />

  <h1>SharpTimer</h1>
    
  <p>
    A native WinUI 3 desktop timer for smart cube training on Windows
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

![SharpTimer main interface](.github/assets/sharptimer-main.png)

---

### Features

- Native Windows desktop experience, with the UI built on WinUI 3 / Windows App SDK
- Built around smart cube training: connect a cube, follow smart scramble progression, start on the first READY turn, and save solves automatically
- Supports basic integration for MoYu32, GAN v2/v3/v4, and QiYi smart cubes
- Saves smart cube move sequences, move count, TPS, and replay metadata as the base for future phase analysis
- Keeps lightweight manual timing as a fallback and debugging path, not as the core product direction
- Provides light / dark themes, Mica / Mica Alt / Acrylic backdrop materials, and Chinese / English switching

GPL-3.0

### Acknowledgements

- `WinUI-Gallery`: official WinUI Gallery examples, frontend reference
- `smartcube-web-bluetooth`: smart cube Bluetooth protocol reference
- `cstimer`: smart cube Bluetooth protocol and timing behavior reference
