; ══════════════════════════════════════════════════════════════════════════════
;  SwiftLaunch — Inno Setup Script
;
;  How to build manually:
;    iscc /DAPP_VERSION=1.0.0 installer\setup.iss
;
;  The CI pipeline passes APP_VERSION automatically via /DAPP_VERSION=X.X.X
;  so you never need to edit this file when bumping the version.
; ══════════════════════════════════════════════════════════════════════════════

#ifndef APP_VERSION
  ; Fallback for local manual builds — keep this in sync with your csproj.
  #define APP_VERSION "1.0.0"
#endif

#define AppName        "SwiftLaunch"
#define AppPublisher   "IMESH"
#define AppURL         "https://github.com/imessh/SwiftLaunch"
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
; The CI workflow expects the file at installer\Output\SwiftLaunch-vX.X.X.exe
OutputDir           = Output
OutputBaseFilename  = SwiftLaunch-v{#APP_VERSION}

; ── Install location ──────────────────────────────────────────────────────────
DefaultDirName      = {autopf}\{#AppName}
DefaultGroupName    = {#AppName}
DisableProgramGroupPage = yes

; ── Installer appearance ──────────────────────────────────────────────────────
SetupIconFile       = ..\assets\app.ico
WizardStyle         = modern
WizardSizePercent   = 110

; ── Compression ───────────────────────────────────────────────────────────────
Compression         = lzma2/ultra64
SolidCompression    = yes
LZMAUseSeparateProcess = yes

; ── Privileges ────────────────────────────────────────────────────────────────
; PrivilegesRequired=lowest installs per-user without UAC prompt.
; Change to "admin" if you need HKLM registry writes.
PrivilegesRequired  = lowest
PrivilegesRequiredOverridesAllowed = dialog

; ── Minimum Windows version (Windows 10 x64) ─────────────────────────────────
MinVersion          = 10.0.17763
ArchitecturesAllowed            = x64
ArchitecturesInstallIn64BitMode = x64

; ── Signing (optional — uncomment and configure if you have a code signing cert)
; SignTool = signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f "cert.pfx" /p "password" $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Offer a desktop shortcut (unchecked by default — respects user preference)
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The published single-file executable from the CI publish step
Source: "..\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu shortcut
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}"; Comment: "{#AppDescription}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; Desktop shortcut (only created if user chose the task above)
Name: "{autodesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Register SwiftLaunch for Windows startup (HKCU — no admin required).
; This mirrors what StartupManager.cs does at runtime, but the installer
; sets it immediately so the app starts after the very first reboot.
Root:      HKCU
Subkey:    Software\Microsoft\Windows\CurrentVersion\Run
ValueType: string
ValueName: SwiftLaunch
ValueData: """{app}\{#AppExeName}"""
Flags:     uninsdeletevalue

[Run]
; Launch SwiftLaunch immediately after installation finishes (optional).
Filename:    "{app}\{#AppExeName}"
Description: "Launch SwiftLaunch now"
Flags:       nowait postinstall skipifsilent

[UninstallRun]
; Gracefully close any running instance before uninstalling
Filename:    "taskkill.exe"
Parameters:  "/f /im {#AppExeName}"
Flags:       runhidden skipifdoesntexist

[UninstallDelete]
; Clean up the SQLite index left in %LOCALAPPDATA%\SwiftLaunch
; Comment this out if you want to preserve the user's index on uninstall.
Type: filesandordirs; Name: "{localappdata}\SwiftLaunch"
