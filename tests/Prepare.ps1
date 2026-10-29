$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$fixture = Join-Path $PSScriptRoot '測試相簿 空格 & 符號'
$nested = Join-Path $fixture '子資料夾\孫資料夾'
New-Item -ItemType Directory -Path $nested -Force | Out-Null
function New-Photo($Path, $Background, $Text) {
    $bitmap = New-Object System.Drawing.Bitmap 1280, 720
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml($Background))
    $font = New-Object System.Drawing.Font 'Microsoft JhengHei UI', 48
    $graphics.DrawString($Text, $font, [System.Drawing.Brushes]::White, 70, 290)
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Jpeg)
    $font.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
New-Photo (Join-Path $fixture '01_照片.jpg') '#174F5A' '第一張 · 資料夾隨機放映'
New-Photo (Join-Path $nested '02_子資料夾.JPG') '#5C376D' '第二張 · 來自孫資料夾'
New-Photo (Join-Path $fixture '04_直式測試.jpg') '#274B35' '第四張 · 影片之後自動繼續'
Set-Content -LiteralPath (Join-Path $fixture '00_損壞.jpg') -Value 'deliberately invalid image'
Set-Content -LiteralPath (Join-Path $fixture 'ignore.txt') -Value 'not media'
$clip = Join-Path $fixture '03_影片.mp4'
& ffmpeg -hide_banner -loglevel error -y -f lavfi -i 'testsrc2=size=1280x720:rate=24' -t 3 -c:v libx264 -pix_fmt yuv420p $clip
if ($LASTEXITCODE -ne 0) { throw 'Fixture video generation failed' }
$junction = Join-Path $nested '迴圈連結'
$expectedSkipped = 0
try {
    if (-not (Test-Path -LiteralPath $junction)) { New-Item -ItemType Junction -Path $junction -Target $fixture | Out-Null }
    $expectedSkipped = 1
} catch { Write-Output 'Junction creation is unavailable in this environment; junction assertion will be skipped.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /reference:System.Core.dll "/out:$PSScriptRoot\CoreTests.exe" (Join-Path (Split-Path $PSScriptRoot) 'Core.cs') (Join-Path $PSScriptRoot 'CoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Tests compilation failed' }
& (Join-Path $PSScriptRoot 'CoreTests.exe') $fixture $expectedSkipped
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
Write-Output "Fixture: $fixture"
