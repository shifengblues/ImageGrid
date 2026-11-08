$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$licenses = @(
    'skiasharp\3.119.1\LICENSE.txt',
    'skiasharp.nativeassets.win32\3.119.1\THIRD-PARTY-NOTICES.txt',
    'microsoft.netcore.app.runtime.win-x64\10.0.0\LICENSE.TXT',
    'microsoft.netcore.app.runtime.win-x64\10.0.0\THIRD-PARTY-NOTICES.TXT',
    'microsoft.windowsdesktop.app.runtime.win-x64\10.0.0\LICENSE'
)
$text = foreach ($license in $licenses) {
    "`n=== $license ===`n"
    Get-Content -LiteralPath (Join-Path $nugetRoot $license) -Raw
}
Set-Content -LiteralPath (Join-Path $projectRoot 'dist\THIRD-PARTY-NOTICES.txt') -Value $text -Encoding utf8
