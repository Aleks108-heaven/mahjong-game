; Inno Setup script for the Mahjong Table installer. Build it with tools\package.ps1, which passes the version
; and the path of the published exe:  ISCC /DAppVersion=0.1.0 /DSourceExe=<path> /DOutputDir=<dir> MahjongTable.iss

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\artifacts\staging\MahjongTable.exe"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "Mahjong Table"
#define AppExe "MahjongTable.exe"
#define AppUrl "https://github.com/Aleks108-heaven/mahjong-game"

[Setup]
; Never change AppId: it is how Windows recognises an upgrade or an uninstall of this app.
AppId={{C2FFD216-EF00-48B0-839C-0A234777E6A9}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Aleks108-heaven
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} setup

; Installs for the current user and needs no administrator rights; the wizard offers "all users" as a choice.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

SetupIconFile=..\Wpf\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
; Lets a running copy be closed so an upgrade can replace the exe.
CloseApplications=yes
RestartApplications=no

OutputDir={#OutputDir}
OutputBaseFilename=MahjongTable-{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "{#AppExe}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The app's saved language, level and seat (a few bytes of JSON for the user who uninstalls).
Type: filesandordirs; Name: "{localappdata}\MahjongTable"
