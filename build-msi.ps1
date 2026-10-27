[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$CompiledOnly,
    [switch]$PortableOnly
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'
$work = Join-Path $env:TEMP 'AegisWall-msi-work'
$stage = Join-Path $env:TEMP 'AegisWall-msi-stage'
$wixRoot = Join-Path $env:LOCALAPPDATA 'AegisWall\wix314'

if (-not $SkipBuild) {
    & (Join-Path $root 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

if (-not (Test-Path -LiteralPath (Join-Path $wixRoot 'candle.exe'))) {
    $zip = Join-Path $env:TEMP 'wix314-binaries.zip'
    if (-not (Test-Path -LiteralPath $zip)) {
        Write-Host 'Downloading WiX Toolset 3.14...'
        Invoke-WebRequest -Uri 'https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip' -OutFile $zip -UseBasicParsing
    }
    New-Item -ItemType Directory -Force -Path $wixRoot | Out-Null
    Expand-Archive -LiteralPath $zip -DestinationPath $wixRoot -Force
}

$candle = Join-Path $wixRoot 'candle.exe'
$light = Join-Path $wixRoot 'light.exe'
$heat = Join-Path $wixRoot 'heat.exe'
$wixUi = Join-Path $wixRoot 'WixUIExtension.dll'

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path $work | Out-Null
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host 'Staging MSI payload...'
Copy-Item -Path (Join-Path $root 'build\*') -Destination $stage -Recurse -Force
Remove-Item -LiteralPath (Join-Path $stage 'AegisWall.Tests.exe') -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $stage 'AegisWall.Tests.pdb') -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path (Join-Path $stage 'src') | Out-Null
Copy-Item -Path (Join-Path $root 'src\*') -Destination (Join-Path $stage 'src') -Recurse -Force

New-Item -ItemType Directory -Force -Path (Join-Path $stage 'docs') | Out-Null
Copy-Item -Path (Join-Path $root 'docs\*') -Destination (Join-Path $stage 'docs') -Recurse -Force

foreach ($file in @('install.ps1', 'uninstall.ps1', 'Run-AegisWall.ps1', 'Run-AegisWall-Cli.ps1', 'Run-AegisWall-Service.ps1', 'AegisWall-Portable.bat', 'README.md', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage -Force
}

Write-Host 'Harvesting payload...'
& $heat dir $stage -cg PayloadFiles -dr INSTALLFOLDER -var var.StageDir -gg -g1 -srd -sreg -scom -out (Join-Path $work 'PayloadFiles.wxs')
if ($LASTEXITCODE -ne 0) { throw 'heat.exe failed.' }

function Build-Msi {
    param(
        [string]$FileName,
        [string]$ProductName,
        [string]$Portable,
        [string]$UpgradeCode,
        [string]$ShortcutTarget,
        [string]$StartGuid,
        [string]$DesktopGuid
    )

    Write-Host "Compiling $ProductName..."
    & $candle -nologo -arch x64 `
        "-dStageDir=$stage" `
        "-dProductName=$ProductName" `
        "-dPortable=$Portable" `
        "-dShortcutTarget=$ShortcutTarget" `
        "-dUpgradeCode=$UpgradeCode" `
        "-dStartGuid=$StartGuid" `
        "-dDesktopGuid=$DesktopGuid" `
        "-dLicenseRtf=$(Join-Path $root 'installer\LICENSE.rtf')" `
        "-dVersion=0.1.0" `
        -out ($work + '\') `
        (Join-Path $root 'installer\AegisWall.wxs') `
        (Join-Path $work 'PayloadFiles.wxs')
    if ($LASTEXITCODE -ne 0) { throw "candle.exe failed for $ProductName." }

    Write-Host "Linking $FileName..."
    & $light -nologo -ext $wixUi -out (Join-Path $dist $FileName) (Join-Path $work 'AegisWall.wixobj') (Join-Path $work 'PayloadFiles.wixobj')
    if ($LASTEXITCODE -ne 0) { throw "light.exe failed for $FileName." }
}

if (-not $PortableOnly) {
    Build-Msi -FileName 'AegisWall-0.1.0-x64.msi' -ProductName 'AegisWall' -Portable '0' `
        -UpgradeCode '{6F1C2D8A-3B4E-4F5A-9C7D-1E2F3A4B5C60}' `
        -ShortcutTarget 'AegisWall.UI.exe' `
        -StartGuid '{A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D}' `
        -DesktopGuid '{B2C3D4E5-F6A7-4B8C-9D0E-1F2A3B4C5D6E}'
}

if (-not $CompiledOnly) {
    Build-Msi -FileName 'AegisWall-Portable-0.1.0-x64.msi' -ProductName 'AegisWall Portable' -Portable '1' `
        -UpgradeCode '{7A2D3E9B-4C5F-4A6B-8D8E-2F3A4B5C6D71}' `
        -ShortcutTarget 'AegisWall-Portable.bat' `
        -StartGuid '{C3D4E5F6-A7B8-4C9D-0E1F-2A3B4C5D6E7F}' `
        -DesktopGuid '{D4E5F6A7-B8C9-4D0E-9F1A-3B4C5D6E7F80}'
}

Get-ChildItem -LiteralPath $dist -Filter '*.msi' | Select-Object Name, Length

