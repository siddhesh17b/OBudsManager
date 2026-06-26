# OBuds Manager

A Fluent, modern Windows utility to manage noise control modes (ANC) and monitor battery levels for Oppo, OnePlus, and Realme Bluetooth earbuds.

---

## Installation

Download the pre-compiled setup installer (`OBudsManagerSetup.exe`) from the [GitHub Releases](https://github.com/siddhesh17b/OBudsManager/releases) page and run the installer. 

*Note: The installer requires administrative privileges to install the application into `C:\Program Files\OBuds Manager`.*

---

## Supported Devices

Any earbuds utilizing the **OPOv1** (Oppo Protocol) protocol:
- **OnePlus:** Nord Buds 3 Pro, Nord Buds 3, Nord Buds 2, Buds Pro 2, Buds Pro, etc.
- **Oppo:** Enco X2, Enco Free2, Enco Air3 Pro, Enco Air2 Pro, etc.
- **Realme:** Buds Air 5 Pro, Buds Air 3, etc.

*Note: Ensure your earbuds are paired and connected to Windows via Bluetooth before launching the application.*

---

## Building and Running

### Prerequisites
- **Operating System:** Windows 10 (build 19041+) or Windows 11
- **Developer SDK:** [.NET 10.0 SDK](https://dotnet.microsoft.com/download)

### Run Locally
To compile and run the application from source:
```powershell
dotnet run --project OBudsManager.csproj
```

### Publish Single-File Executable
To publish a portable, framework-dependent single-file Release binary:
```powershell
dotnet publish OBudsManager.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```
The output executable will be placed in:
`bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\OBudsManager.exe`

---

## Compiling the Setup Installer

### Prerequisites
- [Inno Setup 6](https://jrsoftware.org/ishelp/) installed on your machine.

### Compile Command
Run the Inno Setup compiler from PowerShell to compile the `OBudsManagerSetup.exe` installer package:
```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```
The compiled installer will be saved in the `Output/` folder:
`Output\OBudsManagerSetup.exe`

---

## Troubleshooting

- **No device detected?**
  Verify the earbuds are active and connected under Windows Bluetooth Settings.
- **Cannot delete installation files?**
  If the application is running, the uninstaller will automatically attempt to terminate it. If it fails, close the app from the system tray context menu and retry uninstallation.
