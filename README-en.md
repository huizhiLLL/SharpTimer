<div align="center">
  <img src="SharpTimer.App/Assets/Square150x150Logo.scale-200.png" alt="SharpTimer logo" width="128" height="128" />

  <h1>SharpTimer</h1>

  <p>
    A native Windows timer with smart cube support
  </p>

  <p>
    <a href="README.md">中文</a>
    ·
    <strong>English</strong>
  </p>

  <p>
    <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
    <img alt="C#" src="https://img.shields.io/badge/C%23-12-239120?style=for-the-badge&logo=csharp&logoColor=white" />
    <img alt="WinUI 3" src="https://img.shields.io/badge/WinUI-3-0078D4?style=for-the-badge&logo=windows&logoColor=white" />
    <img alt="SQLite" src="https://img.shields.io/badge/SQLite-local-003B57?style=for-the-badge&logo=sqlite&logoColor=white" />
    <img alt="xUnit" src="https://img.shields.io/badge/xUnit-tests-5E2B97?style=for-the-badge" />
  </p>
</div>

SharpTimer is a native Windows speedcubing timer built with .NET 8, WinUI 3, and SQLite. It provides basic timing features and supports Moyu32 series smart cubes.

### Preview

![SharpTimer main interface](.github/assets/sharptimer-main.png)

### Features

- Native Windows desktop experience, with the UI built on WinUI 3 / Windows App SDK
- Supports basic timing features such as spacebar timing, inspection, penalties, and solve session management
- Supports timing integration for Moyu32 series smart cubes, including smart scramble progression
- Provides light / dark themes, Mica / Mica Alt / Acrylic backdrop materials, and Chinese / English switching

### Tech Stack

| Category | Technology |
| --- | --- |
| Client | WinUI 3, Windows App SDK, XAML |
| Runtime | .NET 8 |
| Language | C# |
| Storage | SQLite |
| Bluetooth | Windows BLE API |
| Testing | xUnit |

### Acknowledgements

- `ref/WinUI-Gallery`: official WinUI Gallery examples, an important frontend reference
- `ref/smartcube-web-bluetooth`: smart cube Bluetooth protocol reference
- `ref/cstimer`: smart cube Bluetooth protocol and basic timing feature reference
