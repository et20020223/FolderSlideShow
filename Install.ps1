$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:LOCALAPPDATA 'Programs\FolderSlideShow'
$exe = Join-Path $destination 'FolderSlideShow.exe'
$menuPaths = @('Software\Classes\Directory\shell\FolderSlideShow.Local', 'Software\Classes\Directory\Background\shell\FolderSlideShow.Local', 'Software\Classes\Drive\shell\FolderSlideShow.Local')
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'FolderSlideShow.exe'))) { throw 'Build the application first.' }
if (-not (Test-Path -LiteralPath 'C:\Program Files\VideoLAN\VLC\libvlc.dll')) { Write-Warning '64-bit VLC is required for video playback. Photos can play without VLC.' }
# Only register this application, for the current Windows account.
foreach ($menuPath in $menuPaths) {
    $existing = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("$menuPath\command")
    if ($null -ne $existing) {
        try { if ([string]$existing.GetValue('') -notlike ('"' + $exe + '"*')) { throw "A different application owns $menuPath. Installation stopped." } }
        finally { $existing.Dispose() }
    }
}
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($file in @('FolderSlideShow.exe', 'libsharpyuv.dll', 'libwebp.dll', 'libwebpdemux.dll', 'README.md', 'Uninstall.ps1', 'Uninstall.cmd')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $destination $file) -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $destination -Recurse -Force
foreach ($menuPath in $menuPaths) {
    $placeholder = if ($menuPath.Contains('\Background\')) { '%V' } else { '%1' }
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($menuPath)
    try {
        $key.SetValue('', '隨機放映（含子資料夾）')
        $key.SetValue('MUIVerb', '隨機放映（含子資料夾）')
        $key.SetValue('Position', 'Top')
        $key.SetValue('Icon', '"' + $exe + '",0')
        $key.SetValue('MultiSelectModel', 'Single')
        $command = $key.CreateSubKey('command')
        try {
            # The trailing dot avoids the Windows quoted-argument edge case for drive roots ending in a backslash.
            $command.SetValue('', '"' + $exe + '" "' + $placeholder + '\."')
        } finally { $command.Dispose() }
    } finally { $key.Dispose() }
}
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) '資料夾隨機放映.lnk'))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $destination
$shortcut.Description = '隨機播放照片和影片，包含所有子資料夾'
$shortcut.IconLocation = $exe + ',0'
$shortcut.Save()
$sendToShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('SendTo')) '隨機放映（含子資料夾）.lnk'))
$sendToShortcut.TargetPath = $exe
$sendToShortcut.WorkingDirectory = $destination
$sendToShortcut.Description = '隨機播放資料夾及所有子資料夾中的照片和影片'
$sendToShortcut.IconLocation = $exe + ',0'
$sendToShortcut.Save()
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FolderSlideShowShellRefresh {
    [DllImport("shell32.dll")] public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
'@
[FolderSlideShowShellRefresh]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Output "Installed: $exe"
Write-Output 'Right-click a folder or its background > Show more options > 隨機放映（含子資料夾）'
Write-Output 'Classic-menu alternative: right-click a folder > Send to > 隨機放映（含子資料夾）'
