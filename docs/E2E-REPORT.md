# End-to-end test report — 2026-10-27

**Build:** 0.1.1 (portable MSI, `AegisWall-Portable-0.1.1-x64.msi`)
**Host:** Windows 11 build 26200, 64-bit, Smart App Control ON
**Mode:** Portable (SYSTEM scheduled task `AegisWall Core`)
**Key:** Development file key (`devkey.bin`), test-only; no YubiKey attached
**Result:** 31 / 31 checks passed

Raw results: [`e2e-results-0.1.1.json`](e2e-results-0.1.1.json)

## Results

| Check | Result | Detail |
| --- | --- | --- |
| MSI install | PASS | exit 0 |
| Core task exists | PASS | Running |
| Core task running | PASS | Running |
| Dev key created | PASS | `%ProgramData%\AegisWall\devkey.bin` |
| Enroll | PASS | exit 0 |
| Unlock | PASS | exit 0 |
| Disarmed baseline | PASS | Disarmed |
| Baseline curl | PASS | exit 0 |
| Allow curl | PASS | exit 0 |
| Arm | PASS | exit 0 |
| Protected status | PASS | 18 WFP filters |
| Trigger monitor | PASS | available |
| Allowed app after arm | PASS | exit 0 |
| Enable test mode | PASS | exit 0 |
| Clear initial lockdown | PASS | exit 0 |
| Beacon blocked | PASS | exit 0 |
| Automatic lockdown | PASS | 14s remaining |
| Lockdown blocks allowed app | PASS | curl exit 7 |
| Clear auto lockdown | PASS | exit 0 |
| Manual trigger mode | PASS | exit 0 |
| Manual lockdown | PASS | exit 0 |
| Timer expiry | PASS | Protected |
| Task stop keeps filters | PASS | beacon exit 7 |
| Task restart | PASS | KeyLocked |
| Unlock after restart | PASS | exit 0 |
| Set grace | PASS | exit 0 |
| Key removal fail-closed | PASS | KeyLocked |
| Key restore/unlock | PASS | exit 0 |
| Config tamper reload | PASS | exit 0 |
| Config tamper detected | PASS | Unverified |
| Config restored | PASS | Protected |
| Disarm | PASS | exit 0 |
| Disarmed | PASS | Disarmed |
| MSI uninstall | PASS | exit 0 |
| Core task removed | PASS | removed |
| Install dir removed | PASS | removed |

## Bugs found and fixed during this test

1. WFP loopback/DNS filters used `FWPM_CONDITION_IP_REMOTE_ADDRESS_V4/V6`, which are invalid at ALE layers (`FWP_E_CONDITION_NOT_FOUND`). Changed to `FWPM_CONDITION_IP_REMOTE_ADDRESS`.
2. Security 5157 events do not contain `FilterName`; they use `FilterOrigin` (`Unknown` for AegisWall filters). Monitor now accepts `FilterOrigin=Unknown` and ignores Windows Firewall `Query User Default`.
3. 5157 XML uses a default namespace; `XDocument.Descendants("Data")` returned nothing. Parser now matches `LocalName == "Data"`.
4. `EventLogWatcher` was unreliable for Security events; replaced with a 1.5s `EventLogReader` polling monitor.
5. `Reload()` did not refresh `status.json`; it now calls `WriteStatus()`.
6. Test beacon path contained `AegisWallTest`, which the engine intentionally ignores; test beacon moved to `%TEMP%\NetProbe`.
7. Portable mode used a PowerShell-hosted Windows service; moved to a SYSTEM scheduled task (`AegisWall Core`) to avoid SCM/service-start failures.
8. MSI left `portable.mode` behind; uninstall now removes it.
9. Portable UI looked for the CLI in the PowerShell directory; added `AEGISWALL_HOME` and current-directory fallback.
10. App shortcut had no icon; added `assets/app.ico` and shortcut icon.

## Still not covered by this run

- YubiKey hardware enrollment (no YubiKey attached).
- Real removable USB key (DPAPI tested with a temp file).
- Compiled Windows service with Smart App Control off.
- Reboot persistence (task stop/restart was tested, not a full reboot).
- Real malware samples.
- Full UI click-through after the icon/CLI fix (UI rendering is covered by self-test screenshots).

These remain in `TEST-PLAN.md` and `MALWARE-TESTING.md` for the spare-laptop run.
