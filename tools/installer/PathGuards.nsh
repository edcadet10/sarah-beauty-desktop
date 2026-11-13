!macro DeclareGuards PREFIX
Function ${PREFIX}RequireExpectedRoot
  System::Call 'kernel32::GetFullPathNameW(w "$INSTDIR", i ${NSIS_MAX_STRLEN}, w .r0, p 0) i.r1'
  System::Call 'kernel32::GetFullPathNameW(w "${INSTALL_ROOT}", i ${NSIS_MAX_STRLEN}, w .r2, p 0) i.r3'
  ${If} $1 == 0
  ${OrIf} $1 >= ${NSIS_MAX_STRLEN}
  ${OrIf} $3 == 0
  ${OrIf} $3 >= ${NSIS_MAX_STRLEN}
  ${OrIf} $0 == ""
  ${OrIf} $2 == ""
  ${OrIf} $0 != $2
    MessageBox MB_OK|MB_ICONSTOP "Use this installer's designated application folder." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  StrCpy $INSTDIR $0
FunctionEnd

Function ${PREFIX}RequirePlainPath
  Pop $0
  check_parent:
    System::Call 'kernel32::GetFileAttributesW(w r0) i.r1'
    ${If} $1 != -1
      IntOp $2 $1 & 0x400
      ${If} $2 != 0
        MessageBox MB_OK|MB_ICONSTOP "The application folder contains a link or junction. Choose an ordinary folder before continuing." /SD IDOK
        SetErrorLevel 2
        Abort
      ${EndIf}
    ${EndIf}
    ${GetParent} "$0" $3
    StrCmp $3 "" plain_done
    StrCmp $3 $0 plain_done
    StrCpy $0 $3
    Goto check_parent
  plain_done:
FunctionEnd

Function ${PREFIX}RequireClosedApp
  IfFileExists "$INSTDIR\SarahBeauty.Desktop.exe" 0 app_closed
  System::Call 'kernel32::CreateFileW(w "$INSTDIR\SarahBeauty.Desktop.exe", i 0x40000000, i 1, p 0, i 3, i 0, p 0) p.r0'
  ${If} $0 == -1
    MessageBox MB_OK|MB_ICONSTOP "Close Sarah Beauty Desktop before installing or removing it." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  System::Call 'kernel32::CloseHandle(p r0)'
  app_closed:
FunctionEnd
!macroend
