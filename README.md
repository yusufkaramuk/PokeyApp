# 👆 PokeyApp

<p align="left">
  <img src="https://img.shields.io/badge/C%23-%23239120.svg?style=for-the-badge&logo=c-sharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge" alt="MIT License" />
</p>

A lightweight Windows LAN "poke" application inspired by the old MSN Messenger nudge. When you click the button, the other computer shows a non-intrusive notification with a short sound — without stealing focus from whatever the user is doing.

Works between **two Windows computers on the same local network (LAN or Wi-Fi)**.

---

## What Does It Do?

You run PokeyApp on two computers on the same network. When you click **"Dürt!"** (Poke), the other person immediately receives a floating popup notification and a short sound — without any of their windows being minimized or interrupted. It's a quick, distraction-free way to get someone's attention.

---

## Features

- **One-click poke** — click "Dürt!" to instantly notify the other person
- **Focus-safe notifications** — popup appears using `WS_EX_NOACTIVATE` so it never steals focus or interrupts fullscreen apps
- **Auto peer discovery** — finds the other computer via UDP broadcast; no manual IP needed in most cases
- **Automatic reconnect** — recovers from disconnects with exponential backoff (2s → 4s → 8s → 30s)
- **Notification sound** — embedded WAV plays on receive (can be disabled in Settings)
- **Frameless dark UI** — minimal, draggable window that positions itself at the bottom-right corner
- **System tray support** — minimize to tray, stays alive in background
- **Persistent settings** — username, peer IP, port, and sound preference saved per-user in `%APPDATA%\PokeyApp\`

---

## Tech Stack

| Component | Technology | Notes |
|---|---|---|
| UI Framework | WPF (.NET 8) | `net8.0-windows`, XAML-based |
| Architecture | MVVM | `CommunityToolkit.Mvvm` source generators |
| DI / Hosting | `Microsoft.Extensions.Hosting` | All services managed via DI |
| Transport | TCP port 14191 | Length-prefixed JSON framing, symmetric connect |
| Discovery | UDP port 14190 | Broadcast + response, 30s interval |
| Logging | Serilog | File sink → `%APPDATA%\PokeyApp\logs\app-YYYYMMDD.log` |
| Serialization | `System.Text.Json` | |
| Testing | xUnit + FluentAssertions + Moq | 13 tests |

---

## Requirements

- **Windows 10 or 11** (x64)
- **[.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** — only needed to build from source
- **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)** — only needed for debug/framework-dependent runs; the published EXE is self-contained
- **Same local network** on both computers (LAN or Wi-Fi on the same subnet)

No internet connection required — the app communicates only on your local network.

---

## Installation (Recommended — End Users)

### Step 1 — Build the self-contained EXE

```powershell
dotnet publish src/PokeyApp/PokeyApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
```

This creates a single `publish/PokeyApp.exe` (~150 MB, includes .NET runtime, no installation of .NET required on the target machine).

### Step 2 — Run the installer (as Administrator)

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1
```

The installer:
- Copies the app to `%LOCALAPPDATA%\PokeyApp\`
- Creates a Start Menu shortcut
- Adds Windows Firewall inbound rules for TCP 14191 and UDP 14190

**Do this on both computers.**

### Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall
```

---

## Development Setup

```powershell
# 1. Clone the repository
git clone https://github.com/yusufkaramuk/PokeyApp.git
cd PokeyApp

# 2. Restore dependencies (runs automatically on build)
dotnet restore

# 3. Run in debug mode
dotnet run --project src/PokeyApp
```

> **Note:** .NET 8 SDK is required. No other tools or environment variables are needed for development.

---

## Environment Variables

PokeyApp has **no required environment variables**. All configuration is stored in `%APPDATA%\PokeyApp\appsettings.json` (created automatically on first run with safe defaults).

Default settings:

```json
{
  "LocalUsername": "<your machine name>",
  "PeerIpAddress": "",
  "TcpPort": 14191,
  "UdpPort": 14190,
  "SoundEnabled": true,
  "StartMinimized": false,
  "NotificationDurationSeconds": 4
}
```

To change settings, use the **⚙ Settings** button inside the app.

---

## Build / Test / Publish

```powershell
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run all tests (13 tests)
dotnet test

# Publish: self-contained single EXE for Windows x64
dotnet publish src/PokeyApp/PokeyApp.csproj `
  -c Release -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o publish/
```

---

## Usage: Connecting Two Computers

1. Install and run PokeyApp on **both** computers
2. On each computer, click **⚙ Settings**
3. Check the **"Keşfedilen cihazlar"** dropdown — if the other computer appears, select it and click **Kaydet**
4. If auto-discovery doesn't work: enter the other machine's IP address in the **"Manuel IP adresi"** field
5. The status dot turns **green** when connected
6. Click **"Dürt!"** to send a poke

---

## Project Structure

```
PokeyApp/
├── src/
│   └── PokeyApp/
│       ├── Infrastructure/      # AppSettings model, JSON-based ConfigurationService
│       ├── Messages/            # PokeMessage, DiscoveryBeacon, DiscoveryResponse records
│       ├── Native/              # P/Invoke declarations (WS_EX_NOACTIVATE, SetWindowPos)
│       ├── Resources/           # tray icons (.ico), notification sound (.wav), color theme
│       ├── Services/            # AudioService, ConnectionService, DiscoveryService,
│       │                        # NotificationService, PokeService
│       ├── Transport/           # ITransport, TcpTransport, MessageFramer, TransportState
│       ├── Tray/                # TrayIconManager (NotifyIcon wrapper)
│       ├── ViewModels/          # MainViewModel, SettingsViewModel, NotificationViewModel,
│       │                        # TrayViewModel (CommunityToolkit.Mvvm)
│       ├── Views/               # MainWindow, NotificationWindow, SettingsWindow (XAML)
│       ├── App.xaml(.cs)        # Application entry, DI container, service wiring
│       └── Program.cs           # Custom [STAThread] Main with startup error capture
├── tests/
│   └── PokeyApp.Tests/
│       ├── Services/            # PokeService serialization tests
│       └── Transport/           # MessageFramer unit tests, TcpTransport integration tests
├── .gitignore
├── install.ps1                  # Windows installer (copies app, creates shortcut, firewall)
├── create-icons.ps1             # Generates .ico resource files (run once during dev setup)
├── create-wav.ps1               # Generates poke.wav notification sound (run once)
├── LICENSE
├── README.md
└── PokeyApp.sln
```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| Connection stays **orange** (Connecting) | Firewall may be blocking the port. Run `install.ps1` as Administrator on both machines. |
| Other computer not in discovery dropdown | Use manual IP. Make sure both computers are on the same subnet. Some routers block UDP broadcast between subnets. |
| No sound on notification | Settings → uncheck then re-check "Bildirim sesi". Check Windows volume mixer. |
| App window not visible | It opens frameless at bottom-right corner. Check all monitors. Use Alt+Tab. |
| "Access denied" running install.ps1 | Right-click PowerShell → "Run as Administrator", then run the script. |
| Two computers can't connect even with manual IP | Verify `ping <ip>` works between them. Some Wi-Fi routers block client-to-client traffic. |

**Log file location:** `%APPDATA%\PokeyApp\logs\app-YYYYMMDD.log`

---

## Security

- PokeyApp communicates **only on your local network** (LAN/Wi-Fi). It opens no internet connections.
- TCP port 14191 and UDP port 14190 are used. The installer adds inbound firewall rules scoped to **Private and Domain** network profiles only (not Public).
- No authentication is implemented — any PokeyApp instance on the same subnet can send pokes. This is by design for a LAN-only tool.
- No data is stored on disk except `appsettings.json` (contains username and peer IP — no passwords or tokens).

---

## Versioning

This project uses [Semantic Versioning](https://semver.org/) in **pre-v1** form. The app is a functional MVP but not yet `v1.0.0` — multi-user support, tray icon stability, and UX polish are planned before a stable release.

| Version | Milestone |
|---|---|
| `v0.1.0` | TCP transport, message framing, base UI and notification window |
| `v0.2.0` | UDP peer discovery, Settings UI, full service integration |
| `v0.3.0` | Startup stability fixes, deterministic TCP role assignment, tests, installer, docs |

Current release: **v0.3.0**

---

## License

MIT License — see [LICENSE](LICENSE) for details.

---

## Contributing

Bug reports and pull requests are welcome.
Open an issue at [github.com/yusufkaramuk/PokeyApp/issues](https://github.com/yusufkaramuk/PokeyApp/issues).
