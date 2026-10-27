[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$ServiceOnly,
    [switch]$PurgeData,
    [string]$InstallDir = (Join-Path $env:ProgramFiles 'AegisWall')
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run uninstall.ps1 from an elevated PowerShell window.'
}

if (-not $Force) {
    Write-Warning 'This stops AegisWall, removes every persistent WFP filter, deletes the service and watchdog task.'
    $answer = Read-Host 'Continue? [y/N]'
    if ($answer -notmatch '^[yY]') {
        Write-Host 'Cancelled.'
        return
    }
}

$serviceName = 'AegisWall'
$taskName = 'AegisWall Watchdog'
$dataDir = Join-Path $env:ProgramData 'AegisWall'

try {
    Stop-ScheduledTask -TaskName 'AegisWall Core' -ErrorAction SilentlyContinue
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($service -and $service.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
        (Get-Service -Name $serviceName).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))
    }
}
catch {
    Write-Warning "Could not stop the service: $($_.Exception.Message)"
}

$resetDone = $false
if (Test-Path -LiteralPath (Join-Path $InstallDir 'AegisWall.Cli.exe')) {
    try {
        & (Join-Path $InstallDir 'AegisWall.Cli.exe') reset --force
        $resetDone = $true
    }
    catch {
        Write-Warning "Compiled CLI reset failed: $($_.Exception.Message)"
    }
}
if (-not $resetDone -and (Test-Path -LiteralPath (Join-Path $InstallDir 'Run-AegisWall-Cli.ps1'))) {
    try {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $InstallDir 'Run-AegisWall-Cli.ps1') reset --force
        $resetDone = $true
    }
    catch {
        Write-Warning "Portable CLI reset failed: $($_.Exception.Message)"
    }
}
if (-not $resetDone) {
    Write-Warning 'Could not run the reset command. Persistent WFP filters may remain.'
}

try {
    foreach ($folder in @([Environment]::GetFolderPath('CommonStartMenu'), [Environment]::GetFolderPath('CommonDesktopDirectory'))) {
        if (-not [string]::IsNullOrEmpty($folder)) {
            $link = Join-Path $folder 'AegisWall.lnk'
            if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link -Force }
        }
    }
}
catch {
    Write-Warning "Could not remove shortcuts: $($_.Exception.Message)"
}

try {
    Unregister-ScheduledTask -TaskName 'AegisWall Core' -Confirm:$false -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
}
catch {
    Write-Warning "Could not remove the watchdog task: $($_.Exception.Message)"
}

try {
    & sc.exe delete $serviceName | Out-Null
}
catch {
    Write-Warning "Could not delete the service: $($_.Exception.Message)"
}

if (-not $ServiceOnly) {
    if (Test-Path -LiteralPath $InstallDir) {
        Remove-Item -LiteralPath $InstallDir -Recurse -Force
    }

    if ($PurgeData -and (Test-Path -LiteralPath $dataDir)) {
        Remove-Item -LiteralPath $dataDir -Recurse -Force
        Write-Host 'Configuration and audit data removed.'
    } else {
        Write-Host "Configuration retained at $dataDir. Use -PurgeData to remove it."
    }
} else {
    Write-Host 'Service and filters removed. MSI will delete program files.'
}

Write-Host 'AegisWall uninstalled.'



