; ══════════════════════════════════════════════════════════════════════════════
;  SwiftLaunch — Inno Setup Script
;
;  Repo structure this file is written for:
;    repo/
;    ├── assets/app.ico
;    ├── installer/setup.iss        ← this file
;    ├── installer/Output/          ← installer EXE output
;    ├── publish/SwiftLaunch.exe    ← dotnet publish output
;    └── src/swiftlaunch/...
;
;  How to build manually (run from repo root):
;    iscc /DAPP_VERSION=1.0.0 installer\setup.iss
;
;  CI passes APP_VERSION automatically — never edit the version here.
; ══════════════════════════════════════════════════════════════════════════════

#ifndef APP_VERSION
  #define APP_VERSION "1.0.0"
#endif

#define AppName        "SwiftLaunch"
#define AppPublisher   "IMESH"
#define AppURL         "https://github.com/your-github-username/SwiftLaunch"
#define AppExeName     "SwiftLaunch.exe"
#define AppDescription "A lightning-fast keyboard-driven folder launcher for Windows"

[Setup]
; ── Identity ──────────────────────────────────────────────────────────────────
AppId               = {{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}}
AppName             = {#AppName}
AppVersion          = {#APP_VERSION}
AppVerName          = {#AppName} v{#APP_VERSION}
AppPublisher        = {#AppPublisher}
AppPublisherURL     = {#AppURL}
AppSupportURL       = {#AppURL}/issues
AppUpdatesURL       = {#AppURL}/releases

; ── Output ────────────────────────────────────────────────────────────────────
; CI expects:  installer\Output\SwiftLaunch-vX.X.X.exe
OutputDir           = Output
OutputBaseFilename  = SwiftLaunch-v{#APP_VERSION}

; ── Install location ──────────────────────────────────────────────────────────
DefaultDirName      = {autopf}\{#AppName}
DefaultGroupName    = {#AppName}
DisableProgramGroupPage = yes

; ── Installer appearance ──────────────────────────────────────────────────────
; setup.iss is in installer/
; assets/app.ico is one level up, then into assets/
SetupIconFile       = ..\assets\app.ico
WizardStyle         = modern
WizardSizePercent   = 110

; ── Compression ───────────────────────────────────────────────────────────────
Compression         = lzma2/ultra64
SolidCompression    = yes
LZMAUseSeparateProcess = yes

; ── Privileges ────────────────────────────────────────────────────────────────
PrivilegesRequired  = lowest
PrivilegesRequiredOverridesAllowed = dialog

; ── Minimum Windows version (Windows 10 x64) ─────────────────────────────────
MinVersion          = 10.0.17763
ArchitecturesAllowed            = x64
ArchitecturesInstallIn64BitMode = x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Published EXE is at: publish\SwiftLaunch.exe  (repo root)
; setup.iss is in:     installer\
; So relative path is: ..\publish\SwiftLaunch.exe
Source: "..\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"; Comment: "{#AppDescription}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\SwiftLaunch"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"
Root: HKCU; Subkey: "Software\SwiftLaunch"; ValueType: string; ValueName: "Version"; ValueData: "{#APP_VERSION}"

[Run]
Filename:    "{app}\{#AppExeName}"
Description: "Launch SwiftLaunch now"
Flags:       nowait postinstall skipifsilent

[UninstallRun]
Filename:    "taskkill.exe"
Parameters:  "/f /im {#AppExeName}"
Flags:       runhidden skipifdoesntexist

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SwiftLaunch"
