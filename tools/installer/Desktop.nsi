Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!ifndef REVIEW_SUFFIX
  !define REVIEW_SUFFIX ""
!endif
!ifndef INSTALL_ROOT
  !define INSTALL_ROOT "$LOCALAPPDATA\Programs\SarahBeautyDesktopPreview${REVIEW_SUFFIX}"
!endif
!define PRODUCT_NAME "Sarah Beauty Desktop Preview${REVIEW_SUFFIX}"
!define PRODUCT_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\SarahBeautyDesktopPreview${REVIEW_SUFFIX}"
Name "${PRODUCT_NAME}"
OutFile "${INSTALLER_OUTPUT}"
VIProductVersion "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileDescription" "Sarah Beauty Desktop preview installer"
VIAddVersionKey /LANG=1033 "LegalCopyright" "See included dependency notices."
InstallDir "${INSTALL_ROOT}"
RequestExecutionLevel user
SetCompressor /SOLID zlib
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
!include "PathGuards.nsh"
!insertmacro DeclareGuards ""
!insertmacro DeclareGuards "un."

Section "Install"
  SetShellVarContext current
  SetRegView 64
  Call RequireExpectedRoot
  !define PATH_GUARD_PREFIX ""
  !include "${PACKAGE_INCLUDES}\CheckPaths.nsh"
  !undef PATH_GUARD_PREFIX
  Push "$INSTDIR\ThirdParty\Installer"
  Call RequirePlainPath
  Call RequireClosedApp
  ClearErrors
  !include "${PACKAGE_INCLUDES}\InstallFiles.nsh"
  IfErrors install_failed
  SetOutPath "$INSTDIR\ThirdParty\Installer"
  File /oname=NSIS-COPYING.txt "${NSISDIR}\COPYING"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "${PRODUCT_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKCU "${PRODUCT_KEY}" "DisplayVersion" "${APP_VERSION}-preview"
  WriteRegStr HKCU "${PRODUCT_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${PRODUCT_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${PRODUCT_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${PRODUCT_KEY}" "NoRepair" 1
  CreateShortcut "$SMPROGRAMS\${PRODUCT_NAME}.lnk" "$INSTDIR\SarahBeauty.Desktop.exe"
  Goto install_done
  install_failed:
    MessageBox MB_OK|MB_ICONSTOP "The app files could not be copied. Your business records were not changed. Retry installation after correcting the storage or access problem." /SD IDOK
    SetErrorLevel 3
    Abort
  install_done:
SectionEnd

Section "Uninstall"
  SetShellVarContext current
  SetRegView 64
  Call un.RequireExpectedRoot
  !define PATH_GUARD_PREFIX "un."
  !include "${PACKAGE_INCLUDES}\CheckPaths.nsh"
  !undef PATH_GUARD_PREFIX
  Push "$INSTDIR\ThirdParty\Installer"
  Call un.RequirePlainPath
  Call un.RequireClosedApp
  !include "${PACKAGE_INCLUDES}\RemoveFiles.nsh"
  Delete "$INSTDIR\ThirdParty\Installer\NSIS-COPYING.txt"
  RMDir "$INSTDIR\ThirdParty\Installer"
  RMDir "$INSTDIR\ThirdParty"
  Delete "$SMPROGRAMS\${PRODUCT_NAME}.lnk"
  DeleteRegKey HKCU "${PRODUCT_KEY}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
