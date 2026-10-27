$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$failures = 0

function Fail([string]$message) {
    Write-Host "FAIL: $message" -ForegroundColor Red
    $script:failures++
}

function Ok([string]$message) {
    Write-Host "OK:   $message" -ForegroundColor Green
}

$scripts = @(
    (Join-Path $root 'build.ps1'),
    (Join-Path $root 'install.ps1'),
    (Join-Path $root 'uninstall.ps1'),
    (Join-Path $root 'Run-AegisWall.ps1'),
    (Join-Path $root 'Run-AegisWall-Cli.ps1'),
    (Join-Path $root 'Run-AegisWall-Service.ps1'),
    (Join-Path $root 'tests\run-tests.ps1'),
    (Join-Path $root 'tools\selftest.ps1'),
    (Join-Path $root 'tools\build-assets.ps1'),
    (Join-Path $root 'build-msi.ps1')
)

foreach ($script in $scripts) {
    if (-not (Test-Path -LiteralPath $script)) {
        Fail "missing script $script"
        continue
    }
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -gt 0) {
        Fail "${script}: $($errors[0].Message)"
    } else {
        Ok "syntax $([System.IO.Path]::GetFileName($script))"
    }
}

$required = @(
    'README.md', 'LICENSE', 'SECURITY.md', 'CONTRIBUTING.md', 'CHANGELOG.md',
    'src\AegisWall.Core\ProtectionEngine.cs',
    'src\AegisWall.Core\WfpEngine.cs',
    'src\AegisWall.Core\HardwareKey.cs',
    'src\AegisWall.UI\MainForm.cs',
    'src\AegisWall.UI\FieldControls.cs',
    'docs\SMART-APP-CONTROL.md',
    'docs\THREAT-MODEL.md',
    'docs\ARCHITECTURE.md',
    'docs\TEST-PLAN.md',
    'docs\MALWARE-TESTING.md',
    'docs\TEST-LAB.md'
)

foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $file))) {
        Fail "missing file $file"
    }
}

$icons = Get-ChildItem -LiteralPath (Join-Path $root 'assets\icons') -Filter '*.svg'
if ($icons.Count -lt 20) {
    Fail "expected at least 20 SVG icons, found $($icons.Count)"
} else {
    Ok "$($icons.Count) SVG icons"
}

foreach ($icon in $icons) {
    $content = Get-Content -LiteralPath $icon.FullName -Raw
    if ($content -notmatch 'd="') {
        Fail "icon has no path data: $($icon.Name)"
    }
}

foreach ($shot in @('screenshot.png', 'screenshot-apps.png', 'screenshot-lockdown.png', 'screenshot-key.png', 'screenshot-logs.png', 'screenshot-settings.png', 'screenshot-lab.png', 'screenshot-discover.png')) {
    $path = Join-Path $root "assets\$shot"
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "missing screenshot $shot"
    }
}

if ($failures -gt 0) {
    Write-Host "$failures validation failure(s)." -ForegroundColor Red
    exit 1
}

Write-Host 'Validation passed.' -ForegroundColor Green




