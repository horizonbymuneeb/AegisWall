$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $root
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$build = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $build | Out-Null
$out = Join-Path $build 'AegisWall.Tests.exe'
$coreSources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src\AegisWall.Core') -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName })
$refs = @('/r:System.dll','/r:System.Core.dll','/r:System.Web.Extensions.dll','/r:System.Xml.dll','/r:System.Xml.Linq.dll','/r:System.Security.dll')
$sources = $coreSources + @(Join-Path $root 'tests\AegisWall.Tests.cs')
& $csc /nologo /platform:x64 /target:exe ('/out:' + $out) $sources $refs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $out
exit $LASTEXITCODE
