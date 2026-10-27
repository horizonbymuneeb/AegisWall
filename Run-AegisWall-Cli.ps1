param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Remaining
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$coreDir = Join-Path $root 'src\AegisWall.Core'
$cliDir = Join-Path $root 'src\AegisWall.Cli'
if (-not (Test-Path -LiteralPath $coreDir) -or -not (Test-Path -LiteralPath $cliDir)) {
    throw 'AegisWall source files were not found. Use the signed CLI or extract the portable package.'
}

$coreFiles = Get-ChildItem -LiteralPath $coreDir -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\Properties\\' }
$cliFiles = Get-ChildItem -LiteralPath $cliDir -Filter '*.cs'
$paths = @()
foreach ($file in $coreFiles) { $paths += $file.FullName }
foreach ($file in $cliFiles) { $paths += $file.FullName }

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
[Environment]::SetEnvironmentVariable('AEGISWALL_HOME', $root, 'Process')
[Environment]::CurrentDirectory = $root
if ($null -eq $Remaining) { $Remaining = @() }
$exitCode = [AegisWall.Cli.Program]::Main($Remaining)
exit $exitCode

