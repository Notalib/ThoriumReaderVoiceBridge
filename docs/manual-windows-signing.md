# Manual Windows installer build and signing

This runbook builds and signs the MSI from a Windows machine. It is intended for
manual release or verification builds using Azure CLI authentication for the
`dfg@kb.dk` account.

The repository stores the Artifact Signing account metadata in
[`signing/metadata.json`](../signing/metadata.json). Azure CLI provides the
authentication token, but local file signing is performed by Windows `signtool.exe`
with Microsoft's Artifact Signing DLib.

## Prerequisites

Install these on the Windows signing machine:

- Git
- Azure CLI
- .NET 10 SDK
- WiX v6 .NET tool
- Windows SDK `signtool.exe`, version `10.0.2261.755` or newer
- Microsoft Artifact Signing Client Tools, which provide `Azure.CodeSigning.Dlib.dll`

The signing user must have the **Certificate Profile Signer** role on the Artifact
Signing certificate profile.

```powershell
winget install -e --id Microsoft.DotNet.SDK.10
winget install -e --id Microsoft.AzureCLI
winget install -e --id Microsoft.Azure.ArtifactSigningClientTools

dotnet tool install --global wix --version 6.*
$wixVersion = wix --version
wix extension add -g "WixToolset.UI.wixext/$wixVersion"
```

## Check out the repository

```powershell
git clone https://github.com/Notalib/tts-windows-voice-fixer.git
cd tts-windows-voice-fixer
git pull --ff-only
```

## Sign in to Azure

```powershell
az login
az account show --query "{user:user.name, tenant:tenantId, subscription:name}" --output table
```

If Azure CLI selects the wrong tenant or subscription, set the expected context
before signing:

```powershell
az login --tenant <tenant-id-or-domain>
az account set --subscription <subscription-id-or-name>
```

## Build the launcher payload

The launcher project version is the source of truth for the executable version,
the MSI `ProductVersion`, and the output MSI filename.

```powershell
$csproj = [xml](Get-Content src/Launcher/Launcher.csproj)
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> found in src/Launcher/Launcher.csproj" }

Write-Host "Building version $version"
```

Run tests, then publish the self-contained Windows executable:

```powershell
dotnet test ThoriumVoiceBridge.slnx

dotnet publish src/Launcher/Launcher.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:Version=$version
```

Stage the files consumed by WiX:

```powershell
$stage = "artifacts/stage"
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item src/Launcher/bin/Release/net10.0-windows/win-x64/publish/ThoriumVoiceBridge.exe $stage
Copy-Item src/App/KB_icon.ico $stage
Copy-Item src/App/KB_logo.png $stage

Get-ChildItem $stage | Format-Table Name, Length
```

## Locate signing tools

```powershell
$signTool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" `
  -Recurse `
  -Filter signtool.exe `
  -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -match '\\x64\\' } |
  Sort-Object FullName -Descending |
  Select-Object -First 1 -ExpandProperty FullName

$dlib = Get-ChildItem @($env:ProgramFiles, ${env:ProgramFiles(x86)}) `
  -Recurse `
  -Filter Azure.CodeSigning.Dlib.dll `
  -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -match '\\x64\\' } |
  Select-Object -First 1 -ExpandProperty FullName

if (-not $signTool) { throw "Could not find x64 signtool.exe. Install the Windows SDK or Build Tools." }
if (-not $dlib) { throw "Could not find Azure.CodeSigning.Dlib.dll. Install Microsoft.Azure.ArtifactSigningClientTools." }

Write-Host "signtool: $signTool"
Write-Host "dlib:     $dlib"
```

Use matching architectures for `signtool.exe` and `Azure.CodeSigning.Dlib.dll`.
The commands above intentionally pick the x64 versions of both.

## Sign the launcher

The launcher must be signed before the MSI is built, so the MSI embeds the signed
executable.

```powershell
& $signTool sign `
  /v `
  /debug `
  /fd SHA256 `
  /tr "http://timestamp.acs.microsoft.com" `
  /td SHA256 `
  /dlib $dlib `
  /dmdf signing/metadata.json `
  artifacts/stage/ThoriumVoiceBridge.exe
```

## Build the MSI

```powershell
$out = "artifacts/ThoriumVoiceBridge-$version.msi"

wix build packaging/Package.wxs `
  -ext WixToolset.UI.wixext `
  -d Version=$version `
  -d StageDir="$($PWD.Path)\artifacts\stage" `
  -arch x64 `
  -out $out
```

## Sign the MSI

```powershell
& $signTool sign `
  /v `
  /debug `
  /fd SHA256 `
  /tr "http://timestamp.acs.microsoft.com" `
  /td SHA256 `
  /dlib $dlib `
  /dmdf signing/metadata.json `
  $out
```

Timestamping is required because Artifact Signing certificates are short-lived.
Keep `/tr http://timestamp.acs.microsoft.com /td SHA256` on both signing commands.

## Verify signatures

```powershell
foreach ($file in @("artifacts/stage/ThoriumVoiceBridge.exe", $out)) {
  $sig = Get-AuthenticodeSignature $file
  Write-Host "$file -> $($sig.Status) / $($sig.SignerCertificate.Subject)"
  if ($sig.Status -ne "Valid") {
    throw "Invalid signature on $file"
  }
}
```

The finished installer is written to:

```text
artifacts/ThoriumVoiceBridge-<version>.msi
```

## Troubleshooting

- `SignerSign() failed` or `403 Forbidden`: confirm `az account show` is using
  the tenant and subscription where the Artifact Signing account exists, and that
  [`signing/metadata.json`](../signing/metadata.json) uses the matching regional
  endpoint.
- `No certificates were found`: confirm the signing user has the
  **Certificate Profile Signer** role on the certificate profile, not only on the
  resource group or subscription.
- `signtool` cannot load the DLib: confirm the Windows SDK `signtool.exe` version
  is `10.0.2261.755` or newer and that `signtool.exe` and
  `Azure.CodeSigning.Dlib.dll` use the same architecture.
- The MSI installs an unsigned launcher: rebuild the MSI after signing
  `artifacts/stage/ThoriumVoiceBridge.exe`; signing the MSI afterward does not
  change the embedded executable.
