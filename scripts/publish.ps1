$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1')
$sdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $projectRoot
try {
    & $sdk publish src/StoryboardGrid -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o dist
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item README.md dist/使用说明.md -Force
    Copy-Item docs/KNOWN-LIMITATIONS.md dist/已知限制.md -Force
    & (Join-Path $PSScriptRoot 'notices.ps1')
    $report = Join-Path $projectRoot 'tests\output\release'
    $exe = Join-Path $projectRoot 'dist\ImageGrid.exe'
    $probe = Start-Process -FilePath $exe -ArgumentList @('--verify',('"' + $report + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($probe.ExitCode -ne 0) { throw "Published EXE verification failed. See $report" }
    Copy-Item (Join-Path $report 'release-check.json') docs/release-check.json -Force
    Copy-Item (Join-Path $report 'window-export-check.json') docs/window-export-check.json -Force
    $version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion.Split('.')[0..2] -join '.'
    Copy-Item -LiteralPath $exe -Destination (Join-Path $projectRoot "dist\ImageGrid-$version.exe") -Force
    Get-FileHash -LiteralPath $exe -Algorithm SHA256 | Format-List
} finally { Pop-Location }
