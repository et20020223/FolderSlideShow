$ErrorActionPreference = 'Stop'
$app = Join-Path (Split-Path $PSScriptRoot) 'FolderSlideShow.exe'
if (-not (Test-Path -LiteralPath $app)) { throw 'Run Build.ps1 first.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $PSScriptRoot 'ShellSelectionTests.exe'
& $compiler /nologo /target:exe /platform:x64 /codepage:65001 /reference:System.Core.dll /reference:System.Windows.Forms.dll "/out:$testExe" (Join-Path $PSScriptRoot 'ShellSelectionTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Tests compilation failed' }
& $testExe $app
if ($LASTEXITCODE -ne 0) { throw 'Shell selection tests failed' }
