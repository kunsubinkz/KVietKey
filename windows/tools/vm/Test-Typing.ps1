<#
.SYNOPSIS
  Kiểm thử gõ thật trên Windows: mở Notepad, gõ chuỗi phím qua hook của KVietKey rồi so với kết quả mong đợi.

.DESCRIPTION
  Chạy trong máy ảo / máy test Windows 10-11 (x64 hoặc ARM64), KHÔNG chạy trên máy đang làm việc vì script
  tự gõ phím vào cửa sổ đang focus. Script chạy KVietKey với thư mục dữ liệu tạm (cài đặt sạch, không đụng cài đặt thật).

.EXAMPLE
  powershell -STA -File Test-Typing.ps1
  powershell -STA -File Test-Typing.ps1 -Exe C:\KVietKey\KVietKey.exe -Method vni -DelayMs 30
#>
param(
    [string]$Exe = "$env:LOCALAPPDATA\Programs\KVietKey\KVietKey.exe",
    [ValidateSet('telex', 'vni')][string]$Method = 'telex',
    [int]$DelayMs = 15
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class KvkWin {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

if (-not (Test-Path $Exe)) { throw "Không thấy $Exe — truyền đường dẫn bằng -Exe." }

# Mỗi cặp: chuỗi phím gõ vào, văn bản mong đợi trên màn hình.
$cases = if ($Method -eq 'telex') {
    @(
        @('tieengs vieetj', 'tiếng việt'),
        @('Vieetj Nam', 'Việt Nam'),
        @('nguwowif dduwowcj', 'người được'),
        @('hoaf thuys khoer', 'hoà thuý khoẻ'),
        @('xin chaof cacs banj', 'xin chào các bạn'),
        @('muaw cuarw nuwowcs', 'mưa cửa nước'),
        @('truwowngf dduwowngf', 'trường đường'),
        @('windows class show text', 'windows class show text')
    )
} else {
    @(
        @('tie6ng1 vie6t5', 'tiếng việt'),
        @('Vie6t5 Nam', 'Việt Nam'),
        @('ngu7o7i2 d9u7o7c5', 'người được'),
        @('hoa2 thuy1 khoe3', 'hoà thuý khoẻ'),
        @('xin chao2 ca1c ba5n', 'xin chào các bạn')
    )
}

$data = Join-Path $env:TEMP ("kvk-test-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $data | Out-Null
@{ method = $Method; onboardingDone = $true; language = 'vi' } | ConvertTo-Json |
    Set-Content -Path (Join-Path $data 'settings.json') -Encoding UTF8

$app = $null
$notepad = $null
$failures = 0
$oldClipboard = $null
try {
    $oldClipboard = [System.Windows.Forms.Clipboard]::GetText()
    $app = Start-Process -FilePath $Exe -ArgumentList @('--allow-multiple', '--autostart', '--data-dir', "`"$data`"") -PassThru
    Start-Sleep -Seconds 2
    if ($app.HasExited) { throw 'KVietKey thoát ngay sau khi chạy (hook không cài được?). Thử chạy: KVietKey.exe --selftest' }

    $notepad = Start-Process -FilePath notepad.exe -PassThru
    $window = $null
    for ($i = 0; $i -lt 30 -and -not $window; $i++) {
        Start-Sleep -Milliseconds 300
        $window = Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    }
    if (-not $window) { throw 'Không mở được Notepad.' }
    [void][KvkWin]::SetForegroundWindow($window.MainWindowHandle)
    Start-Sleep -Milliseconds 800

    foreach ($case in $cases) {
        foreach ($ch in $case[0].ToCharArray()) {
            [System.Windows.Forms.SendKeys]::SendWait([string]$ch)
            Start-Sleep -Milliseconds $DelayMs
        }
        [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
        Start-Sleep -Milliseconds ($DelayMs * 4)
    }

    [System.Windows.Forms.SendKeys]::SendWait('^a')
    Start-Sleep -Milliseconds 200
    [System.Windows.Forms.SendKeys]::SendWait('^c')
    Start-Sleep -Milliseconds 400
    $lines = ([System.Windows.Forms.Clipboard]::GetText() -split "`r?`n") | Where-Object { $_ -ne '' }

    for ($i = 0; $i -lt $cases.Count; $i++) {
        $expected = $cases[$i][1]
        $got = if ($i -lt $lines.Count) { $lines[$i] } else { '<thiếu dòng>' }
        if ($got -eq $expected) {
            Write-Host "PASS  $($cases[$i][0])  →  $got"
        } else {
            $failures++
            Write-Host "FAIL  $($cases[$i][0])  →  '$got'  (mong đợi '$expected')" -ForegroundColor Red
        }
    }
}
finally {
    if ($notepad -and -not $notepad.HasExited) { Stop-Process -Id $notepad.Id -Force -ErrorAction SilentlyContinue }
    Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*Untitled*' -or $_.MainWindowTitle -like '*Không có tiêu đề*' } |
        Stop-Process -Force -ErrorAction SilentlyContinue
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue }
    if ($null -ne $oldClipboard -and $oldClipboard -ne '') { [System.Windows.Forms.Clipboard]::SetText($oldClipboard) }
    Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
}

if ($failures -eq 0) { Write-Host "`nTYPING TEST OK ($Method)" } else { Write-Host "`nTYPING TEST FAILED: $failures ca lệch" -ForegroundColor Red }
exit ([int]($failures -ne 0))
