$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $root
$coreSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\AegisWall.Core') -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\Properties\\' } | ForEach-Object { $_.FullName })
$paths = $coreSources + @(Join-Path $root 'tests\AegisWall.Tests.cs')
$refs = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xml.dll','System.Xml.Linq.dll','System.Security.dll')

Add-Type -Path $paths -ReferencedAssemblies $refs
$exitCode = [AegisWall.Tests.Program]::Main(@())
exit $exitCode
