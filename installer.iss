; Inno Setup Script for O Buds Manager
; See https://jrsoftware.org/ishelp/ for details on scripting

[Setup]
AppId={{5D875F9B-D102-11E1-9B23-00025B00A5A5}
AppName=O Buds Manager
AppVersion=1.0.0
AppPublisher=Siddhesh Bisen
AppPublisherURL=https://github.com/siddhesh17b/OBudsManager
DefaultDirName={autopf}\O Buds Manager
DefaultGroupName=O Buds Manager
DisableProgramGroupPage=yes
OutputBaseFilename=OBudsManagerSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; Support both administrative (All Users) and non-administrative (Just Me) installs.
; "lowest" runs without admin prompts by default, registering the app in the current user's Installed Apps database.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

SetupIconFile=app_icon.ico
UninstallDisplayIcon={app}\app_icon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copy all files in the publish folder recursively to ensure app_icon.ico and WPF-UI assets are copied
Source: "bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Put the shortcut directly in the Programs list (root) so it is found instantly in Windows Search
Name: "{autoprograms}\O Buds Manager"; Filename: "{app}\OBudsManager.exe"; IconFilename: "{app}\app_icon.ico"
Name: "{autodesktop}\O Buds Manager"; Filename: "{app}\OBudsManager.exe"; Tasks: desktopicon; IconFilename: "{app}\app_icon.ico"

[Run]
Filename: "{app}\OBudsManager.exe"; Description: "{cm:LaunchProgram,O Buds Manager}"; Flags: nowait postinstall skipifsilent

[Registry]
; Clean up the startup registry key on uninstall
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueName: "OBudsManager"; Flags: deletevalue uninsdeletevalue
