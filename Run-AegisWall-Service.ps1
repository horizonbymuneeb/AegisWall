$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dataDir = Join-Path $env:ProgramData 'AegisWall'
$logPath = Join-Path $dataDir 'service-bootstrap.log'

try {
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    $coreDir = Join-Path $root 'src\AegisWall.Core'
    $serviceDir = Join-Path $root 'src\AegisWall.Service'
    if (-not (Test-Path -LiteralPath $coreDir) -or -not (Test-Path -LiteralPath $serviceDir)) {
        throw 'AegisWall source files were not found in the portable install.'
    }

    $coreFiles = Get-ChildItem -LiteralPath $coreDir -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\Properties\\' }
    $serviceFiles = Get-ChildItem -LiteralPath $serviceDir -Filter '*.cs'
    $paths = @()
    foreach ($file in $coreFiles) { $paths += $file.FullName }
    foreach ($file in $serviceFiles) { $paths += $file.FullName }

    $references = @(
        'System.dll',
        'System.Core.dll',
        'System.ServiceProcess.dll',
        'System.Web.Extensions.dll',
        'System.Xml.dll',
        'System.Xml.Linq.dll',
        'System.Security.dll'
    )

    Add-Type -Path $paths -ReferencedAssemblies $references
    [Environment]::SetEnvironmentVariable('AEGISWALL_PORTABLE', '1', 'Process')
    [Environment]::CurrentDirectory = $root
    [AegisWall.Service.Program]::Main(@())
}
catch {
    try {
        Add-Content -LiteralPath $logPath -Value ((Get-Date).ToString('o') + ' ' + $_.Exception.ToString())
    } catch {
    }
    throw
}
