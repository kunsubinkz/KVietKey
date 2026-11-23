; Bộ cài KVietKey cho Windows (NSIS 3, build được trên macOS/Linux bằng `makensis`).
; Cài theo từng người dùng (không cần quyền admin) vào %LOCALAPPDATA%\Programs\KVietKey.
;
; Tham số bắt buộc (-D) do build_release.sh truyền vào:
;   VERSION   phiên bản 3 số, vd. 1.0.0
;   ARCH      x64 hoặc arm64
;   SRC       thư mục chứa các file đã publish (KVietKey.exe + DLL gốc + licenses\)
;   OUTFILE   đường dẫn file .exe bộ cài đầu ra
;   ICON      đường dẫn kvietkey.ico
;   LICENSE   đường dẫn file giấy phép (MIT)

Unicode true
SetCompressor /SOLID lzma

!ifndef VERSION
  !error "Thiếu -DVERSION=x.y.z"
!endif
!ifndef ARCH
  !error "Thiếu -DARCH=x64|arm64"
!endif
!ifndef SRC
  !error "Thiếu -DSRC=<thư mục publish>"
!endif
!ifndef OUTFILE
  !error "Thiếu -DOUTFILE=<file bộ cài>"
!endif
!ifndef ICON
  !error "Thiếu -DICON=<kvietkey.ico>"
!endif
!ifndef LICENSE
  !error "Thiếu -DLICENSE=<file giấy phép>"
!endif

!include "MUI2.nsh"
!include "FileFunc.nsh"

!define APPNAME "KVietKey"
!define PUBLISHER "kunsubin"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define APP_KEY "Software\${APPNAME}"

Name "${APPNAME}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\${APPNAME}"
InstallDirRegKey HKCU "${APP_KEY}" "InstallDir"
RequestExecutionLevel user
BrandingText "${APPNAME} ${VERSION} · ${ARCH}"
ShowInstDetails nevershow
ShowUninstDetails nevershow

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "LegalCopyright" "© 2026 ${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APPNAME} Setup (${ARCH})"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"

!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_ABORTWARNING

; Trang kết thúc: chạy ngay + (tùy chọn) khởi động cùng Windows.
!define MUI_FINISHPAGE_RUN "$INSTDIR\KVietKey.exe"
!define MUI_FINISHPAGE_RUN_TEXT "$(RUN_TEXT)"
!define MUI_FINISHPAGE_SHOWREADME ""
!define MUI_FINISHPAGE_SHOWREADME_TEXT "$(AUTOSTART_TEXT)"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION EnableAutostart
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${LICENSE}"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Ngôn ngữ đầu tiên là mặc định; máy dùng tiếng Việt sẽ tự chọn Vietnamese.
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Vietnamese"

LangString RUN_TEXT ${LANG_ENGLISH} "Run KVietKey now"
LangString RUN_TEXT ${LANG_VIETNAMESE} "Chạy KVietKey ngay"
LangString AUTOSTART_TEXT ${LANG_ENGLISH} "Start KVietKey when Windows starts"
LangString AUTOSTART_TEXT ${LANG_VIETNAMESE} "Khởi động KVietKey cùng Windows"
LangString DELETE_DATA ${LANG_ENGLISH} "Also delete your KVietKey settings and macros?"
LangString DELETE_DATA ${LANG_VIETNAMESE} "Xóa luôn cài đặt và gõ tắt của KVietKey?"
LangString SHORTCUT_DESC ${LANG_ENGLISH} "Vietnamese input method"
LangString SHORTCUT_DESC ${LANG_VIETNAMESE} "Bộ gõ tiếng Việt"

Function EnableAutostart
  WriteRegStr HKCU "${RUN_KEY}" "${APPNAME}" '"$INSTDIR\KVietKey.exe" --autostart'
FunctionEnd

Section "KVietKey" SecMain
  SectionIn RO

  ; Bản cũ đang chạy sẽ giữ khóa file: đóng nó trước khi chép đè.
  nsExec::Exec 'taskkill /F /IM KVietKey.exe'
  Sleep 300

  SetOutPath "$INSTDIR"
  File /r "${SRC}\*.*"
  File "/oname=LICENSE.txt" "${LICENSE}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\KVietKey.exe" "" "$INSTDIR\KVietKey.exe" 0 SW_SHOWNORMAL "" "$(SHORTCUT_DESC)"

  WriteRegStr HKCU "${APP_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\KVietKey.exe"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${UNINST_KEY}" "EstimatedSize" "$0"
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /F /IM KVietKey.exe'
  Sleep 300

  Delete "$SMPROGRAMS\${APPNAME}.lnk"
  DeleteRegValue HKCU "${RUN_KEY}" "${APPNAME}"
  DeleteRegKey HKCU "${UNINST_KEY}"
  DeleteRegKey HKCU "${APP_KEY}"

  ; Chỉ xóa cả thư mục khi đúng là thư mục KVietKey đã cài.
  IfFileExists "$INSTDIR\KVietKey.exe" 0 +2
    RMDir /r "$INSTDIR"

  MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON2 "$(DELETE_DATA)" /SD IDNO IDNO skipData
    RMDir /r "$APPDATA\${APPNAME}"
  skipData:
SectionEnd
