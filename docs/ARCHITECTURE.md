# Architecture

AegisWall is a user-mode control plane over **Windows Filtering Platform (WFP)**. The actual packet classification happens inside the Windows kernel (BFE/WFP). AegisWall does **not** ship a custom `.sys` driver in 0.1.0.

## System context

```mermaid
flowchart LR
    User[User] --> UI[AegisWall UI<br/>WinForms]
    Admin[Administrator] --> CLI[AegisWall CLI]
    UI -->|runas| CLI
    CLI -->|named pipe<br/>SYSTEM/Administrators| SVC[AegisWall Service<br/>LocalSystem]
    SVC --> WFP[Windows Filtering Platform<br/>kernel classification]
    SVC --> CFG[%ProgramData%\AegisWall\config.json<br/>HMAC-signed]
    SVC --> AUD[audit.jsonl<br/>SHA-256 chain]
    SVC --> STATUS[status.json<br/>read-only for UI]
    SVC --> KEY[YubiKey HMAC<br/>or USB DPAPI key file]
    TASK[Watchdog task<br/>SYSTEM, every minute] --> SVC
    MSI[MSI installer] --> SVC
```

## Components

```mermaid
flowchart TB
    subgraph UI[AegisWall.UI]
      Dash[Dashboard]
      Apps[Applications]
      Lock[Lockdown]
      Key[Hardware key]
      Logs[Audit log]
      Settings[Settings]
    end

    subgraph CLI[AegisWall.Cli]
      Cmd[status / enroll / arm / allow / lockdown / audit / diagnostics]
    end

    subgraph SVC[AegisWall.Service]
      Engine[ProtectionEngine]
      IPC[IpcServer<br/>admin-only pipe]
      Monitor[BlockEventMonitor<br/>Security 5157]
      Vault[KeyVault]
      Signer[ConfigSigner]
      Audit[AuditLog]
    end

    subgraph CORE[AegisWall.Core]
      Wfp[WfpEngine<br/>FwpmEngineOpen0 / FwpmFilterAdd0]
      Crypto[Crypto / Base32]
      Store[ConfigStore / StatusStore]
      Hw[YubiKeyCli / UsbKeyProvider]
    end

    UI --> CLI
    CLI --> IPC
    IPC --> Engine
    Engine --> Wfp
    Engine --> Vault
    Engine --> Signer
    Engine --> Audit
    Engine --> Monitor
    Vault --> Hw
    Engine --> Store
    Wfp --> BFE[BFE / WFP kernel]
```

## Data flow

```mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant UI as UI
    participant CLI as CLI (elevated)
    participant S as Service (SYSTEM)
    participant K as Hardware key
    participant W as WFP/BFE
    participant A as Audit log

    U->>UI: Add app / arm
    UI->>CLI: runas command
    CLI->>S: named pipe request
    S->>K: challenge-response (if unlocking)
    K-->>S: HMAC response
    S->>S: unwrap vault key, verify config HMAC
    S->>W: FwpmTransactionBegin / FilterAdd / Commit
    W-->>S: filters active
    S->>A: append hash-chained event
    S-->>CLI: status snapshot
    CLI-->>UI: status.json
```

## Install sequence (MSI)

```mermaid
sequenceDiagram
    autonumber
    participant M as MSI
    participant PS as PowerShell (deferred, SYSTEM)
    participant SCM as Service Control Manager
    participant W as WFP/BFE
    participant T as Task Scheduler

    M->>M: InstallFiles to Program Files\AegisWall
    M->>PS: install.ps1 -ServiceOnly -Force -InstallDir ...
    PS->>PS: create %ProgramData%\AegisWall + ACLs
    PS->>SCM: New-Service AegisWall (EXE or PowerShell host)
    PS->>SCM: recovery actions + service DACL
    PS->>T: register AegisWall Watchdog (SYSTEM, every minute)
    PS->>PS: auditpol Filtering Platform Connection failure
    PS->>SCM: Start-Service
    SCM->>W: service starts, applies persistent filters
    M-->>M: success (or rollback on failure)
```

## Enrollment sequence

```mermaid
sequenceDiagram
    autonumber
    participant U as User
    participant C as CLI
    participant K as YubiKey/USB
    participant V as KeyVault
    participant S as Service

    U->>C: enroll --usb / --yubikey
    C->>K: provision random key / slot 2 secret
    C->>K: HMAC(challenge)
    K-->>C: response
    C->>V: derive KEK, wrap vault key
    V-->>C: vault key + recovery code
    C->>C: sign config with vault key
    C->>C: print recovery code once
    C->>S: reload
    S->>S: fail-closed until unlocked
```

## Blocked attempt -> lockdown

```mermaid
sequenceDiagram
    autonumber
    participant P as Unapproved process
    participant W as WFP
    participant E as Security log
    participant M as BlockEventMonitor
    participant S as ProtectionEngine
    participant A as Audit

    P->>W: connect()
    W-->>P: blocked (persistent deny filter)
    W->>E: event 5157 (Filtering Platform blocked)
    E-->>M: EntryWritten
    M->>S: OnBlockedConnection(app, pid)
    S->>S: debounce 60s, counters++
    S->>S: set lockdownUntil = now + minutes
    S->>W: add higher-weight block filters (4 layers)
    S->>A: lockdown.trigger
    Note over S: countdown; loopback/DHCP remain allowed
    S->>W: remove lockdown filters at expiry
    S->>A: lockdown.expired
```

## Key removal grace

```mermaid
sequenceDiagram
    autonumber
    participant K as Hardware key
    participant P as PollKey timer
    participant S as Service
    participant W as WFP
    participant A as Audit

    K-->>P: absent
    P->>S: start grace (default 30s), keep current policy
    alt key returns
        K-->>P: present
        P->>S: cancel grace
        S->>A: key.returned
    else grace expires
        P->>S: clear vault key
        S->>W: default-deny (and optional lockdown timer)
        S->>A: key.removed
    end
```

## Uninstall sequence

```mermaid
sequenceDiagram
    autonumber
    participant M as MSI
    participant PS as uninstall.ps1 (SYSTEM)
    participant S as Service
    participant W as WFP
    participant T as Task Scheduler

    M->>PS: uninstall.ps1 -ServiceOnly -Force
    PS->>S: Stop-Service
    PS->>W: CLI reset --force (delete filters/sublayer)
    PS->>T: unregister watchdog
    PS->>S: sc delete AegisWall
    M->>M: RemoveFiles
```

## Kernel and registry details

### Kernel

- **No custom kernel driver.** AegisWall calls the documented WFP user-mode API (`fwpuclnt.dll`):
  - `FwpmEngineOpen0`
  - `FwpmSubLayerAdd0`
  - `FwpmFilterAdd0`
  - `FwpmTransactionBegin0` / `Commit0`
  - `FwpmFilterDeleteByKey0`
  - `FwpmGetAppIdFromFileName0`
- The **Base Filtering Engine (BFE)** and WFP classify packets in kernel mode.
- Filters are installed at:
  - `FWPM_LAYER_ALE_AUTH_CONNECT_V4/V6`
  - `FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4/V6`
- Persistent filters are stored by BFE and restored across BFE restarts.
- A future release may add a signed WFP callout driver for runtime hash/signature enforcement. That is **not** present in 0.1.0.

### Registry

AegisWall does not create custom kernel registry keys or hooks. It touches:

| Location | Purpose |
| --- | --- |
| `HKLM\SYSTEM\CurrentControlSet\Services\AegisWall` | Service registration created by SCM |
| `HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy\VerifiedAndReputablePolicyState` | Read-only SAC detection |
| WFP persistent store (BFE) | Persistent filters/sublayer |
| Task Scheduler store | `AegisWall Watchdog` task |
| `HKLM\Software\AegisWall` | MSI shortcut key path |

The app does **not** patch `netsh`, Windows Firewall rules, LSPs, NDIS, or kernel callbacks.

### Files

| Path | Purpose |
| --- | --- |
| `%ProgramData%\AegisWall\config.json` | Signed policy, allowlist, key vault blobs |
| `%ProgramData%\AegisWall\status.json` | Read-only UI status |
| `%ProgramData%\AegisWall\audit.jsonl` | Hash-chained audit events |
| `%ProgramData%\AegisWall\service-bootstrap.log` | Portable service compile/start errors |
| `C:\Program Files\AegisWall` | Binaries, source (portable), docs, scripts |

## Security invariants

1. While armed, a default-deny block filter exists at all four ALE layers.
2. The allowlist is only applied if the config HMAC verifies with the vault key.
3. A missing key or invalid signature never opens the network.
4. The control pipe is restricted to SYSTEM and Administrators.
5. Every decision is appended to a hash chain.
6. The UI never writes config directly; it launches an elevated CLI action.
7. Persistent filters remain if the service crashes.

## Known limits

- Admin/SYSTEM can remove filters; see `THREAT-MODEL.md`.
- No custom kernel driver; no runtime hash enforcement yet.
- USB key uses DPAPI machine scope.
- System DNS is allowed through `svchost.exe`; pinned mode narrows the destination.
- Automatic trigger detection needs the Filtering Platform audit policy.
