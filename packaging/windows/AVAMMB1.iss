; Inno Setup script for the Windows installer (built in CI by packaging/package.ps1).
; Defines passed on the command line: AppVersion, Rid (win-x64 / win-arm64), SourceDir, OutputDir.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Rid
  #define Rid "win-x64"
#endif

[Setup]
AppId={{97E74127-6FBC-4FB6-A793-B835C80FF8C5}
AppName=AVAMMB1
AppVerName=AVAMMB1 {#AppVersion}
AppVersion={#AppVersion}
AppPublisher=AVAMMB1 contributors
AppPublisherURL=https://github.com/Harlock123/AVAMMB1
DefaultDirName={autopf}\AVAMMB1
DefaultGroupName=AVAMMB1
UninstallDisplayIcon={app}\AVAMMB1.exe
UninstallDisplayName=AVAMMB1 (AVAM and M) {#AppVersion}
OutputDir={#OutputDir}
OutputBaseFilename=AVAMMB1-{#AppVersion}-{#Rid}-setup
SetupIconFile=..\..\Assets\Icons\avammb1.ico
LicenseFile=..\..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Installs for the current user by default (no administrator rights needed); "all users" is offered.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if Rid == "win-arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\AVAMMB1"; Filename: "{app}\AVAMMB1.exe"; Comment: "AVAM and M - Book One, a first-person fantasy role-playing adventure"
Name: "{group}\Uninstall AVAMMB1"; Filename: "{uninstallexe}"
Name: "{autodesktop}\AVAMMB1"; Filename: "{app}\AVAMMB1.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AVAMMB1.exe"; Description: "Play AVAMMB1 now"; Flags: nowait postinstall skipifsilent
