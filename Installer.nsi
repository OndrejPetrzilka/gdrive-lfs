; example2.nsi
;
; This script is based on example1.nsi but it remembers the directory, 
; has uninstall support and (optionally) installs start menu shortcuts.
;
; It will install example2.nsi into a directory that the user selects.
;
; See install-shared.nsi for a more robust way of checking for administrator rights.
; See install-per-user.nsi for a file association example.

;--------------------------------

; The name of the installer
Name "GoogleDriveLFS"

; The file to write
OutFile "SetupGoogleDriveLFS.exe"

; Request application privileges for Windows Vista and higher
RequestExecutionLevel admin

; Build Unicode installer
Unicode True

; The default installation directory
InstallDir $PROGRAMFILES\GoogleDriveLFS

; Registry key to check for directory (so if you install again, it will 
; overwrite the old one automatically)
InstallDirRegKey HKLM "Software\GoogleDriveLFS" "Install_Dir"

;--------------------------------

; Pages

Page directory
Page instfiles

UninstPage uninstConfirm
UninstPage instfiles

;--------------------------------

; The stuff to install
Section "GoogleDriveLFS (required)"

  SectionIn RO
  
  ; Set output path to the installation directory.
  SetOutPath $INSTDIR
  
  ; Put file there
  File "Google.Apis.dll"
  File "Google.Apis.Auth.dll"
  File "Google.Apis.Core.dll" 
  File "Google.Apis.Drive.v3.dll"
  File "GoogleDriveLFS.dll"
  File "GoogleDriveLFS.exe"
  File "GoogleDriveLFS.pdb"
  File "GoogleDriveLFS.deps.json"
  File "GoogleDriveLFS.runtimeconfig.json"
  File "Newtonsoft.Json.dll"
  File "System.CodeDom.dll"
  File "System.Management.dll"
  File "SetupGit.bat"

  ExecWait '"$INSTDIR\SetupGit.bat"'
    
  ; Write the installation path into the registry
  WriteRegStr HKLM SOFTWARE\GoogleDriveLFS "Install_Dir" "$INSTDIR"
  
  ; Write the uninstall keys for Windows
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GoogleDriveLFS" "DisplayName" "GoogleDriveLFS"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GoogleDriveLFS" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GoogleDriveLFS" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GoogleDriveLFS" "NoRepair" 1
  WriteUninstaller "$INSTDIR\uninstall.exe"
  
SectionEnd

;--------------------------------

; Uninstaller

Section "Uninstall"
  
  ; Remove registry keys
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\GoogleDriveLFS"
  DeleteRegKey HKLM SOFTWARE\GoogleDriveLFS

  ; Remove files and uninstaller
  Delete "$INSTDIR\Google.Apis.dll"
  Delete "$INSTDIR\Google.Apis.Auth.dll"
  Delete "$INSTDIR\Google.Apis.Core.dll" 
  Delete "$INSTDIR\Google.Apis.Drive.v3.dll"
  Delete "$INSTDIR\GoogleDriveLFS.dll"
  Delete "$INSTDIR\GoogleDriveLFS.exe"
  Delete "$INSTDIR\GoogleDriveLFS.pdb"
  Delete "$INSTDIR\GoogleDriveLFS.deps.json"
  Delete "$INSTDIR\GoogleDriveLFS.runtimeconfig.json"
  Delete "$INSTDIR\Newtonsoft.Json.dll"
  Delete "$INSTDIR\System.CodeDom.dll"
  Delete "$INSTDIR\System.Management.dll"
  Delete "$INSTDIR\SetupGit.bat"

  Delete $INSTDIR\uninstall.exe

  ; Remove directories
  RMDir "$INSTDIR"

SectionEnd
