# Security policy

## Reporting a vulnerability

Do not open a public issue for a vulnerability that can weaken enforcement, bypass the allowlist, recover the vault key, or remove filters without authorization.

Report it privately to the maintainer:

- GitHub: **@horizonbymuneeb**
- Use GitHub Security Advisories: `Security` → `Report a vulnerability`

Include:

- Windows version and build
- AegisWall version
- Whether SAC/WDAC is enabled
- Hardware key type (YubiKey/USB/none)
- Reproduction steps and expected/actual behavior

## Scope

In scope:

- WFP filter bypass
- Config signature bypass
- Vault/recovery key disclosure
- Service/pipe privilege escalation
- Audit-chain forgery
- Installer ACL weaknesses

Out of scope:

- An administrator/SYSTEM user deleting filters (documented limitation)
- Kernel-mode malware
- Physical access with the hardware key
- Smart App Control blocking unsigned builds (see `docs/SMART-APP-CONTROL.md`)

## Supported versions

Only the latest release receives security fixes.
