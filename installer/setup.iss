; ══════════════════════════════════════════════════════════════════════════════
; SwiftLaunch — Inno Setup Script
;
; Build:
; iscc /DAPP_VERSION=1.0.0 installer\setup.iss
;
; Output:
; installer\Output\SwiftLaunch-v1.0.0.exe
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

; Application identity
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#APP_VERSION}
AppVerName={#AppName} v{#APP_VERSION}

AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases


; Output settings
OutputDir=Output
OutputBaseFilename=SwiftLaunch-v{#APP_VERSION}


; Installation location
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes


; Installer icon
SetupIconFile=..\assets\app.ico

WizardStyle=modern
WizardSizePercent=110


; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes


; Permissions
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog


; Windows requirements
MinVersion=10.0.17763
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64



[Languages]

Name: "english"; MessagesFile: "compiler:Default.isl"



[Tasks]

Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked



[Files]
; Published application
Source: "..\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\app.ico"; DestDir: "{app}"; Flags: ignoreversion


[Icons]

Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app.ico"; Comment: "{#AppDescription}"

Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon



[Registry]

Root: HKCU; Subkey: "Software\SwiftLaunch"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

Root: HKCU; Subkey: "Software\SwiftLaunch"; ValueType: string; ValueName: "Version"; ValueData: "{#APP_VERSION}"



[Run]

Filename: "{app}\{#AppExeName}"; Description: "Launch SwiftLaunch now"; Flags: nowait postinstall skipifsilent



[UninstallRun]

Filename: "taskkill.exe"; Parameters: "/f /im {#AppExeName}"; Flags: runhidden skipifdoesntexist



[UninstallDelete]

Type: filesandordirs; Name: "{localappdata}\SwiftLaunch"