# Verification status

This page is deliberately honest about what was tested and what was not.

## Environment used for development

- Windows 11 build 26200, 64-bit.
- Non-administrator shell for normal work.
- Smart App Control **On** (state 1).
- No YubiKey attached.
- No spare malware VM.
- Unsigned EXEs are blocked by SAC; the in-memory PowerShell host is used for UI tests.

## Live-tested on this machine

| Area | Evidence |
| --- | --- |
| C# build | `build.ps1` compiles service, CLI and UI with the in-box C# 5 compiler |
| Unit tests | `tests\run-tests.ps1`: 45 checks, 0 failures |
| Crypto/config | wrap/unwrap, tamper detection, HMAC signature, recovery |
| USB key provider | DPAPI provision/unlock/recover in a temp folder |
| Audit chain | append + verify |
| WFP structs | `FwpmFilter0`, `FwpmSubLayer0`, `FwpValue0` sizes |
| Installed app scanner | real scan returned installed Start Menu/registry apps (56 on this PC) |
| UI | self-test screenshots for dashboard, apps, lockdown, key, logs, settings, test lab, discover |
| Real icons | EXE associated icons rendered in allowlist and discovery dialog |
| Portable CLI | `Run-AegisWall-Cli.ps1 status` runs via PowerShell host |
| Portable bootstrap | `Run-AegisWall-Service.ps1 -Console` starts and stays alive (WFP apply requires admin) |
| MSI build | WiX 3.14 builds compiled and portable MSIs |
| MSI layout | administrative install extracts all payload files |
| MSI install/uninstall | portable 0.1.1 installed on this PC; MSI uninstall removed the product; leftovers cleaned manually |
| CI | GitHub Actions validate + build + tests + PSScriptAnalyzer, green |

## Not live-tested end-to-end here

| Area | Why |
| --- | --- |
| Actual WFP blocking | Requires an elevated service; normal shell is non-admin and SAC blocks unsigned EXEs |
| Allow/deny for a real app | Same as above; the tester must arm on a VM |
| Trigger-based lockdown (5157) | Requires admin service + audit policy + a real blocked attempt |
| Service stop persistence | Requires installed compiled service |
| Reboot persistence | Requires a VM/reboot |
| YubiKey enrollment | No YubiKey hardware attached |
| USB removable-drive unlock | DPAPI tested in a temp folder, not a real USB drive |
| Compiled MSI service under SAC-off | Not run on this machine |
| Portable task install end-to-end | Install log showed `Core task status: Running` on this PC, but network blocking was not verified |
| Real malware containment | Not safe on the development machine; requires isolated VM |
| UI after the latest CLI-path/icon fix | Fix is built; not re-installed end-to-end on this PC after cleanup |

## What this means

The build, unit-level crypto/config/key code, real app scanner, UI rendering, MSI packaging and uninstall path are tested. The **enforcement path** (WFP filters, lockdown, service/task behavior under real admin, YubiKey, reboot) still needs the spare-laptop/VM test described in `TEST-PLAN.md` and `MALWARE-TESTING.md`.

Do not treat CI green as proof that WFP enforcement works on every PC. CI only builds and runs unit tests; it does not apply firewall filters.

## Recommended next test

1. Install the latest `AegisWall-Portable-0.1.1-x64.msi` on a VM.
2. Enroll a USB key or YubiKey.
3. Allow one browser.
4. Run Test lab → beacon test.
5. Verify the browser works, the beacon is blocked, and lockdown starts.
6. Stop the core task, run the beacon again, verify persistent filters.
7. Remove the key, verify grace and fail-closed.
8. Reboot and verify `KeyLocked`.
9. Uninstall and verify the task/service and filters are gone.

Record the results in `TEST-PLAN.md`.
