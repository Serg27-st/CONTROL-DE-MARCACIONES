#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{A5AE1C95-6D42-4CB8-BEC4-D9C13F6636ED}
AppName=Control de Marcación
AppVersion={#MyAppVersion}
AppPublisher=GCA
DefaultDirName={autopf}\ControlMarcacion
DefaultGroupName=Control de Marcación
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\artifacts\local-installer\installer
OutputBaseFilename=ControlMarcacion-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=Control de Marcación
SetupLogging=yes

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: checkedonce

[Dirs]
Name: "{commonappdata}\ControlMarcacion\data"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{commonappdata}\ControlMarcacion\logs"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{commonappdata}\ControlMarcacion\backups"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{commonappdata}\ControlMarcacion\config"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{commonappdata}\ControlMarcacion\runtime"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
Source: "..\..\artifacts\local-installer\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "appsettings.Local.json"; DestDir: "{commonappdata}\ControlMarcacion\config"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "..\..\docs\LOCAL_INSTALLATION_GUIDE.md"; DestDir: "{app}"; DestName: "GUIA-INSTALACION-LOCAL.md"; Flags: ignoreversion

[Icons]
Name: "{group}\Control de Marcación"; Filename: "{app}\ControlMarcacion.exe"; WorkingDir: "{app}"
Name: "{commondesktop}\Control de Marcación"; Filename: "{app}\ControlMarcacion.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ControlMarcacion.exe"; Description: "Abrir Control de Marcación"; WorkingDir: "{app}"; Flags: postinstall nowait skipifsilent
