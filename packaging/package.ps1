# Makes the Windows installer (Inno Setup) from a build in dist/<rid>/ (see build-all.ps1).
# Usage: packaging/package.ps1 -Rid win-x64 [-Version 1.12.0]
param(
    [Parameter(Mandatory = $true)][string]$Rid,
    [string]$Version = ""
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root "Directory.Build.props"))).Project.PropertyGroup.Version | Select-Object -First 1
}
$src = Join-Path $root "dist\$Rid"
if (-not (Test-Path $src)) { throw "no build in $src - run build-all.ps1 first" }

$iscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    choco install innosetup -y --no-progress | Out-Host
}
if (-not (Test-Path $iscc)) { throw "Inno Setup (ISCC.exe) not found" }

& $iscc "/DAppVersion=$Version" "/DRid=$Rid" "/DSourceDir=$src" "/DOutputDir=$(Join-Path $root 'dist')" (Join-Path $root "packaging\windows\AVAMMB1.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed: $LASTEXITCODE" }
Write-Host "packaged dist\AVAMMB1-$Version-$Rid-setup.exe"
