; Instalador do Véspera para Windows (Inno Setup 6).
; Instala em %LocalAppData%\Programs\Vespera, sem pedir administrador.
; Uso local:   iscc /DAppVersion=0.1.0 /DSourceDir=..\build\windows installer\vespera.iss
; No CI:       gerado pelo job "installer" de .github/workflows/release.yml em tags v*.

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#ifndef SourceDir
  #define SourceDir "..\build\windows"
#endif
#ifndef OutputDir
  #define OutputDir "..\build\installer"
#endif

[Setup]
AppId={{6E2B6C1A-6A0E-4F8E-9E3B-5B5C1F6D2A71}
AppName=Véspera
AppVersion={#AppVersion}
AppPublisher=Véspera (open source)
AppPublisherURL=https://github.com/NasckDev/vespera
AppSupportURL=https://github.com/NasckDev/vespera/issues
AppUpdatesURL=https://github.com/NasckDev/vespera/releases
DefaultDirName={localappdata}\Programs\Vespera
DefaultGroupName=Véspera
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=Vespera-{#AppVersion}-setup
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\Vespera.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; O Godot .NET exporta o .exe e a pasta data_Vespera_windows_x86_64 (DECISIONS D16): os dois são necessários.
Source: "{#SourceDir}\Vespera.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\data_Vespera_windows_x86_64\*"; DestDir: "{app}\data_Vespera_windows_x86_64"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE-ASSETS.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Véspera"; Filename: "{app}\Vespera.exe"
Name: "{group}\{cm:UninstallProgram,Véspera}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\Véspera"; Filename: "{app}\Vespera.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Vespera.exe"; Description: "{cm:LaunchProgram,Véspera}"; Flags: nowait postinstall skipifsilent
