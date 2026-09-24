Unicode true

!include "MUI2.nsh"

!ifndef TAG
  !error "pass -DTAG=<release tag>"
!endif
!ifndef SOURCE
  !error "pass -DSOURCE=<folder holding the Windows build>"
!endif
!ifndef OUTFILE
  !error "pass -DOUTFILE=<installer to write>"
!endif

!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Blastlands"
!define GAME_EXE "$INSTDIR\Game\Blastlands.exe"

Name "Blastlands"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\Blastlands"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "Blastlands ${TAG}"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "${GAME_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Start Blastlands"

!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Section "Install"
  RMDir /r "$INSTDIR\Game"

  SetOutPath "$INSTDIR\Game"
  File /r /x "*_BurstDebugInformation_DoNotShip" "${SOURCE}\*.*"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateShortcut "$SMPROGRAMS\Blastlands.lnk" "${GAME_EXE}"
  CreateShortcut "$DESKTOP\Blastlands.lnk" "${GAME_EXE}"

  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "Blastlands"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "FerrLabs"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "${GAME_EXE}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\Blastlands.lnk"
  Delete "$DESKTOP\Blastlands.lnk"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  RMDir /r "$INSTDIR"
SectionEnd
