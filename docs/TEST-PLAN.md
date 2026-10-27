# AegisWall test plan

Use a **spare Windows 11 VM or laptop**. AegisWall changes WFP filters and can lock the network. Keep a recovery path and a snapshot.

For malware/C2/ransomware containment scenarios, see [MALWARE-TESTING.md](MALWARE-TESTING.md).

## Test matrix

| ID | Area | Test | Expected |
| --- | --- | --- | --- |
| TC-01 | MSI | Install compiled MSI | Service, watchdog, filters, ACLs installed |
| TC-02 | MSI | Install portable MSI | PowerShell-hosted service, portable marker, UI launches |
| TC-03 | SAC | SAC on, compiled MSI | Documented warning/block; portable works |
| TC-04 | Key | Enroll YubiKey | Slot 2 programmed, touch required, recovery code shown |
| TC-05 | Key | Enroll USB key | `aegiswall.key` written, machine-bound, unlock works |
| TC-06 | Arm | Arm with key | Status `Protected`, filters applied |
| TC-07 | Allowlist | Allow one app, block another | Allowed app connects, other app blocked |
| TC-08 | Apps | Toggle outbound/inbound/enabled | WFP filters change; blocked app cannot connect |
| TC-09 | Lockdown | Blocked attempt triggers timer | All external traffic blocked for N minutes |
| TC-10 | Lockdown | Manual lockdown + unlock | Countdown, then restore |
| TC-11 | Key | Remove key, reinsert within grace | Network stays up, no lockdown |
| TC-12 | Key | Remove key past grace | Fail-closed; optional lockdown timer |
| TC-13 | Key | Boot without key while armed | `KeyLocked`, no allowlisted traffic |
| TC-14 | Tamper | Edit `config.json` | `Unverified`; default-deny applied |
| TC-15 | Service | Stop service while armed | Persistent filters remain; network still blocked |
| TC-16 | Reboot | Reboot while armed | Filters restored; `KeyLocked` until unlock |
| TC-17 | Recovery | Disarm with recovery code | Service disarmed; filters removed |
| TC-18 | Recovery | Rotate recovery code | Old code rejected, new code works |
| TC-19 | Import/export | Export then import allowlist | Apps restored with toggles |
| TC-20 | Audit | Verify chain, export CSV | Chain valid; CSV opens |
| TC-21 | Uninstall | Uninstall MSI | Service/task/filters removed; data policy respected |
| TC-22 | Security | Non-admin control pipe | Access denied |
| TC-23 | DNS | Pinned DNS | Only listed resolvers allowed |
| TC-24 | UI | Inputs, dropdowns, number fields | Rounded fields, no clipping, validation works |
| TC-25 | Discovery | Discover installed apps | Real Start Menu/registry apps appear with logos; allow selected |

## 0. Preparation

1. Snapshot the VM.
2. Confirm the build:

```powershell
Get-FileHash .\dist\AegisWall-0.1.1-x64.msi -Algorithm SHA256
Get-FileHash .\dist\AegisWall-Portable-0.1.1-x64.msi -Algorithm SHA256
```

3. Check SAC:

```powershell
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' `
  -Name VerifiedAndReputablePolicyState
```

4. Keep an elevated PowerShell open.

## TC-01 - Compiled MSI

```powershell
msiexec /i .\dist\AegisWall-0.1.1-x64.msi /l*v .\install-compiled.log
```

Verify:

```powershell
Get-Service AegisWall | Format-List Name,Status,StartType
Get-ScheduledTask -TaskName 'AegisWall Watchdog' | Format-List TaskName,State
Test-Path 'C:\Program Files\AegisWall\AegisWall.Service.exe'
Test-Path 'C:\ProgramData\AegisWall'
```

Expected:

- Service `Running`, start type `Automatic`.
- Watchdog task `Ready`/`Running`, user `SYSTEM`.
- `status.json` appears within a few seconds.

If SAC is on, expect a block/failure; use TC-02 or a signed build.

## TC-02 - Portable MSI

```powershell
msiexec /i .\dist\AegisWall-Portable-0.1.1-x64.msi /l*v .\install-portable.log
```

Verify:

```powershell
Get-Service AegisWall | Format-List Name,Status
Get-Content 'C:\Program Files\AegisWall\portable.mode'
Get-Content 'C:\ProgramData\AegisWall\service-bootstrap.log' -ErrorAction SilentlyContinue
```

Launch:

```text
C:\Program Files\AegisWall\AegisWall-Portable.bat
```

Expected: UI opens; first launch takes a few seconds because the C# sources compile in memory.

## TC-03 - Smart App Control

1. Turn SAC **On** (Windows Security -> App & browser control -> Smart App Control).
2. Try the compiled MSI/EXE.
3. Try the portable MSI/launcher.

Expected:

- Compiled unsigned EXE may be blocked with "An Application Control policy has blocked this file".
- Portable mode works through the signed PowerShell host.
- The installer warns before installing unsigned EXEs.
- `docs/SMART-APP-CONTROL.md` explains signed-release and SAC-off options.

Do **not** change `VerifiedAndReputablePolicyState` manually.

## TC-04 - YubiKey enrollment

1. Insert a YubiKey.
2. UI -> Hardware key -> **YubiKey** -> **Enroll key**.
3. Approve UAC, confirm slot 2 programming, touch the key.

Expected:

- `ykman otp info` shows slot 2 challenge-response.
- UI shows provider `yubikey`, serial, "Connected and enrolled".
- Recovery code is printed once; save it.
- `config.json` has `keyProvider: "yubikey"`, non-empty `vaultBlob`, `keyChallenge`.

Test wrong key:

- Insert a different YubiKey and click **Unlock now** -> error, no policy change.

## TC-05 - USB key enrollment

1. Format a USB drive, set volume label `AEGISWALL`.
2. UI -> Hardware key -> **USB key** -> label `AEGISWALL` -> **Enroll key**.
3. Verify `AEGISWALL:\aegiswall.key` exists.
4. Unlock with the USB inserted.
5. Copy `aegiswall.key` to another PC and try to unlock.

Expected:

- First PC unlocks.
- Second PC cannot decrypt (DPAPI machine scope).
- Removing the USB starts the grace timer.

## TC-06 - Arm

UI -> Dashboard -> **Arm protection**.

Expected:

- Status `Protected`.
- `AegisWall.Cli.exe diagnostics` reports `wfpOpen: true`, `filterCount > 0`, `configVerified: true`.
- `audit.jsonl` gets `arm`.

## TC-07 - Allow one app, block another

1. Allow `C:\Program Files\Google\Chrome\Application\chrome.exe`.
2. Browse to `https://example.com` -> works.
3. Run an unapproved client, for example copy `curl.exe` to `C:\Temp\test-client.exe` and run:

```powershell
Copy-Item C:\Windows\System32\curl.exe C:\Temp\test-client.exe
C:\Temp\test-client.exe https://example.com
```

Expected:

- Chrome connects.
- `test-client.exe` fails to connect.
- Security event `5157` appears:

```powershell
Get-WinEvent -FilterHashtable @{LogName='Security'; Id=5157} -MaxEvents 5 |
  Format-List TimeCreated, Message
```

## TC-08 - Per-app toggles

In Applications:

- Turn **Out** off for Chrome -> Chrome cannot connect.
- Turn **In** on for a server app -> inbound filter added.
- Turn **On** off -> all filters for that app removed.
- Use search and access filters.

Expected: status filter count changes; behavior matches toggles.

## TC-09 - Automatic lockdown

With trigger mode `User apps only`:

1. Run `C:\Temp\test-client.exe https://example.com`.
2. Wait for the blocked event.

Expected:

- Status `Lockdown`, countdown starts.
- Chrome and the test client cannot reach the internet.
- Loopback and DHCP still work.
- After the timer expires, allowlisted traffic returns.

If no trigger: check the audit policy:

```powershell
auditpol /get /subcategory:'Filtering Platform Connection'
```

## TC-10 - Manual lockdown

UI -> Lockdown -> **Lock network now** (e.g., 1 minute).

Expected: immediate `Lockdown`; unlock after timer or **Unlock now**.

## TC-11 - Key removal within grace

1. While protected, remove the USB/YubiKey.
2. Reinsert within 30 seconds.

Expected: status `Key grace` briefly; no lockdown; audit `key.removed.grace` + `key.returned`.

## TC-12 - Key removal past grace

1. Remove the key and wait longer than the grace period.

Expected:

- `KeyLocked` or `Lockdown`; all external traffic blocked.
- If **Lockdown on key removal** is on, a timer starts.
- Audit `key.removed`.

## TC-13 - Boot without key

1. Arm, then reboot without the key.

Expected:

- Persistent default-deny filters restored.
- Status `KeyLocked`.
- Insert key and **Unlock now** to restore allowlisted traffic.

## TC-14 - Config tampering

1. Stop the service (or use a copy).
2. Edit `%ProgramData%\AegisWall\config.json` (for example add an app).
3. Start the service and unlock.

Expected: `Unverified`; allowlist is not applied; network is blocked. Audit `wfp.error`/verification failure.

## TC-15 - Stop service while armed

```powershell
Stop-Service AegisWall
```

Expected:

- Service stops.
- WFP filters remain (persistent).
- Network stays blocked for unapproved apps.
- Watchdog restarts the service within a minute.

Restore:

```powershell
Start-Service AegisWall
```

## TC-16 - Reboot while armed

1. Arm and unlock.
2. Reboot.

Expected:

- Service starts.
- Persistent filters are restored.
- UI shows `KeyLocked` until the key is unlocked.
- No allowlisted traffic before unlock.

## TC-17 - Recovery code

```powershell
AegisWall.Cli.exe disarm --recovery <code>
```

Expected: disarm succeeds without the key; filters removed; audit `disarm`.

## TC-18 - Rotate recovery

UI -> Hardware key -> **Rotate code**; save the new code printed in the elevated console.

Expected:

- Old code fails.
- New code succeeds.

## TC-19 - Export/import

```powershell
AegisWall.Cli.exe export C:\Temp\allowlist.json
AegisWall.Cli.exe import C:\Temp\allowlist.json --replace
```

Expected: app list, outbound/inbound/enabled and notes restored.

## TC-20 - Audit chain

UI -> Audit log -> **Verify chain**; **Export CSV**.

Expected: `Chain valid`; CSV contains Time, Event, Actor, Detail.

Tamper test (on a copy/VM): edit a line in `audit.jsonl`; verify reports the broken sequence.

## TC-21 - Uninstall

```powershell
msiexec /x .\dist\AegisWall-0.1.1-x64.msi /l*v .\uninstall.log
```

Expected:

- Service deleted.
- Watchdog task removed.
- WFP filters removed (`AegisWall.Cli.exe status` not available after removal; check before uninstall or use `netsh wfp show state`).
- Program Files removed.
- `%ProgramData%\AegisWall` retained unless purged.

Manual purge:

```powershell
.\uninstall.ps1 -Force -PurgeData
```

## TC-22 - Non-admin pipe

As a standard user:

```powershell
AegisWall.Cli.exe arm
```

Expected: UAC prompt; if declined, no change. Direct pipe connection from a non-admin should be denied.

## TC-23 - Pinned DNS

1. Settings -> DNS mode `Pinned servers` -> `1.1.1.1`.
2. Allow Chrome; browse.
3. Change to an unlisted resolver in the OS; DNS should fail.

Expected: only `svchost` DNS to `1.1.1.1:53` is permitted.

## TC-24 - UI input design

Check:

- Rounded search/text fields with focus accent.
- Custom dropdowns (no native ComboBox look).
- Number fields with custom up/down chevrons.
- Toggles for outbound/inbound/enabled.
- No clipped labels or fields at 1120Ãƒâ€”880.
- Validation: number fields clamp; empty search shows empty state.

## TC-25 - Installed app discovery

1. Applications -> **Discover**.
2. Search for an installed app.
3. Select it and click **Allow selected**.

Expected: the list is populated from Start Menu, uninstall registry and App Paths (not dummy data), each row has the real EXE icon, and selected apps appear in the allowlist.

## Logs to collect on failure

```powershell
Get-Service AegisWall | Format-List *
Get-ScheduledTask -TaskName 'AegisWall Watchdog' | Format-List *
Get-Content 'C:\ProgramData%\AegisWall\audit.jsonl' -Tail 50
Get-Content 'C:\ProgramData%\AegisWall\service-bootstrap.log' -Tail 50 -ErrorAction SilentlyContinue
Get-WinEvent -FilterHashtable @{LogName='Security'; Id=5157} -MaxEvents 20
msiexec /i .\dist\AegisWall-0.1.1-x64.msi /l*v .\install.log
```

Redact serial numbers and paths before sharing.


