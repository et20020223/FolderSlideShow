$ErrorActionPreference = 'Stop'
$installedExe = Join-Path $env:LOCALAPPDATA 'Programs\FolderSlideShow\FolderSlideShow.exe'
$classPath = 'Software\Classes\CLSID\{D923A8E6-8B39-4A2C-99F2-DB7BA7446B75}'
$menuPaths = @('Software\Classes\Directory\shell\FolderSlideShow.Local', 'Software\Classes\Directory\Background\shell\FolderSlideShow.Local', 'Software\Classes\Drive\shell\FolderSlideShow.Local')
foreach ($menuPath in $menuPaths) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("$menuPath\command")
    $owned = $false
    if ($null -ne $key) {
        try { $owned = ([string]$key.GetValue('')).StartsWith('"' + $installedExe + '"', [StringComparison]::OrdinalIgnoreCase) }
        finally { $key.Dispose() }
    }
    if ($owned) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($menuPath, $false) }
}
$server = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("$classPath\LocalServer32")
$ownedServer = $false
if ($null -ne $server) {
    try { $ownedServer = ([string]$server.GetValue('')).StartsWith('"' + $installedExe + '"', [StringComparison]::OrdinalIgnoreCase) }
    finally { $server.Dispose() }
}
if ($ownedServer) { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($classPath, $false) }
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) '資料夾隨機放映.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    if ($shortcut.TargetPath -eq $installedExe) { Remove-Item -LiteralPath $shortcutPath }
}
$sendToShortcut = Join-Path ([Environment]::GetFolderPath('SendTo')) '隨機放映（含子資料夾）.lnk'
if (Test-Path -LiteralPath $sendToShortcut) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($sendToShortcut)
    if ($shortcut.TargetPath -eq $installedExe) { Remove-Item -LiteralPath $sendToShortcut }
}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FolderSlideShowShellRefresh {
    [DllImport("shell32.dll")] public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
'@
[FolderSlideShowShellRefresh]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Output 'Removed context-menu entries and desktop shortcut. Application files remain for optional manual deletion.'
