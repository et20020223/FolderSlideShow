$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
& python (Join-Path $PSScriptRoot 'make_webp_fixtures.py')
if ($LASTEXITCODE -ne 0) { throw 'Fixture creation failed' }
foreach ($file in @('libsharpyuv.dll','libwebp.dll','libwebpdemux.dll')) {
    Copy-Item -LiteralPath (Join-Path $project $file) -Destination (Join-Path $PSScriptRoot $file) -Force
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /reference:System.Core.dll /reference:System.Drawing.dll "/out:$PSScriptRoot\WebpTests.exe" (Join-Path $project 'WebpImage.cs') (Join-Path $project 'Core.cs') (Join-Path $PSScriptRoot 'WebpTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& (Join-Path $PSScriptRoot 'WebpTests.exe') (Join-Path $PSScriptRoot 'WebP 測試 & 空格') (Join-Path $PSScriptRoot 'webp-reference')
if ($LASTEXITCODE -ne 0) { throw 'WebP checks failed' }
