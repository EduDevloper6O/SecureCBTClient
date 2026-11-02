#ifndef AppVersion
#define AppVersion "1.0.0.0"
#endif

#define AppName "Secure CBT Client"
#define AppPublisher "Secure CBT Client"
#define AppExeName "Elecctron CBT.exe"
#define AppId "{7A0E9D1A-7B7B-4A2E-BB75-8BAA3A7C62C5}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Secure CBT Client
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=SecureCBT_Installer
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayName={#AppName}

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
