<div align="center">

<div style="display: flex; align-items: center; gap: 10px; justify-content: center;">
    <img src="https://ericlostie-launcher.jagoba.dev/public/imgs/logo-launcher.png" width="45" height="45" alt="LostieLauncher icon" />
    <h1 style="margin: 20 0;">LostieLauncher</h1>
</div>

<p align="center">
  <a href="https://ericlostie-launcher.jagoba.dev/public/installer/LostieLauncher-win-Setup.exe">
    <img src="https://img.shields.io/badge/Download%20Installer-Lostie%20Launcher-blue?style=for-the-badge" alt="Download Lostie Launcher" />
  </a>
</p>

**Modern game launcher for Windows**

[![Version](https://img.shields.io/badge/version-0.9.1-blue?style=flat-square)](releases/) [![Platform](https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square&logo=windows)](https://microsoft.com/windows) [![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/) [![WPF](https://img.shields.io/badge/UI-WPF-68217A?style=flat-square)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/) [![Velopack](https://img.shields.io/badge/updates-Velopack-FFE084?style=flat-square)](https://velopack.io/) [![License](https://img.shields.io/badge/license-see%20LICENSE-lightgrey?style=flat-square)](LICENSE.txt)

Discover, download, install, and manage your games from a clean interface with multi-language support.

</div>

---

## ✨ Features

| Feature                       | Description                                                                             |
| ----------------------------- | --------------------------------------------------------------------------------------- |
| 🎮 **Game library**           | Browse the full catalog of available games from the content server                      |
| ⬇️ **Resumable downloads**    | Supports pausing and resuming downloads with `.part` files and a real-time progress bar |
| 🔑 **Key system**             | Access to exclusive content via download keys validated by the server                   |
| 🕹️ **My games**               | Dedicated view with installed games, version tracking and playtime                      |
| 🔄 **Automatic updates**      | Delta updates with [Velopack](https://velopack.io/) — the launcher updates itself       |
| 📰 **News and notifications** | News feed and announcements from the home screen                                        |
| ❓ **FAQs**                   | Searchable frequently-asked-questions view with localized, collapsible entries           |
| 🎨 **Themes**                 | 10 swappable visual themes: **Volcarona**, **Zoroark**, **Infernape**, **Torterra**, **Empoleon**, **Mewtwo**, **Cefireon**, **Sylveon**, **Astrem**, and **Auretoskos** |
| 🌍 **Multi-language**         | 8 available languages with full UI translations                                         |
| 💾 **Save shortcut**          | Quick-access button to open the save folder of an installed game                        |
| 📺 **Social links**           | Direct buttons to open EricLostie's Twitch, YouTube, and Twitter from the main UI       |
| 🖥️ **System tray**            | Minimize to tray with a context menu (Open / Exit)                                      |
| 🚀 **Start with Windows**     | Option to launch the launcher on login, in normal or minimized mode                     |

---

## 🌍 Supported languages

Español · English · Català · Euskera · Galego · Português · Valencià · Français

The language is selected in the settings and applied dynamically throughout the application.

---

> [!NOTE]
> The following sections are intended for developers.

## 🏗️ Architecture

The project follows the **MVVM** pattern with centralized **Dependency Injection**.

```
LostieLauncher/
├── Core/               # DI container configuration
├── Models/             # Data models
├── Services/           # Service layer
├── ViewModels/         # ViewModels with CommunityToolkit.Mvvm
├── Views/              # Windows, dialogs and WPF components
│   ├── Components/     # GameCard, NewsCard, NotificationCard, FaqCard (+ skeletons)
│   ├── Dialogs/        # DownloadConfirmDialog, WelcomeDialog, CustomMessageBox, SpecialVersionDialog
│   └── Partials/       # GamesView, HomeView, LibraryView, FaqsView, SettingsView
├── Converters/         # XAML value converters
├── Styles/             # Global styles
├── Themes/             # Theme resources
├── Content/            # Localized strings
├── Utils/              # Logging and process utilities
└── Assets/             # Icons and graphic resources
```

### Main services

| Service                  | Responsibility                                                |
| ------------------------ | ------------------------------------------------------------- |
| `IContentService`        | Fetches the game catalog, news, and registers installed games |
| `IDownloadService`       | Manages downloads, file extraction, and the key system        |
| `ISettingsService`       | Loads and persists configuration in `launcher_settings.json`  |
| `IWindowsStartupService` | Integration with the Windows registry for automatic startup   |
| `IUpdateGateway`         | Seam over the Velopack update manager; checks for app updates  |
| `IUpdateNotifier`        | Prompts the user to apply/restart via WPF dialogs             |
| `IUpdateService`         | Orchestrates the update check flow (gateway + notifier)        |

### ViewModels

| ViewModel           | View                                           |
| ------------------- | ---------------------------------------------- |
| `MainViewModel`     | Main navigation hub                            |
| `HomeViewModel`     | Home screen with news and notifications        |
| `LibraryViewModel`  | Available game catalog and download management |
| `GamesViewModel`    | Installed games                                |
| `FaqsViewModel`     | Searchable FAQ list                            |
| `SettingsViewModel` | Settings panel                                 |
| `GlobalViewModel`   | Shared global state                            |

---

## 🛠️ Technologies

| Package                                                                                                             | Version  | Usage                                             |
| ------------------------------------------------------------------------------------------------------------------- | -------- | ------------------------------------------------- |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)                                                 | 8.4.2    | MVVM with `ObservableProperty` and `RelayCommand` |
| [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks)                                       | 6.2.1    | Vector icons in the UI                            |
| [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) | 10.0.9   | IoC container                                     |
| [Microsoft.Extensions.Http](https://www.nuget.org/packages/Microsoft.Extensions.Http)                               | 10.0.9   | `IHttpClientFactory` with named clients           |
| [SharpCompress](https://github.com/adamhathcock/sharpcompress)                                                      | 0.49.1   | ZIP/7z extraction of downloaded files             |
| [Velopack](https://velopack.io/)                                                                                    | 0.0.1298 | Automatic delta update system                     |

---

## 🚀 Build and publish

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows 10/11
- PowerShell 7+ (for the release script)
- `vpk` Velopack CLI installed globally

### Development build

```powershell
cd LostieLauncher
dotnet build
```

### Release build (local)

```powershell
.\scripts\build-release.ps1
```

Artifacts are generated in `releases/`:

- `LostieLauncher-0.9.1-full.nupkg` — initial installation package
- Delta packages (on successive builds)
- `releases.win.json` — update manifest
- `RELEASES` — Velopack metadata

### Release build with server upload

```powershell
.\scripts\build-release.ps1 -Upload -SshHost "user@my-server.com" -SshPath "/var/www/installer/"
```

---

## ⚙️ Configuration

Configuration is automatically saved to `launcher_settings.json` in `%APPDATA%\LostieLauncher\` (settings from older versions stored next to the executable are migrated automatically).

| Option              | Type          | Default      | Description                                  |
| ------------------- | ------------- | ------------ | -------------------------------------------- |
| `Language`          | `AppLanguage` | `Esp`        | Interface language                           |
| `Theme`             | `AppTheme`    | `Volcarona`  | Visual theme                                 |
| `StartWithWindows`  | `bool`        | `false`      | Launch on Windows startup                    |
| `StartMinimized`    | `bool`        | `false`      | Start in the system tray                     |
| `AutoUpdate`        | `bool`        | `false`      | Check for updates on startup                 |
| `DownloadDirectory` | `string`      | My Documents | Game installation folder                     |
| `HasSeenWelcome`    | `bool`        | `false`      | Controls whether the welcome dialog is shown |

---

## 📡 API Endpoints

| Endpoint                                                                 | Description                        |
| ------------------------------------------------------------------------ | ---------------------------------- |
| `https://ericlostie-launcher.jagoba.dev/games/listado.json`              | Available game catalog             |
| `https://cdn.jagoba.dev/ericlostie-launcher/homepage-notifications.json` | Home screen news and notifications |
| `https://cdn.jagoba.dev/ericlostie-launcher/flag.txt`                     | Maintenance flag (gates server actions) |
| `https://ericlostie-launcher.jagoba.dev/games`                           | Base URL for downloads             |
| `https://ericlostie-launcher.jagoba.dev/public/installer/`               | Velopack update feed               |

---

<div align="center">

LostieLauncher © 2026

</div>
