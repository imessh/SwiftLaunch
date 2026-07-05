# SwiftLaunch

> A lightning-fast folder launcher for Windows — lives in your system tray, opens with a keystroke.

![SwiftLaunch Banner](docs/screenshots/banner.png)

---

## Description

SwiftLaunch is a keyboard-driven folder launcher for Windows 10/11. Press **Ctrl+Space** from anywhere on your desktop to instantly search, open, and create folders — without ever touching your mouse. It indexes your drives in the background, learns from your usage, and keeps its index in sync with the filesystem in real time.

---

## Features

- **Global hotkey** — Ctrl+Space launches the search bar from any application
- **Instant search** — fuzzy + acronym matching against a local SQLite index
- **Recent & frequent folders** — hot cache surfaces your most-used folders at the top
- **Open in File Explorer** — single-word search, press Enter
- **Open in VS Code** — append `v` to any search to open the result in VS Code
- **Create folders** — type `child parent` to create a new folder inside an existing one
- **Create + open in VS Code** — add `v` to a create command to open it in VS Code on success
- **Live filesystem sync** — FileSystemWatcher detects renames, creates, and deletes while the app is running
- **Stale entry cleanup** — entries whose paths no longer exist are automatically removed from suggestions
- **System tray** — runs silently in the background; double-click the tray icon to open
- **Run on startup** — registers itself at Windows startup automatically on first launch; can be toggled from the tray menu
- **Single instance** — prevents duplicate processes
- **Self-contained publish** — ships as a single `.exe` with no external dependencies

---

## Screenshots

> _Screenshots coming soon._

| Search | Create | VS Code |
|--------|--------|---------|
| ![Search](docs/screenshots/search.png) | ![Create](docs/screenshots/create.png) | ![VSCode](docs/screenshots/vscode.png) |

---

## Requirements

| Requirement | Version |
|-------------|---------|
| Windows | 10 or 11 (x64) |
| .NET SDK | 8.0 or later |
| Visual Studio Code | Any (optional, for VS Code open feature) |

---

## Installation

### Option A — Download release (recommended)

1. Download `SwiftLaunch.exe` from the [Releases](../../releases) page.
2. Place it anywhere on your machine (e.g. `C:\Tools\SwiftLaunch\`).
3. Run it once — it registers itself for Windows startup automatically.
4. Press **Ctrl+Space** to open the launcher.

### Option B — Build from source

See [Build Instructions](#build-instructions) below.

---

## Build Instructions

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 x64

### Clone and build

```bash
git clone https://github.com/your-username/SwiftLaunch.git
cd SwiftLaunch
dotnet build -c Release
```

The compiled output will be in:

```
bin/Release/net8.0-windows/
```

### Run locally (debug)

```bash
dotnet run
```

---

## Publishing Instructions

SwiftLaunch is configured for a self-contained, single-file publish targeting `win-x64`.

```bash
dotnet publish -c Release
```

The single executable will be output to:

```
bin/Release/net8.0-windows/win-x64/publish/SwiftLaunch.exe
```

No installer or runtime is required on the target machine.

> Publishing settings are defined in `SwiftLaunch.csproj`:
> `PublishSingleFile`, `SelfContained`, `RuntimeIdentifier=win-x64`, `PublishReadyToRun`, `EnableCompressionInSingleFile`.

---

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **Ctrl+Space** | Toggle launcher open / close |
| **Enter** | Execute selected suggestion |
| **↑ / ↓** | Navigate suggestion list |
| **Escape** | Close launcher |

> If Ctrl+Space is taken by another application, SwiftLaunch automatically falls back to **Alt+Space**, then **Ctrl+Shift+Space**.

---

## Command Syntax

SwiftLaunch interprets what you type using token count and the standalone flag `v`. No prefixes are needed.

| Input | Tokens | Action |
|-------|--------|--------|
| `Downloads` | 1 word | Open **Downloads** in File Explorer |
| `Downloads v` | 1 word + `v` | Open **Downloads** in VS Code |
| `v Downloads` | `v` + 1 word | Open **Downloads** in VS Code |
| `MyProject Documents` | 2 words | Create folder **MyProject** inside **Documents** |
| `MyProject Documents v` | 2 words + `v` | Create **MyProject** inside **Documents**, then open in VS Code |
| `v MyProject Documents` | `v` + 2 words | Same as above |

### Rules

- `v` is a **standalone flag only** — the token must be exactly `v` (case-insensitive).
- Words like `vfolder`, `dev`, `childv` are treated as regular search terms, never as the flag.
- VS Code only opens **after a successful operation** — never on failure or if the folder already exists.
- In create mode, the suggestion list shows matching **parent folders** as you type the second word.

---

## Project Structure

```
SwiftLaunch/
├── App.xaml                  # WPF application entry point
├── App.xaml.cs               # Startup, tray icon, hotkey wiring, single-instance guard
├── LauncherWindow.xaml       # Search UI layout
├── LauncherWindow.xaml.cs    # Input parsing, search dispatch, command execution
├── FolderIndexer.cs          # SQLite index, FileSystemWatcher, hot cache, search logic
├── HotkeyManager.cs          # Global hotkey registration via Win32 RegisterHotKey
├── StartupManager.cs         # Windows registry startup enable / disable
├── SwiftLaunch.csproj        # Project file (.NET 8, WPF + WinForms, single-file publish)
├── .editorconfig             # C# formatting rules
├── .gitignore                # Git ignore rules
├── CHANGELOG.md              # Version history
├── CONTRIBUTING.md           # Contribution guidelines
├── LICENSE                   # MIT License
└── README.md                 # This file
```

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Data.Sqlite` | 8.0.0 | Local folder index database |
| `System.Drawing.Common` | 8.0.0 | Programmatic tray icon rendering |

Both are bundled into the published executable — no installation required on the end-user machine.

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

---

## Future Roadmap

- [ ] Settings UI (configurable hotkey, index exclusion list, theme)
- [ ] File search in addition to folder search
- [ ] Plugin / extension API
- [ ] Recent files (not just folders)
- [ ] Network drive support
- [ ] Portable mode (no registry writes)
- [ ] Automated installer (WiX / Inno Setup)
- [ ] CI/CD pipeline (GitHub Actions)
