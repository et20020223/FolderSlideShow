$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('Program.cs', 'Core.cs', 'Vlc.cs', 'WebpImage.cs', 'Player.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$output = Join-Path $PSScriptRoot 'FolderSlideShow.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/win32icon:$PSScriptRoot\app.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/out:$output" $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output "Built: $output"
