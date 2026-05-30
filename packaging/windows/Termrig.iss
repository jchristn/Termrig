#define AppName "Termrig"
#define AppPublisher "jchristn"
#define AppExeName "Termrig.exe"
#ifndef AppVersion
#define AppVersion "0.1.0"
#endif
#ifndef PublishDir
#define PublishDir "..\..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
#define OutputDir "..\..\artifacts\packages\windows"
#endif

[Setup]
AppId=com.jchristn.Termrig
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=termrig-{#AppVersion}-windows-x64-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\LICENSE.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; AppUserModelID: "com.jchristn.Termrig"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; AppUserModelID: "com.jchristn.Termrig"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
