#define MyAppName "SmartCopy"
#define MyAppVersion "1.0.4"
#define MyAppPublisher "SmartCopy"
#define MyAppExeName "SmartCopy.exe"

[Setup]
AppId={{E7D3A9C2-4F1E-4B8A-9C5D-2A6E8B0F1C3E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\SmartCopy
DefaultGroupName=SmartCopy
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
OutputDir=out
OutputBaseFilename=SmartCopySetup_{#MyAppVersion}
SetupIconFile=..\media\smartcopy.ico
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\SmartCopy.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SmartCopy"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\SmartCopy"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
