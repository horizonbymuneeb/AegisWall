# Test lab

The Test lab is for the "I will install it on a spare laptop and test it" workflow. It gives you safe, repeatable containment tests without weakening enforcement.

Test mode **does not** disable WFP filters or open the network. It only:

- shortens the punishment timer to a configurable number of seconds (15-3600, default 60);
- prefixes audit events with `[TEST]`;
- keeps the same default-deny, allowlist and fail-closed behavior.

## Open it

UI → **Test lab**.

The checklist shows:

- Unapproved beacon blocked
- Lockdown triggered
- Service stop keeps filters (manual)
- Key removal grace (manual)
- Reboot persistence (manual)
- Config tamper detection (manual)

## Beacon test

The beacon is a temporary copy of `curl.exe`:

```text
%TEMP%\AegisWallTest\beacon.exe
```

Because it is not on the allowlist, WFP blocks its outbound connection. The CLI compares `BlockedAttempts` before/after and checks for `Lockdown`.

UI: **Run beacon test**.

CLI:

```powershell
AegisWall.Cli.exe test-mode on
AegisWall.Cli.exe test-beacon
AegisWall.Cli.exe test-report C:\Temp\aegiswall-report.json
AegisWall.Cli.exe test-mode off
```

Portable:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File "C:\Program Files\AegisWall\Run-AegisWall-Cli.ps1" test-beacon
```

Expected:

- Beacon exits non-zero.
- `BlockedAttempts` increases.
- Audit contains `test.beacon`.
- If trigger mode allows it, `[TEST]` lockdown starts for the configured test seconds.
- The beacon path is under `%TEMP%`, not `System32`, so `User apps only` mode does not exclude it.

If the beacon connects, check:

- Is `C:\Temp\AegisWallTest\beacon.exe` on the allowlist?
- Is the service armed?
- Did the service fail to apply filters? Run `AegisWall.Cli.exe diagnostics`.

## Manual tests

### Service stop

```powershell
Stop-Service AegisWall
C:\Temp\AegisWallTest\beacon.exe https://example.com
Start-Service AegisWall
```

Expected: beacon remains blocked; watchdog restarts the service within a minute.

### Key removal

Remove the YubiKey/USB key, then reinsert within the grace period. Expected: `Key grace`, no lockdown. Leave it out past the grace period: expected `KeyLocked`/`Lockdown`, no external traffic.

### Reboot

Reboot without the key. Expected: persistent filters, `KeyLocked`, no allowlisted traffic until unlock.

### Config tamper

On a VM copy, edit `%ProgramData%\AegisWall\config.json` (for example add an app), then reload. Expected: `Unverified`, default-deny. Do not do this on a production machine.

## Test report

The report includes:

- status snapshot
- diagnostics (WFP filter count, monitor, audit chain, key state)
- last 200 audit entries

UI: **Export report**. CLI: `test-report <file>`.

## Safety

- Use a VM/spare laptop with a snapshot.
- No shared folders or host drives.
- Real malware only in an isolated VM; AegisWall is not antivirus.
- Test mode still blocks the network; do not expect it to be a "no enforcement" sandbox.
- Record known gaps: admin filter deletion, injection into an allowed app, DNS tunneling, path replacement, ransomware file encryption.
