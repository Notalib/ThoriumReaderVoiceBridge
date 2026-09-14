[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$SkipTests,
    [switch]$PublishRelease
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

function Find-Executable {
    param(
        [Parameter(Mandatory)] [string]$CommandName,
        [Parameter(Mandatory)] [AllowEmptyCollection()] [string[]]$Candidates
    )

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    return $null
}

function Find-SignTool {
    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'),
        (Join-Path $env:ProgramFiles 'Windows Kits\10\bin')
    ) | Where-Object { $_ -and (Test-Path $_) }

    $tool = $roots |
        ForEach-Object { Get-ChildItem $_ -Recurse -Filter signtool.exe -File -ErrorAction SilentlyContinue } |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if ($tool) { return $tool.FullName }
    return $null
}

function Find-Dlib {
    $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA) |
        Where-Object { $_ -and (Test-Path $_) } |
        Select-Object -Unique

    $candidates = $roots |
        ForEach-Object { Get-ChildItem $_ -Recurse -Filter Azure.CodeSigning.Dlib.dll -File -ErrorAction SilentlyContinue } |
        Sort-Object FullName

    $dlib = $candidates |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Select-Object -First 1

    if (-not $dlib) { $dlib = $candidates | Select-Object -First 1 }

    if ($dlib) { return $dlib.FullName }
    return $null
}

$az = Find-Executable 'az' @(
    'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd',
    'C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd',
    (Join-Path $env:LOCALAPPDATA 'Programs\Azure CLI\wbin\az.cmd')
)
$dotnet = Find-Executable 'dotnet' @()
$gh = Find-Executable 'gh' @(
    (Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'GitHub CLI\gh.exe')
)
$signTool = Find-SignTool
$dlib = Find-Dlib

if (-not $az) { throw 'Azure CLI (az.cmd) was not found. Install it or add its wbin directory to PATH.' }
if (-not $dotnet) { throw '.NET SDK (dotnet.exe) was not found.' }
if ($PublishRelease -and -not $gh) { throw 'GitHub CLI (gh.exe) was not found. Install it with: winget install --id GitHub.cli' }
if (-not $signTool) { throw 'x64 signtool.exe was not found. Install the Windows SDK 10.0.2261.755 or newer.' }
if (-not $dlib) { throw 'x64 Azure.CodeSigning.Dlib.dll was not found. Install Microsoft Artifact Signing Client Tools.' }

$project = Join-Path $repoRoot 'src\ThoriumVoiceBridge\ThoriumVoiceBridge.csproj'
$metadataPath = Join-Path $repoRoot 'signing\metadata.json'
$csproj = [xml][IO.File]::ReadAllText($project)
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> found in $project" }
$assemblyName = $csproj.Project.PropertyGroup.AssemblyName | Where-Object { $_ } | Select-Object -First 1
if (-not $assemblyName) { throw "No <AssemblyName> found in $project" }

$metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
$executableName = "$assemblyName.exe"
$artifact = Join-Path $repoRoot "artifacts\$executableName"

Write-Host "Repository: $repoRoot"
Write-Host "Version:   $version"
Write-Host "Azure CLI: $az"
Write-Host "dotnet:    $dotnet"
if ($PublishRelease) { Write-Host "GitHub CLI: $gh" }
Write-Host "signtool:  $signTool"
Write-Host "DLib:      $dlib"
Write-Host "Endpoint:  $($metadata.Endpoint)"

if ($CheckOnly) {
    Write-Host 'Prerequisite check passed.' -ForegroundColor Green
    exit 0
}

if ($PublishRelease) {
    & $gh auth status
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI is not authenticated. Run: gh auth login' }

    $trackedChanges = & git status --porcelain --untracked-files=no
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the Git worktree status.' }
    if ($trackedChanges) { throw 'Commit tracked changes before publishing a GitHub release.' }
}

$accountJson = & $az account show --output json
if ($LASTEXITCODE -ne 0) {
    Write-Host 'No active Azure CLI login found. Starting interactive Azure login.'
    & $az login
    if ($LASTEXITCODE -ne 0) { throw 'Azure CLI login failed.' }
    $accountJson = & $az account show --output json
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the Azure CLI account after login.' }
}

$account = $accountJson | ConvertFrom-Json
Write-Host "Azure user:         $($account.user.name)"
Write-Host "Azure tenant:       $($account.tenantId)"
Write-Host "Azure subscription: $($account.name)"

$accessToken = & $az account get-access-token --resource 'https://codesigning.azure.net' --query accessToken --output tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accessToken)) {
    throw 'Could not acquire an Artifact Signing access token from Azure CLI.'
}

if (-not $SkipTests) {
    & $dotnet test (Join-Path $repoRoot 'ThoriumVoiceBridge.slnx')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed; release was not published.' }
}

& $dotnet publish $project --configuration Release --runtime win-x64 --self-contained true "-p:Version=$version"
if ($LASTEXITCODE -ne 0) { throw 'Publish failed; release was not signed.' }

$publishOutput = Join-Path $repoRoot "src\ThoriumVoiceBridge\bin\Release\net10.0-windows\win-x64\publish\$executableName"
if (-not (Test-Path $publishOutput)) { throw "Published executable was not found at $publishOutput" }

New-Item -ItemType Directory -Force -Path (Split-Path $artifact) | Out-Null
Copy-Item $publishOutput $artifact -Force

$temporaryMetadataPath = Join-Path ([IO.Path]::GetTempPath()) "ThoriumVoiceBridge-signing-$([guid]::NewGuid().ToString('N')).json"
try {
    $temporaryMetadata = [ordered]@{
        Endpoint = $metadata.Endpoint
        CodeSigningAccountName = $metadata.CodeSigningAccountName
        CertificateProfileName = $metadata.CertificateProfileName
        AccessToken = $accessToken
    } | ConvertTo-Json
    [IO.File]::WriteAllText($temporaryMetadataPath, $temporaryMetadata, [Text.UTF8Encoding]::new($false))

    & $signTool sign /v /debug /fd SHA256 /tr 'http://timestamp.acs.microsoft.com' /td SHA256 /dlib $dlib /dmdf $temporaryMetadataPath $artifact
    if ($LASTEXITCODE -ne 0) { throw 'Artifact signing failed.' }
}
finally {
    Remove-Item $temporaryMetadataPath -Force -ErrorAction SilentlyContinue
}

$signature = Get-AuthenticodeSignature $artifact
Write-Host "$artifact -> $($signature.Status) / $($signature.SignerCertificate.Subject)"
if ($signature.Status -ne 'Valid') { throw 'The executable signature is not valid.' }

Write-Host "Signed release: $artifact" -ForegroundColor Green

if ($PublishRelease) {
    $tag = "v$version"
    $repository = & $gh repo view --json nameWithOwner --jq '.nameWithOwner'
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repository)) {
        throw 'Could not determine the GitHub repository from the origin remote.'
    }

    $headCommit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the current Git commit.' }

    # gh writes "release not found" to stderr, which $ErrorActionPreference = 'Stop' would turn into a terminating error.
    & { $ErrorActionPreference = 'Continue'; & $gh release view $tag --repo $repository 2>&1 | Out-Null }
    if ($LASTEXITCODE -eq 0) {
        $releaseCommit = & $gh api "repos/$repository/commits/$tag" --jq '.sha'
        if ($LASTEXITCODE -ne 0) { throw "Could not resolve release tag $tag." }
        if ($releaseCommit -ne $headCommit) {
            throw "Release $tag points to $releaseCommit, but the signed build is from $headCommit."
        }

        & $gh release upload $tag $artifact --repo $repository --clobber
        if ($LASTEXITCODE -ne 0) { throw "Could not upload $executableName to release $tag." }
    }
    else {
        & $gh release create $tag $artifact --repo $repository --target $headCommit --title $tag --generate-notes
        if ($LASTEXITCODE -ne 0) { throw "Could not create release $tag. Confirm the commit has been pushed." }
    }

    Write-Host "Published GitHub release: https://github.com/$repository/releases/tag/$tag" -ForegroundColor Green
}