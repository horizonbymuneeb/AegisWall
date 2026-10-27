# Threat model

## Goal

Keep unapproved applications off the network and fail closed when the hardware anchor is missing or the policy is tampered with.

## Assets

- The WFP policy (allowlist, default-deny, lockdown filters).
- The signed `config.json` and the vault key.
- The hardware key (YubiKey HMAC secret or USB DPAPI key file).
- The audit chain.
- The service and watchdog.

## Adversaries

1. **Unprivileged malware** — runs as the logged-in user, no admin.
2. **Admin-level malware** — has Administrator or SYSTEM.
3. **Physical attacker** — has the PC but not the hardware key.
4. **Careless user** — removes the USB key, stops the service, edits config.

## What AegisWall stops

- Unprivileged processes cannot change WFP filters, stop the service, edit `%ProgramData%` config, or call the admin-only control pipe.
- Unapproved outbound and inbound connections are blocked at the WFP ALE layers while armed.
- Blocked attempts can trigger a timed full-network lockdown.
- Removing the hardware key starts a grace period, then fail-closed.
- A modified config fails the HMAC check and the service applies default-deny instead of the modified allowlist.
- Persistent filters remain if the service crashes or is stopped while armed.
- The audit log is hash-chained, so silent edits are detectable.

## What AegisWall does not stop

- **A local administrator or SYSTEM** can stop the service, delete WFP filters, replace binaries, take ownership of files, or load a vulnerable signed driver. AegisWall is user-mode WFP; it is not a hypervisor or a Microsoft-signed ELAM/PPL component.
- **A physical attacker with the hardware key** can unlock the vault. Physical possession is the root of trust.
- **A copied USB key file** is machine-bound with DPAPI, but an admin on the same PC can decrypt it. The YubiKey is stronger because the HMAC secret never leaves the key.
- **DNS tunneling** remains possible through the permitted system resolver. Pinned DNS mode narrows the destination but does not inspect DNS payloads.
- **Kernel-mode malware** can bypass user-mode WFP management.
- **Boot-time attacks** before the service starts are not covered. Persistent filters are restored by BFE, but a malicious bootloader/kernel is out of scope.

## Trust boundaries

- UI runs unprivileged; it only reads status/config and launches elevated CLI actions.
- The control named pipe allows only SYSTEM and Administrators.
- The service runs as LocalSystem and owns the WFP engine.
- `%ProgramData%\AegisWall` is ACL-restricted; config is HMAC-signed.
- The hardware key unlocks the vault key; the vault key signs the policy.

## Residual risks

- System DNS via `svchost.exe`.
- Path-based allowlisting: a replaced binary at the same path is allowed. File hashes are recorded, but runtime hash enforcement needs a kernel driver.
- Automatic trigger detection depends on the Filtering Platform audit policy.
- User-mode service can be disabled by an administrator.
- USB key file is only as strong as DPAPI machine scope.

## Hardening roadmap

1. Signed WFP callout driver with image hash and signer checks.
2. UEFI-locked WDAC policy that blocks tampering tools and enforces the AegisWall binaries.
3. Protected Process Light / ELAM signing (requires Microsoft approval).
4. Secure Boot + BitLocker + TPM measured boot guidance.
5. Backup hardware key and threshold recovery.
