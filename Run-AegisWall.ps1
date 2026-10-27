$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$coreDir = Join-Path $root 'src\AegisWall.Core'
$uiDir = Join-Path $root 'src\AegisWall.UI'
if (-not (Test-Path -LiteralPath $coreDir) -or -not (Test-Path -LiteralPath $uiDir)) {
    throw 'AegisWall source files were not found. Use the signed build or extract the portable package.'
}

$coreFiles = Get-ChildItem -LiteralPath $coreDir -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\Properties\\' }
$uiFiles = Get-ChildItem -LiteralPath $uiDir -Filter '*.cs'
$paths = @()
foreach ($file in $coreFiles) { $paths += $file.FullName }
foreach ($file in $uiFiles) { $paths += $file.FullName }

$wpf = Join-Path ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) 'WPF'
$references = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll',
    'System.Xml.dll',
    'System.Xml.Linq.dll',
    'System.Security.dll',
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'WindowsBase.dll')
)

Add-Type -Path $paths -ReferencedAssemblies $references
[Environment]::SetEnvironmentVariable('AEGISWALL_PORTABLE', '1', 'Process')
[Environment]::SetEnvironmentVariable('AEGISWALL_HOME', $root, 'Process')
[Environment]::CurrentDirectory = $root
[AegisWall.UI.Program]::Main(@())

