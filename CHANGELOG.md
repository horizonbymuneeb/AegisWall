# Changelog

All notable changes to AegisWall are documented here.

## 0.1.1 - 2026-10-27

### Fixed

- Portable mode now runs the core as a SYSTEM scheduled task (`AegisWall Core`) instead of a PowerShell-hosted Windows service. This fixes MSI failures caused by SCM/service start timeouts and Smart App Control.
- The compiled MSI automatically switches to portable task mode when Smart App Control is on.
- MSI installs write `%ProgramData%\AegisWall\msi-install.log`.
- Existing services are removed with a wait loop before reinstall.
- WFP loopback/DNS filters now use the generic ALE remote-address condition (fixes FWP_E_CONDITION_NOT_FOUND).
- 5157 monitor parses namespaced XML, uses FilterOrigin=Unknown for AegisWall filters and polls the Security log reliably.
- reload writes status.json immediately, so tamper detection is visible without a 2s delay.
- Enabling test mode caps an existing lockdown to the test timer.
- Test beacon moved to %TEMP%\NetProbe so the engine does not ignore it.
- Uninstall removes the portable.mode marker.
- Portable UI uses AEGISWALL_HOME to find the CLI; app/shortcut icon added.

### Added

- Real installed application discovery from Start Menu, uninstall registry and App Paths.
- Real EXE icons in the allowlist and discovery dialog.
- Test lab with test mode, safe beacon test, checklist and JSON report.
- Malware containment test playbook.
- MSI and portable MSI installers.



## 0.1.0 - 2026-10-27

Initial MVP.

### Added

- User-mode WFP allowlist with default-deny outbound and inbound filters.
- Per-app outbound, inbound and enabled toggles.
- Trigger-based network lockdown with configurable timer and grace period.
- YubiKey HMAC-SHA1 slot 2 provider with touch support.
- USB key provider using a DPAPI machine-bound key file.
- HMAC-signed policy and recovery-code rotation.
- Hash-chained audit log with search, filter, verify and CSV export.
- Named-pipe control channel restricted to SYSTEM and Administrators.
- Service recovery, watchdog scheduled task and persistent fail-closed filters.
- Portable PowerShell-hosted mode for Smart App Control PCs.
- CLI: status, enroll, unlock, arm, disarm, lockdown, allow, update-app, remove,
  export, import, audit, diagnostics, rotate-recovery, reset, watchdog.
- WinForms light-theme UI with custom rounded inputs, dropdowns, number fields
  and audit rows.
- GitHub CI, issue templates and validation script.


