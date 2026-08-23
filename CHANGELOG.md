# Changelog

All notable changes to SwiftLaunch will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Planned
- Settings UI with configurable hotkey
- File search (not just folders)
- Portable mode (no registry writes)
- Network drive support

## [v1.0.1] - 2026-08-23

### Fixed
- Enter now launches a suggestion after it is selected with a mouse click
- Installed app shortcuts use the SwiftLaunch application icon

---

## [v1.0.0] — 2025-07-05

Initial release of SwiftLaunch.

### Added
- Global hotkey (Ctrl+Space) to toggle the launcher from any application
  - Automatic fallback to Alt+Space, then Ctrl+Shift+Space if Ctrl+Space is taken
- Folder search across all fixed and removable drives
- SQLite-backed folder index stored in `%LOCALAPPDATA%\SwiftLaunch\index.db`
- Hot cache: top 200 recently opened folders surfaced instantly without a DB query
- Acronym matching (e.g. `ml` matches `MyLibrary`)
- Exact, prefix, and contains scoring tiers for ranked suggestions
- **Open in File Explorer** — single-word search, press Enter
- **Open in VS Code** — append standalone `v` to any search (`folder v` or `v folder`)
- **Create folder** — `child parent` syntax creates a new folder inside an existing one
- **Create + open in VS Code** — `child parent v` or `v child parent`
- Mode indicator badge (Folder / VS Code / New Folder / New+Code) in the launcher UI
- Suggestion list with keyboard navigation (↑ / ↓ / Enter / Escape)
- Parent folder suggestions while typing in create mode
- System tray icon with context menu (Open, Re-index, Run on Startup, Exit)
- Automatic startup registration on first launch via `HKCU\...\Run`
- Toggle "Run on Startup" from the tray menu
- Single-instance guard using a named Mutex
- Background folder indexer with 12-hour re-index interval
- Manual re-index from tray menu
- FileSystemWatcher on each drive — detects Created, Deleted, and Renamed events in real time
- Surgical rename handling: DB rows updated in-place (preserving open count and recency) without a full re-index
- Stale entry filter: folders that no longer exist are removed from suggestions automatically
- Self-contained single-file publish targeting Windows x64 with no runtime dependency

### Changed
- N/A (initial release)

### Fixed
- N/A (initial release)

---

<!-- Links -->
[Unreleased]: https://github.com/your-username/SwiftLaunch/compare/v1.0.1...HEAD
[v1.0.1]: https://github.com/your-username/SwiftLaunch/releases/tag/v1.0.1
[v1.0.0]: https://github.com/your-username/SwiftLaunch/releases/tag/v1.0.0
