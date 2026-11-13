Unicode true
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!define INSTALL_ROOT "${PROBE_ROOT}"
Name "Sarah Beauty path guard probe"
OutFile "${PROBE_OUTPUT}"
InstallDir "${INSTALL_ROOT}"
RequestExecutionLevel user
SilentInstall silent
!include "PathGuards.nsh"
!insertmacro DeclareGuards ""
Section
  Call RequireExpectedRoot
  Push "$INSTDIR"
  Call RequirePlainPath
  Call RequireClosedApp
  StrCmp "$INSTDIR" "${INSTALL_ROOT}" preserved
    SetErrorLevel 1
    Quit
  preserved:
    SetErrorLevel 0
SectionEnd
