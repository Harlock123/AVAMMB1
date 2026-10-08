# Publishes single-file, self-contained AVAMMB1 builds for every supported platform into dist/<rid>/
# and packages each one as dist/AVAMMB1-<version>-<rid>.(zip|tar.gz).
#
# Usage:  ./build-all.ps1 [-Rids win-x64,linux-x64] [-NoReadyToRun] [-SkipTests]
param(
    [string[]] $Rids = @('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64'),
    [switch] $NoReadyToRun,
    [switch] $SkipTests
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

[xml] $props = Get-Content Directory.Build.props
$version = if ($env:VERSION) { $env:VERSION } else { ($props.Project.PropertyGroup.Version | Select-Object -First 1) }
$r2r = if ($NoReadyToRun) { 'false' } else { 'true' }
$project = 'src/AVAMMB1.App/AVAMMB1.App.csproj'
$docs = @('LICENSE', 'README.md', 'CREDITS.md', 'ASSETS_LICENSES.md')

Write-Host "AVAM&M build $version for: $($Rids -join ', ')"
if (-not $SkipTests) {
    dotnet test AVAMMB1.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}

if (Test-Path dist) { Remove-Item dist -Recurse -Force }
New-Item -ItemType Directory dist | Out-Null

foreach ($rid in $Rids) {
    $out = "dist/$rid"
    Write-Host "==> Publishing $rid"
    dotnet publish $project -c Release -r $rid `
        -p:PublishSingleFile=true -p:SelfContained=true -p:PublishReadyToRun=$r2r `
        -p:Version=$version -o $out
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid" }
    Copy-Item $docs $out
    $name = "AVAMMB1-$version-$rid"

    if ($rid -like 'win-*') {
        Compress-Archive -Path "$out/*" -DestinationPath "dist/$name.zip" -Force
    }
    elseif ($rid -like 'osx-*') {
        $app = "$out/AVAMMB1.app"
        New-Item -ItemType Directory "$app/Contents/MacOS", "$app/Contents/Resources" -Force | Out-Null
        Move-Item "$out/AVAMMB1" "$app/Contents/MacOS/AVAMMB1"
        Copy-Item Assets/Icons/avammb1.icns "$app/Contents/Resources/"
        (Get-Content packaging/macos/Info.plist -Raw).Replace('__VERSION__', $version) |
            Set-Content "$app/Contents/Info.plist" -NoNewline
        # Note: archives created on Windows may not preserve the executable bit; run
        # chmod +x AVAMMB1.app/Contents/MacOS/AVAMMB1 after extracting if needed.
        tar -C $out -czf "dist/$name.tar.gz" .
    }
    else {
        tar -C $out -czf "dist/$name.tar.gz" .
    }
    Write-Host "    packaged dist/$name"
}

Write-Host 'Done. Artifacts:'
Get-ChildItem dist -File | Format-Table Name, Length
