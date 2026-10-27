# Smart App Control (SAC) and AegisWall

AegisWall is open source and, by default, the downloaded EXEs are **not code-signed**. Windows 11 **Smart App Control** blocks unsigned or untrusted executables. That is a Windows security decision, not an AegisWall bug. There is no per-app "allow" button inside Smart App Control.

This page explains the supported options, the trade-offs, and the exact steps for users and maintainers.

## Check whether SAC is on

Windows Security:

1. Open **Windows Security**.
2. Go to **App & browser control**.
3. Look at **Smart App Control**.
4. The state is **On**, **Evaluation**, or **Off**.

PowerShell:

```powershell
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' `
  -Name VerifiedAndReputablePolicyState
```

Typical values:

| Value | Meaning |
| --- | --- |
| `0` | Off |
| `1` | On / enforcing |
| `2` | Evaluation |

The AegisWall installer reads this value and warns before installing unsigned EXEs.

## Option 1 - Portable mode (works with SAC on)

Portable mode runs the UI, CLI, and service through the **signed PowerShell host** instead of loading the unsigned AegisWall EXEs. Smart App Control does not block the signed PowerShell host.

Install:

```powershell
.\install.ps1 -Portable
```

Launch the UI:

```text
C:\Program Files\AegisWall\AegisWall-Portable.bat
```

Elevated CLI:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File "C:\Program Files\AegisWall\Run-AegisWall-Cli.ps1" status
```

If Smart App Control blocks the MSI itself, use the ZIP release and run `AegisWall-Portable.bat` (or `Run-AegisWall.ps1`) directly. The launcher uses the signed PowerShell host.

The portable service is installed as:

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "...\Run-AegisWall-Service.ps1"
```

Trade-offs:

- The first UI/CLI launch compiles the C# sources in memory and takes a few seconds.
- The service also compiles at start-up. Check `%ProgramData%\AegisWall\service-bootstrap.log` if it fails.
- Some enterprise WDAC policies block unsigned scripts even when the host is signed. In that case use a signed release.

## Option 2 - Code-signed release (recommended for distribution)

Sign the EXEs with a trusted code-signing certificate and build reputation. Smart App Control trusts signed code with good reputation; a brand-new certificate may still be blocked until reputation accumulates.

Recommended for open source:

- **SignPath Foundation** - free code signing for eligible OSS projects.
- **Azure Trusted Signing** - Microsoft-managed signing for organizations.
- A traditional OV/EV code-signing certificate.

Sign the following files:

```text
AegisWall.UI.exe
AegisWall.Cli.exe
AegisWall.Service.exe
```

Verify:

```powershell
Get-AuthenticodeSignature .\AegisWall.UI.exe | Format-List Status, SignerCertificate
```

For release automation, keep signing secrets in GitHub Actions secrets and sign only after the build and tests pass. Never commit a `.pfx` or its password.

## Option 3 - Turn Smart App Control off (security trade-off)

Microsoft's supported UI:

1. **Windows Security** -> **App & browser control** -> **Smart App Control**.
2. Click **Off**.
3. Confirm.

Important:

- Microsoft warns that turning SAC off is a security downgrade.
- Re-enabling may require a Windows reset or reinstall, depending on the Windows build. Recent updates allow re-enabling from the UI on some builds, but do not rely on it.
- Do **not** flip `VerifiedAndReputablePolicyState` in the registry as a shortcut. It can leave Windows in an inconsistent state and block many apps.
- After changing SAC, reboot and re-run the AegisWall installer.

## Option 4 - Enterprise WDAC allow policy

If the PC is managed by WDAC/App Control for Business, ask the administrator to allow AegisWall by **publisher** (preferred) or by **file hash** (brittle across updates).

A minimal allow rule for a signed build:

```xml
<FileRules>
  <Allow Id="ID_ALLOW_AEGISWALL" FileName="AegisWall.UI.exe" />
</FileRules>
<Signers>
  <Signer Name="AegisWall Publisher" ID="ID_SIGNER_AEGISWALL">
    <CertRoot Type="TBS" Value="..." />
  </Signer>
</Signers>
```

Hash-based rules must be regenerated for every release:

```powershell
Get-FileHash .\AegisWall.UI.exe -Algorithm SHA256
```

## Self-signed certificate for local testing

Self-signed certificates are fine for local development, but Smart App Control will usually still block them because they are not trusted by Microsoft's reputation service.

```powershell
$cert = New-SelfSignedCertificate `
  -Type CodeSigningCert `
  -Subject "CN=AegisWall Dev" `
  -CertStoreLocation Cert:\CurrentUser\My `
  -KeyUsage DigitalSignature `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

Export-Certificate -Cert $cert -FilePath .\AegisWall-Dev.cer
Import-Certificate -FilePath .\AegisWall-Dev.cer -CertStoreLocation Cert:\LocalMachine\Root
Import-Certificate -FilePath .\AegisWall-Dev.cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher

signtool sign /fd SHA256 /a /n "AegisWall Dev" .\AegisWall.UI.exe
```

To remove the test trust later:

```powershell
Get-ChildItem Cert:\LocalMachine\Root, Cert:\LocalMachine\TrustedPublisher |
  Where-Object Subject -eq 'CN=AegisWall Dev' |
  Remove-Item
```

## What AegisWall does not do

- It does not silently disable Smart App Control.
- It does not add Defender exclusions (those do not bypass SAC).
- It does not promise that an unsigned build will run on every Windows 11 PC.

If SAC blocks the installer or the EXEs, use **portable mode** or a **signed release**.

