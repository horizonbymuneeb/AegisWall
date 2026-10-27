[CmdletBinding()]
param(
    [switch]$Portable,
    [switch]$SkipBuild,
    [switch]$ServiceOnly,
    [switch]$Force,
    [string]$InstallDir = (Join-Path $env:ProgramFiles 'AegisWall')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run install.ps1 from an elevated PowerShell window.'
    }
}

function Get-SacState {
    try {
        $value = (Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' -Name 'VerifiedAndReputablePolicyState' -ErrorAction Stop).VerifiedAndReputablePolicyState
        return [int]$value
    }
    catch {
        return -1
    }
}

Assert-Administrator

$transcript = $false
if ($ServiceOnly) {
    try {
        $logDir = Join-Path $env:ProgramData 'AegisWall'
        New-Item -ItemType Directory -Force -Path $logDir | Out-Null
        Start-Transcript -Path (Join-Path $logDir 'msi-install.log') -Append | Out-Null
        $transcript = $true
    } catch {
    }
}

$sacState = Get-SacState
if ($sacState -eq 1 -or $sacState -eq 2) {
    Write-Warning "Smart App Control is ON (state $sacState). Unsigned AegisWall EXEs may be blocked."
    Write-Host 'AegisWall will use the portable PowerShell-hosted mode so the service is not blocked.'
    Write-Host 'See docs\SMART-APP-CONTROL.md for the full explanation and signed-release options.'
    if (-not $Portable) {
        if ($ServiceOnly -or $Force) {
            $Portable = [switch]$true
            Write-Host 'Smart App Control is on; switching to portable mode.'
        }
        else {
            $answer = Read-Host 'Use portable mode instead of unsigned EXEs? [Y/n]'
            if ($answer -notmatch '^[nN]') {
                $Portable = [switch]$true
            }
        }
    }
}

if (-not $ServiceOnly -and -not $Portable -and -not $SkipBuild) {
    Write-Host 'Building AegisWall...'
    & (Join-Path $root 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

if (Test-Path -LiteralPath $InstallDir) {
    Write-Host "Updating existing install at $InstallDir"
} else {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
}

$dataDir = Join-Path $env:ProgramData 'AegisWall'
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

if (-not $ServiceOnly) {
    if ($Portable) {
        Write-Host 'Installing portable (PowerShell-hosted) mode...'
        Copy-Item -LiteralPath (Join-Path $root 'src') -Destination $InstallDir -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $root 'assets') -Destination $InstallDir -Recurse -Force
        foreach ($file in @('Run-AegisWall.ps1', 'Run-AegisWall-Cli.ps1', 'Run-AegisWall-Service.ps1', 'AegisWall-Portable.bat')) {
            Copy-Item -LiteralPath (Join-Path $root $file) -Destination $InstallDir -Force
        }
    } else {
        Write-Host 'Installing compiled mode...'
        Copy-Item -Path (Join-Path $root 'build\*') -Destination $InstallDir -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $root 'src') -Destination $InstallDir -Recurse -Force
        foreach ($file in @('Run-AegisWall.ps1', 'Run-AegisWall-Cli.ps1', 'Run-AegisWall-Service.ps1', 'AegisWall-Portable.bat')) {
            Copy-Item -LiteralPath (Join-Path $root $file) -Destination $InstallDir -Force
        }
    }

    foreach ($file in @('README.md', 'LICENSE', 'docs')) {
        $source = Join-Path $root $file
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination $InstallDir -Recurse -Force
        }
    }
}

if ($Portable) {
    Set-Content -LiteralPath (Join-Path $InstallDir 'portable.mode') -Value 'portable' -Encoding ASCII
}

& icacls.exe $InstallDir /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)RX' 'Users:(OI)(CI)RX' | Out-Null
& icacls.exe $dataDir /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' 'Users:(OI)(CI)R' | Out-Null

$serviceName = 'AegisWall'
$coreTaskName = 'AegisWall Core'
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host 'Stopping existing AegisWall service...'
    try { Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue } catch { }
    & sc.exe delete $serviceName | Out-Null
    $removed = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        $check = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
        if (-not $check) { $removed = $true; break }
    }
    if (-not $removed) {
        throw 'The existing AegisWall service is still marked for deletion. Reboot the PC, then run the installer again.'
    }
}

Unregister-ScheduledTask -TaskName $coreTaskName -Confirm:$false -ErrorAction SilentlyContinue

if (-not $Portable) {
    try {
        $binary = '"' + (Join-Path $InstallDir 'AegisWall.Service.exe') + '"'
        New-Service -Name $serviceName -BinaryPathName $binary -DisplayName 'AegisWall' -Description 'Hardware-anchored application firewall and network killswitch.' -StartupType Automatic | Out-Null
    }
    catch {
        throw "Could not create the AegisWall service: $($_.Exception.Message)"
    }

    & sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    try {
        & sc.exe sdset $serviceName 'D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)' | Out-Null
    }
    catch {
        Write-Warning 'Could not apply the service DACL.'
    }
}

$taskName = 'AegisWall Watchdog'
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
try {
    if ($Portable) {
        $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $taskAction = New-ScheduledTaskAction -Execute $powershell -Argument ('-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $InstallDir 'Run-AegisWall-Cli.ps1') + '" watchdog')
    } else {
        $taskAction = New-ScheduledTaskAction -Execute (Join-Path $InstallDir 'AegisWall.Cli.exe') -Argument 'watchdog'
    }
    $atStartup = New-ScheduledTaskTrigger -AtStartup
    $everyMinute = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1) -RepetitionDuration (New-TimeSpan -Days 3650)
    Register-ScheduledTask -TaskName $taskName -Action $taskAction -Trigger @($atStartup, $everyMinute) -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
}
catch {
    Write-Warning "Could not create the watchdog task: $($_.Exception.Message)"
}

try {
    & auditpol.exe /set /subcategory:'Filtering Platform Connection' /failure:enable | Out-Null
}
catch {
    Write-Warning 'Could not enable Filtering Platform audit policy. Automatic trigger detection may be unavailable.'
}

try {
    if ($Portable) {
        $powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $coreAction = New-ScheduledTaskAction -Execute $powershell -Argument ('-NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $InstallDir 'Run-AegisWall-Service.ps1') + '" -Console')
        $coreTrigger = New-ScheduledTaskTrigger -AtStartup
        Register-ScheduledTask -TaskName $coreTaskName -Action $coreAction -Trigger $coreTrigger -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
        Start-ScheduledTask -TaskName $coreTaskName
        Start-Sleep -Seconds 6
        $coreTask = Get-ScheduledTask -TaskName $coreTaskName -ErrorAction Stop
        Write-Host ("Core task status: " + $coreTask.State)
        if ($ServiceOnly -and $coreTask.State -ne 'Running') {
            throw "AegisWall Core task status is $($coreTask.State). Check $dataDir\service-bootstrap.log."
        }
    }
    else {
        Start-Service -Name $serviceName
        Start-Sleep -Seconds 2
        $service = Get-Service -Name $serviceName
        Write-Host ("Service status: " + $service.Status)
        if ($ServiceOnly -and $service.Status -ne 'Running') {
            throw "AegisWall service status is $($service.Status). Check the service log and Smart App Control settings."
        }
    }
}
catch {
    Write-Warning "AegisWall did not start: $($_.Exception.Message)"
    Write-Host "Check $dataDir\service-bootstrap.log"
    if ($ServiceOnly) {
        throw
    }
}
try {
    $target = if ($Portable) { Join-Path $InstallDir 'AegisWall-Portable.bat' } else { Join-Path $InstallDir 'AegisWall.UI.exe' }
    $shell = New-Object -ComObject WScript.Shell
    foreach ($folder in @([Environment]::GetFolderPath('CommonStartMenu'), [Environment]::GetFolderPath('CommonDesktopDirectory'))) {
        if (-not [string]::IsNullOrEmpty($folder)) {
            $link = $shell.CreateShortcut((Join-Path $folder 'AegisWall.lnk'))
            $link.TargetPath = $target
            $link.WorkingDirectory = $InstallDir
            $link.Description = 'Hardware-anchored application firewall'
            $icon = Join-Path $InstallDir 'assets\app.ico'
            if (Test-Path -LiteralPath $icon) { $link.IconLocation = "$icon,0" }
            $link.Save()
        }
    }
}
catch {
    Write-Warning "Could not create shortcuts: $($_.Exception.Message)"
}

Write-Host ''
Write-Host 'AegisWall installed.'
if ($Portable) {
    Write-Host 'Launch the UI with: ' + (Join-Path $InstallDir 'AegisWall-Portable.bat')
    Write-Host 'CLI from an elevated prompt: powershell -File "' + (Join-Path $InstallDir 'Run-AegisWall-Cli.ps1') + '" status'
} else {
    Write-Host 'Launch the UI with: ' + (Join-Path $InstallDir 'AegisWall.UI.exe')
}
Write-Host ''
Write-Host 'Next:'
Write-Host '  1. Enroll a YubiKey or USB key.'
Write-Host '  2. Add your applications to the allowlist.'
Write-Host '  3. Arm protection.'
Write-Host 'Recovery code ko offline save karein.'

if ($transcript) { try { Stop-Transcript | Out-Null } catch { } }







