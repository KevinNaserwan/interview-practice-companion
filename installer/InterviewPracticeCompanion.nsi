Unicode true
!include "MUI2.nsh"

Name "Interview Practice Companion"
OutFile "..\artifacts\InterviewPracticeCompanion-Setup-x64.exe"
InstallDir "$LOCALAPPDATA\Programs\InterviewPracticeCompanion"
InstallDirRegKey HKCU "Software\InterviewPracticeCompanion" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "Interview Practice Companion"

!define MUI_ABORTWARNING
!define MUI_ICON "..\src\InterviewPracticeCompanion\Resources\AppIcon.ico"
!define MUI_UNICON "..\src\InterviewPracticeCompanion\Resources\AppIcon.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${NSISDIR}\Contrib\Graphics\Wizard\win.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${NSISDIR}\Contrib\Graphics\Wizard\win.bmp"
!define MUI_FINISHPAGE_RUN "$INSTDIR\InterviewPracticeCompanion.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Open Interview Practice Companion"
!define MUI_FINISHPAGE_LINK "Practice mode only — learn about responsible use"
!define MUI_FINISHPAGE_LINK_LOCATION "https://ai.meetsin.id"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "English"

Section "Install" SecMain
  SetOutPath "$INSTDIR"
  File /r "..\artifacts\publish\*"
  CreateDirectory "$SMPROGRAMS\Interview Practice Companion"
  CreateShortcut "$SMPROGRAMS\Interview Practice Companion\Interview Practice Companion.lnk" "$INSTDIR\InterviewPracticeCompanion.exe"
  CreateShortcut "$DESKTOP\Interview Practice Companion.lnk" "$INSTDIR\InterviewPracticeCompanion.exe"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\InterviewPracticeCompanion" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\InterviewPracticeCompanion" "DisplayName" "Interview Practice Companion"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\InterviewPracticeCompanion" "DisplayVersion" "1.0.1"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\InterviewPracticeCompanion" "Publisher" "Interview Practice Companion"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\InterviewPracticeCompanion" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\InterviewPracticeCompanion" "DisplayIcon" "$INSTDIR\InterviewPracticeCompanion.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\Interview Practice Companion.lnk"
  Delete "$SMPROGRAMS\Interview Practice Companion\Interview Practice Companion.lnk"
  RMDir "$SMPROGRAMS\Interview Practice Companion"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "Software\InterviewPracticeCompanion"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\InterviewPracticeCompanion"
SectionEnd
