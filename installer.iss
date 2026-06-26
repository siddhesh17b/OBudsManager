; Inno Setup Script for OBuds Manager
; See https://jrsoftware.org/ishelp/ for details on scripting

[Setup]
AppId={{5D875F9B-D102-11E1-9B23-00025B00A5A5}
AppName=OBuds Manager
AppVersion=1.0.0
AppPublisher=Siddhesh Bisen
AppPublisherURL=https://github.com/siddhesh17b/OBudsManager
DefaultDirName={commonpf}\OBuds Manager
DefaultGroupName=OBuds Manager
DisableProgramGroupPage=yes
DisableDirPage=yes
OutputBaseFilename=OBudsManagerSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; Install in 64-bit mode on 64-bit systems to avoid redirection to Program Files (x86)
ArchitecturesInstallIn64BitMode=x64compatible

; Lock down installation to admin level for Program Files access
PrivilegesRequired=admin

SetupIconFile=app_icon.ico
UninstallDisplayIcon={app}\app_icon.ico
AppMutex=OBudsManagerMutex

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Copy all files in the publish folder recursively to ensure app_icon.ico and WPF-UI assets are copied
Source: "bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Put the shortcut directly in the Programs list (root) so it is found instantly in Windows Search
Name: "{autoprograms}\OBuds Manager"; Filename: "{app}\OBudsManager.exe"; IconFilename: "{app}\app_icon.ico"
; Mandatory Desktop shortcut (no Tasks parameter)
Name: "{autodesktop}\OBuds Manager"; Filename: "{app}\OBudsManager.exe"; IconFilename: "{app}\app_icon.ico"

[Run]
Filename: "{app}\OBudsManager.exe"; Description: "{cm:LaunchProgram,OBuds Manager}"; Flags: nowait postinstall skipifsilent

[Registry]
; Clean up the startup registry key on uninstall
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueName: "OBudsManager"; Flags: deletevalue uninsdeletevalue

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  // Terminate any running instance of the application before installing to avoid file locks
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM OBudsManager.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  // Terminate any running instance of the application before uninstalling to avoid file locks
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM OBudsManager.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;
