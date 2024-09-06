#define Date GetDateTimeString('yyyymmdd','','');
#define DateShort GetDateTimeString('yy.mm.dd','','');

[Setup]

AppId={{195D9982-0962-4B7A-B435-AE01735013B2}
AppName=InfostackerService
AppVersion=1.3.5
AppVerName=InfostackerService {#Date}
AppPublisher=Taskscape Ltd
CreateAppDir=yes
DisableWelcomePage=yes
DisableDirPage=no
DefaultDirName=C:\InfostackerService\
DefaultGroupName=InfostackerService
AllowNoIcons=yes
InfoBeforeFile=README.md
PrivilegesRequired=admin
OutputDir=_release
OutputBaseFilename=InfostackerService_{#Date}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
VersionInfoCompany=Taskscape Ltd
VersionInfoCopyright=Taskscape Ltd
VersionInfoDescription=InfostackerService
VersionInfoProductName=InfostackerService
VersionInfoProductVersion={#DateShort}
VersionInfoTextVersion={#Date}
VersionInfoVersion={#DateShort}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "readme.md"; DestDir: "{app}"; Flags: ignoreversion;
Source: "publish\*"; DestDir: "{app}"; Excludes: "*.pdb, web.config, appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\web.config"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist;
Source: "publish\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist;


