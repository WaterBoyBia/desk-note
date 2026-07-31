#define MyAppName "desk-note"
#define MyAppVersion "1.0.0"
#define MyAppExeName "desk-note.exe"

[Setup]
AppId={{B2DC4D59-4F93-47B8-AF4E-BA697458B7CB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\desk-note
DefaultGroupName=desk-note
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=desk-note-{#MyAppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\DeskNote.App\Resources\desk-note.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes

[Files]
Source: "..\src\DeskNote.App\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\desk-note"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\desk-note"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 desk-note"; Flags: nowait postinstall skipifsilent
