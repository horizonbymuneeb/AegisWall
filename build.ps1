[CmdletBinding()]
param(
    [switch]$Test,
    [switch]$NoClean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDir = Join-Path $root 'build'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "C# compiler not found: $csc"
}

if (-not $NoClean -and (Test-Path -LiteralPath $buildDir)) {
    Remove-Item -LiteralPath $buildDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

$common = @('/nologo', '/platform:x64', '/langversion:default', '/optimize+', '/debug:pdbonly', '/utf8output')
$coreSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\AegisWall.Core') -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName })
$serviceSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\AegisWall.Service') -Filter '*.cs' | ForEach-Object { $_.FullName })
$cliSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\AegisWall.Cli') -Filter '*.cs' | ForEach-Object { $_.FullName })
$uiSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\AegisWall.UI') -Filter '*.cs' | ForEach-Object { $_.FullName })

$coreRefs = @(
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Web.Extensions.dll',
    '/r:System.Xml.dll',
    '/r:System.Xml.Linq.dll',
    '/r:System.Security.dll'
)

$serviceRefs = $coreRefs + @('/r:System.ServiceProcess.dll')
$cliRefs = $coreRefs + @('/r:System.ServiceProcess.dll')

$wpfDir = Join-Path ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'WPF'
$uiRefs = @(
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    "/r:$wpfDir\PresentationCore.dll",
    "/r:$wpfDir\WindowsBase.dll"
)
$uiIcon = Join-Path $root 'assets\app.ico'
if (Test-Path -LiteralPath $uiIcon) { $uiRefs += "/win32icon:$uiIcon" }

function Invoke-Csc {
    param(
        [string]$Target,
        [string]$Output,
        [string[]]$Sources,
        [string[]]$References
    )

    $arguments = @($common) + @("/target:$Target", "/out:$Output") + $Sources + $References
    & $csc $arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Compilation failed for $Output (exit $LASTEXITCODE)"
    }
}

Write-Host 'Compiling AegisWall.Service.exe'
Invoke-Csc -Target 'exe' -Output (Join-Path $buildDir 'AegisWall.Service.exe') -Sources ($coreSources + $serviceSources) -References $serviceRefs

Write-Host 'Compiling AegisWall.Cli.exe'
Invoke-Csc -Target 'exe' -Output (Join-Path $buildDir 'AegisWall.Cli.exe') -Sources ($coreSources + $cliSources) -References $cliRefs

if ($uiSources.Count -gt 0) {
    Write-Host 'Compiling AegisWall.UI.exe'
    Invoke-Csc -Target 'winexe' -Output (Join-Path $buildDir 'AegisWall.UI.exe') -Sources ($coreSources + $uiSources) -References $uiRefs
}

$assetsSource = Join-Path $root 'assets'
$assetsTarget = Join-Path $buildDir 'assets'
if (Test-Path -LiteralPath $assetsSource) {
    if (Test-Path -LiteralPath $assetsTarget) {
        Remove-Item -LiteralPath $assetsTarget -Recurse -Force
    }
    Copy-Item -LiteralPath $assetsSource -Destination $assetsTarget -Recurse -Force
}

Set-Content -LiteralPath (Join-Path $buildDir 'version.txt') -Value '0.1.1' -Encoding ASCII
Write-Host ("Build complete: " + $buildDir)

if ($Test) {
    & (Join-Path $root 'tests\run-tests.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed."
    }
}


