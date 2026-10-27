# Changelog

All notable changes to AegisWall are documented here.

## 0.1.0 — 2026-10-27

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
