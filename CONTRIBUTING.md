# Contributing

Thanks for helping AegisWall. This project touches networking and system security, so please keep changes small, tested and reviewable.

## Development

Requirements:

- Windows 10/11 x64
- Windows PowerShell 5.1 (the in-box `csc.exe` is used)
- No .NET SDK required

Build and test:

```powershell
.\build.ps1 -Test
```

Validate scripts and assets without building:

```powershell
.\tools\validate.ps1
```

Generate UI screenshots:

```powershell
powershell -STA -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\selftest.ps1 -Page dashboard -Out .\assets\screenshot.png
```

## Pull requests

- Keep the existing C# 5 style. Do not use C# 6+ syntax; the in-box compiler rejects it.
- Do not add NuGet dependencies.
- Add or update tests in `tests\AegisWall.Tests.cs` for core behavior.
- Do not weaken the fail-closed behavior, config signing, ACLs or audit chain.
- Include screenshots for UI changes.
- Describe security impact in the PR template.

## Security-sensitive areas

Changes to `WfpEngine`, `ProtectionEngine`, `KeyVault`, `HardwareKey`, `ConfigStore` or `install.ps1` need an explicit threat-model note.

## Commit style

Use short imperative subjects, for example:

```text
fix: keep allowlist filters after service restart
feat: add USB key provider
docs: explain Smart App Control options
```

## License

By contributing, you agree that your contribution is licensed under the MIT License.
