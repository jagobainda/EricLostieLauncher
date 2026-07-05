<#
.SYNOPSIS
    Builds and packages LostieLauncher for release using Velopack.

.DESCRIPTION
    1. Reads the version from the .csproj
    2. Publishes a self-contained win-x64 build
    3. Packages it with `vpk pack` (Velopack signs its own binaries + the app)
    4. Optionally uploads the releases/ folder to the server via SCP

.PARAMETER Sign
    If set, signs all binaries (app + Velopack internals) with the Certum smart card.
    Requires the ACS ACR39U reader connected with the Certum card inserted.

.PARAMETER CertThumbprint
    SHA1 thumbprint of the code-signing certificate. Required when -Sign is set.

.PARAMETER Upload
    If set, uploads the output to the configured remote server.

.PARAMETER SshHost
    SSH host for upload (e.g. user@yourserver.com). Required when -Upload is set.

.PARAMETER SshPath
    Remote path on the server (e.g. /var/www/ericlostie-launcher/public/installer/).
    Required when -Upload is set.

.EXAMPLE
    .\scripts\build-release.ps1
    .\scripts\build-release.ps1 -Sign -CertThumbprint "20ed2e50..."
    .\scripts\build-release.ps1 -Sign -CertThumbprint "20ed2e50..." -Upload -SshHost "user@jagoba.dev" -SshPath "/var/www/installer/"
#>

param(
    [switch]$Sign,
    [switch]$Upload,
    [string]$SshHost = "",
    [string]$SshPath = "/var/www/ericlostie-launcher/public/installer/",
    [string]$CertThumbprint = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Config ────────────────────────────────────────────────────────────────────
$TimestampUrl   = "http://time.certum.pl"
$SigntoolExe    = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"

# ── Paths ─────────────────────────────────────────────────────────────────────
$RepoRoot    = Split-Path $PSScriptRoot -Parent
$ProjectFile = Join-Path $RepoRoot "LostieLauncher\LostieLauncher.csproj"
$PublishDir  = Join-Path $RepoRoot "publish"
$ReleasesDir = Join-Path $RepoRoot "releases"
$IconFile    = Join-Path $RepoRoot "LostieLauncher\Assets\app.ico"

# ── Read version from .csproj ─────────────────────────────────────────────────
[xml]$csproj = Get-Content $ProjectFile
$Version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$Version = ($Version -split "\.")[0..2] -join "."

if (-not $Version) {
    Write-Error "Could not read <Version> from $ProjectFile"
    exit 1
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  LostieLauncher — Velopack Release Build" -ForegroundColor Cyan
Write-Host "  Version : $Version" -ForegroundColor Cyan
if ($Sign) {
    Write-Host "  Signing : Enabled (Certum smart card)" -ForegroundColor Cyan
    Write-Host "  Thumbprint: $CertThumbprint" -ForegroundColor Cyan
}
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# ── Validate signtool if signing ──────────────────────────────────────────────
if ($Sign) {
    if (-not $CertThumbprint) {
        Write-Error "-CertThumbprint is required when using -Sign."
        exit 1
    }
    if (-not (Test-Path $SigntoolExe)) {
        Write-Error "signtool.exe not found at: $SigntoolExe`nInstall Windows SDK or update the path in this script."
        exit 1
    }
}

# ── Clean ─────────────────────────────────────────────────────────────────────
Write-Host "[1/3] Cleaning previous output..." -ForegroundColor Yellow
Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue

# ── Publish ───────────────────────────────────────────────────────────────────
Write-Host "[2/3] Publishing (win-x64, self-contained)..." -ForegroundColor Yellow
dotnet publish $ProjectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $PublishDir `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed."; exit 1 }

# ── Pack with vpk (+ signing) ─────────────────────────────────────────────────
# NOTE: Velopack must handle ALL signing itself because it signs its own
# internal binaries (Setup.exe, Update.exe) during the pack process.
# Signing only the app .exe beforehand is NOT sufficient.
Write-Host "[3/3] Packaging with vpk..." -ForegroundColor Yellow

$vpkArgs = @(
    "pack"
    "--packId",      "LostieLauncher"
    "--packVersion", $Version
    "--packDir",     $PublishDir
    "--mainExe",     "LostieLauncher.exe"
    "--packTitle",   "Lostie Launcher"
    "--icon",        $IconFile
    "--outputDir",   $ReleasesDir
)

if ($Sign) {
    Write-Host "Registrando clave de tarjeta inteligente..." -ForegroundColor Yellow
    certutil -repairstore -user MY $CertThumbprint "C:\Users\Jagoba\keyinfo.inf"
    if ($LASTEXITCODE -ne 0) { Write-Error "certutil -repairstore falló."; exit 1 }

    $vpkArgs += @(
        "--signTemplate", "`"$SigntoolExe`" sign /sha1 $CertThumbprint /tr $TimestampUrl /td sha256 /fd sha256 {{file}}"
    )
}

& vpk @vpkArgs

if ($LASTEXITCODE -ne 0) { Write-Error "vpk pack failed."; exit 1 }

Write-Host ""
Write-Host "✅ Package ready in: $ReleasesDir" -ForegroundColor Green
Write-Host ""
Get-ChildItem $ReleasesDir | Format-Table Name, Length -AutoSize

# ── Optional upload ───────────────────────────────────────────────────────────
if ($Upload) {
    if (-not $SshHost) { Write-Error "-SshHost is required when using -Upload."; exit 1 }

    Write-Host "Uploading to ${SshHost}:${SshPath} ..." -ForegroundColor Yellow
    scp -r "$ReleasesDir\*" "${SshHost}:${SshPath}"

    if ($LASTEXITCODE -ne 0) { Write-Error "Upload via SCP failed."; exit 1 }

    Write-Host "✅ Upload complete." -ForegroundColor Green
}