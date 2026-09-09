# tts-windows-voice-fixer

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
| `src/Launcher.Core/` | Platform-neutral voice bridge service and registry abstraction |
| `src/Launcher/` | Native .NET 10 WinForms application, Windows registry adapter, and image assets |
| `packaging/` | WiX v6 authoring for the MSI |
| `signing/` | Azure Artifact Signing (formerly Trusted Signing) account metadata |
| `.github/workflows/` | CI: build, sign, package, release |

## Why an MSI and not MSIX

MSIX was considered and rejected. Packaged MSIX apps cannot declare
`requestedExecutionLevel=requireAdministrator`, and writes to `HKLM` from inside the
package container are blocked. Both are hard requirements for this app, so the MSI is
not a stopgap — it is the only viable option unless the app stops editing machine-wide
registry state.

## Building locally

Requires the .NET 10 SDK. The Windows-targeted project can be compiled and published
from macOS or Linux with `EnableWindowsTargeting`; the resulting app runs on Windows.

```powershell
dotnet test ThoriumVoiceBridge.slnx
dotnet publish src/Launcher/Launcher.csproj -c Release -r win-x64 --self-contained true

$stage = "artifacts/stage"
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item src/Launcher/bin/Release/net10.0-windows/win-x64/publish/ThoriumVoiceBridge.exe $stage
Copy-Item src/App/KB_icon.ico, src/App/KB_logo.png $stage

dotnet tool install --global wix --version 6.*
$wixVersion = (wix --version)
wix extension add -g "WixToolset.UI.wixext/$wixVersion"

wix build packaging/Package.wxs -ext WixToolset.UI.wixext `
  -d Version=1.3.0 -d StageDir="$($PWD.Path)\artifacts\stage" `
  -arch x64 -out artifacts/ThoriumVoiceBridge-1.3.0.msi
```

The native app uses a manifest to request administrator rights before opening the
machine-wide voice registry.

## Releasing

Every push to `main` builds, signs and uploads an MSI as a workflow artifact.

To publish a release, bump `<Version>` in
[src/Launcher/Launcher.csproj](src/Launcher/Launcher.csproj), update
[CHANGELOG.md](CHANGELOG.md), merge to `main`, then run the **Build** workflow manually
with **Create a GitHub Release** enabled. The version in the csproj is the single source
of truth — it drives the exe file version, the MSI `ProductVersion`, the artifact name and
the release tag.

## Signing setup

Signing uses [`azure/artifact-signing-action`](https://github.com/Azure/artifact-signing-action)
with the account details in [signing/metadata.json](signing/metadata.json). The launcher is
signed before the MSI is built so its signature is embedded in the package; the MSI is then
signed as a whole.

### Manual Signing
For a manual Windows build using Azure CLI authentication, see
[docs/manual-windows-signing.md](docs/manual-windows-signing.md).

### Automated Workflow

Authentication is OpenID Connect, so no secret values are stored in the repository. The
following repository secrets must point at an app registration with a federated credential
for this repo and the **Certificate Profile Signer** role on the signing account:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Builds triggered by pull requests from forks cannot reach the signing service and produce an
unsigned MSI, which is fine for smoke-testing but must never be distributed.

## Testing architecture

The service operates on `IVoiceRegistry`, so tests use an in-memory implementation and
run on macOS without touching Windows. They cover token copying, language normalization,
Microsoft-voice filtering, existing-token protection, and marker-protected removal.
The real `WindowsVoiceRegistry` uses the explicit 64-bit `HKLM` registry view and is
validated by Windows CI and manual Windows smoke testing. The installer UI
(`WixUI_Minimal`) remains English while the app UI is Danish.
