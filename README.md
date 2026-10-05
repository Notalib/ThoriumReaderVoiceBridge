# Thorium Voice Bridge

Windows-værktøj der kopierer installerede SAPI 5-oplæsningsstemmer over i Windows'
OneCore-stemmeregister, så de kan bruges i **Thorium Reader**. Stemmer der er tilføjet
af værktøjet kan også fjernes igen.

Skrevet af Joshua Lindemann for Det Kgl. Biblioteks Nota-service.
Brugerfladen er på dansk; kode, filnavne og dokumentation er på engelsk.

## How it works

Thorium Reader reads voices from `HKLM\SOFTWARE\Microsoft\Speech_OneCore\Voices\Tokens`,
while most third-party voices register themselves under the older SAPI 5 hive at
`HKLM\SOFTWARE\Microsoft\Speech\Voices\Tokens`. The app mirrors selected SAPI tokens into
the OneCore hive and tags each copy with a `ThoriumBridge` marker value, so it only ever
removes entries it created itself. Microsoft's own voices are skipped.

Because it writes to `HKLM`, the app requires administrator rights.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/ThoriumVoiceBridge.Core/` | Platform-neutral voice bridge service and registry abstraction |
| `src/ThoriumVoiceBridge/` | Native .NET 10 WinForms application, Windows registry adapter, and image assets |
| `signing/` | Azure Artifact Signing (formerly Trusted Signing) account metadata |
| `.github/workflows/` | CI: build, sign, and release the executable |

## Distribution

The release is a single self-contained, signed `Tilføj stemmer til Thorium.exe` for 64-bit
Windows. It has no .NET or PowerShell dependency. Windows asks for administrator
permission when it starts, because the app writes to `HKLM`.

## Building locally

Requires the .NET 10 SDK. The Windows-targeted project can be compiled and published
from macOS or Linux with `EnableWindowsTargeting`; the resulting app runs on Windows.

```powershell
dotnet test ThoriumVoiceBridge.slnx
dotnet publish src/ThoriumVoiceBridge/ThoriumVoiceBridge.csproj -c Release -r win-x64 --self-contained true
```

The native app uses a manifest to request administrator rights before opening the
machine-wide voice registry.

## Releasing

Every push to `main` builds, signs and uploads the executable as a workflow artifact.

To publish a release, bump `<Version>` in
[src/ThoriumVoiceBridge/ThoriumVoiceBridge.csproj](src/ThoriumVoiceBridge/ThoriumVoiceBridge.csproj), update
[CHANGELOG.md](CHANGELOG.md), merge to `main`, then run the **Build** workflow manually
with **Create a GitHub Release** enabled. The version in the csproj is the single source
of truth — it drives the EXE file version, artifact name, and release tag.

## Signing setup

Signing uses [`azure/artifact-signing-action`](https://github.com/Azure/artifact-signing-action)
with the account details in [signing/metadata.json](signing/metadata.json).

### Manual Signing
For a manual Windows build using Azure CLI authentication, see
[docs/manual-windows-signing.md](docs/manual-windows-signing.md).

After the prerequisites are installed and `az login` has been completed, run the
release build, signing, and signature verification with:

```powershell
.\tools\Sign-WindowsRelease.ps1
```

Use `.\tools\Sign-WindowsRelease.ps1 -CheckOnly` to validate the local signing
tools without building or signing anything. The signed executable is written to
`artifacts/Tilføj stemmer til Thorium.exe`.

To create the matching `v<version>` GitHub release and upload the signed executable,
install and authenticate GitHub CLI, commit and push the release changes, then run:

```powershell
winget install --id GitHub.cli
gh auth login
.\tools\Sign-WindowsRelease.ps1 -PublishRelease
```

If the release already exists at the current commit, its executable asset is replaced.

### Automated Workflow

Authentication is OpenID Connect, so no secret values are stored in the repository. The
following repository secrets must point at an app registration with a federated credential
for this repo and the **Certificate Profile Signer** role on the signing account:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Builds triggered by pull requests from forks cannot reach the signing service and produce an
unsigned executable, which is fine for smoke-testing but must never be distributed.

## Testing architecture

The service operates on `IVoiceRegistry`, so tests use an in-memory implementation and
run on macOS without touching Windows. They cover token copying, language normalization,
Microsoft-voice filtering, existing-token protection, and marker-protected removal.
The real `WindowsVoiceRegistry` uses the explicit 64-bit `HKLM` registry view and is
validated by Windows CI and manual Windows smoke testing.

## UI preview

The WinForms app only runs on Windows, but the static preview at
[tools/ui-preview/index.html](tools/ui-preview/index.html) mirrors the fixed-size form layout and
loads the same KB icon and logo assets. Open it in a browser on macOS for quick visual checks
before doing the final Windows smoke test.

## License

[MIT](LICENSE) © Det Kgl. Bibliotek
