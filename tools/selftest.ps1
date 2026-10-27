param(
    [string]$Page = 'dashboard',
    [string]$Out = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$coreFiles = Get-ChildItem -LiteralPath (Join-Path $root 'src\AegisWall.Core') -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\Properties\\' }
$uiFiles = Get-ChildItem -LiteralPath (Join-Path $root 'src\AegisWall.UI') -Filter '*.cs'

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

[Environment]::CurrentDirectory = $root
$arguments = @('--selftest', '--page', $Page)
if (-not [string]::IsNullOrEmpty($Out)) {
    $arguments += @('--out', $Out)
}
[AegisWall.UI.Program]::Main($arguments)
