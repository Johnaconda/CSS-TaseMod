# One-step build & deploy for CSS-TaseMod
# - No $env:$Name (uses safe helpers)
# - Auto-detects CS2 server & CounterStrikeSharp API
# - Builds Release and copies plugin DLL to server plugins folder
# - Optionally deploys Panorama UI if client path provided/found

[CmdletBinding()]
param(
  [switch]$DefineClientCmd  # adds TASE_HAS_CLIENTCMD to this build only
)

$ErrorActionPreference = "Stop"

# ---------------------------
# Env helpers (fixed)
# ---------------------------
$script:EnvCache = @{}

function Get-EnvCached([string]$Name, [string]$Fallback = "") {
  if ($script:EnvCache.ContainsKey($Name)) { return $script:EnvCache[$Name] }
  $val = [System.Environment]::GetEnvironmentVariable($Name, 'Process')
  if ([string]::IsNullOrWhiteSpace($val)) { $val = [System.Environment]::GetEnvironmentVariable($Name, 'User') }
  if ([string]::IsNullOrWhiteSpace($val)) { $val = [System.Environment]::GetEnvironmentVariable($Name, 'Machine') }
  if ([string]::IsNullOrWhiteSpace($val)) { $val = $Fallback }
  $script:EnvCache[$Name] = $val
  return $val
}

function Set-Env([string]$Name, [string]$Value) {
  [System.Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
  try { Set-Item -Path ("Env:{0}" -f $Name) -Value $Value -ErrorAction SilentlyContinue } catch {}
  $script:EnvCache[$Name] = $Value
}

# ---------------------------
# Paths & detection
# ---------------------------
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $RepoRoot

Write-Host "[TASE] Repo: $RepoRoot"

# Try env first
$serverRoot = Get-EnvCached "CS2_SERVER_ROOT"
$cssharpDir = Get-EnvCached "CSSHARP_DIR"
$clientRoot = Get-EnvCached "CS2_CLIENT_ROOT"
$tasePanoRoot = Get-EnvCached "TASE_PANO_ROOT"

# Auto-detect server if missing
if ([string]::IsNullOrWhiteSpace($serverRoot)) {
  $candidates = @(
    "C:\cs2-ds",
    "D:\cs2-ds",
    "$HOME\cs2-ds",
    "$HOME\steamcmd\cs2-ds"
  )
  $serverRoot = $null
  foreach ($p in $candidates) {
    $g = Join-Path $p "game\csgo"
    if (Test-Path $g) { $serverRoot = $p; break }
  }
  if (-not $serverRoot) {
    $serverRoot = Read-Host "Enter CS2 Server path (folder that contains 'game\csgo')"
  }
  if (-not (Test-Path (Join-Path $serverRoot "game\csgo"))) {
    throw "[TASE] Invalid CS2 server path: $serverRoot (missing game\csgo)"
  }
  Set-Env "CS2_SERVER_ROOT" $serverRoot
}
Write-Host "[TASE] CS2_SERVER_ROOT = $serverRoot"

# Auto-detect CounterStrikeSharp API if missing
if ([string]::IsNullOrWhiteSpace($cssharpDir)) {
  $try1 = Join-Path $serverRoot "game\csgo\addons\counterstrikesharp\api"
  $try2 = Join-Path $serverRoot "addons\counterstrikesharp\api"
  if (Test-Path (Join-Path $try1 "CounterStrikeSharp.API.dll")) { $cssharpDir = $try1 }
  elseif (Test-Path (Join-Path $try2 "CounterStrikeSharp.API.dll")) { $cssharpDir = $try2 }
  if ([string]::IsNullOrWhiteSpace($cssharpDir)) {
    throw "[TASE] Could not find CounterStrikeSharp.API.dll under $serverRoot"
  }
  Set-Env "CSSHARP_DIR" $cssharpDir
}
Write-Host "[TASE] CSSHARP_DIR = $cssharpDir"

# Optional client (Panorama) deploy
if ([string]::IsNullOrWhiteSpace($clientRoot)) {
  $defaultClient = "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive"
  if (Test-Path (Join-Path $defaultClient "game\csgo\panorama")) {
    $clientRoot = $defaultClient
  } else {
    $inp = Read-Host "Enter CS2 Client path for Panorama deploy (or press Enter to skip)"
    if (-not [string]::IsNullOrWhiteSpace($inp)) { $clientRoot = $inp }
  }
  if ($clientRoot) {
    if (-not (Test-Path (Join-Path $clientRoot "game\csgo\panorama"))) {
      Write-Host "[TASE] Panorama not found under $clientRoot - skipping client deploy."
      $clientRoot = $null
    } else {
      Set-Env "CS2_CLIENT_ROOT" $clientRoot
    }
  }
}
if ($clientRoot) {
  $tasePanoRoot = Join-Path $clientRoot "game\csgo\panorama"
  Set-Env "TASE_PANO_ROOT" $tasePanoRoot
  Write-Host "[TASE] CS2_CLIENT_ROOT = $clientRoot"
}

# ---------------------------
# Stage API locally for build
# ---------------------------
$apiDll = Join-Path $cssharpDir "CounterStrikeSharp.API.dll"
if (-not (Test-Path $apiDll)) { throw "[TASE] Missing API: $apiDll" }

$libDir = Join-Path $RepoRoot "lib"
New-Item -ItemType Directory -Force -Path $libDir | Out-Null
Copy-Item $apiDll (Join-Path $libDir "CounterStrikeSharp.API.dll") -Force
Write-Host "[TASE] Staged API -> $libDir\CounterStrikeSharp.API.dll"

# ---------------------------
# Optional compile constant
# ---------------------------
$propsPath = Join-Path $RepoRoot "Directory.Build.props"
if ($DefineClientCmd) {
  $propsContent = @'
<Project>
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);TASE_HAS_CLIENTCMD</DefineConstants>
  </PropertyGroup>
</Project>
'@
  Set-Content -Path $propsPath -Value $propsContent -Encoding UTF8
  Write-Host "[TASE] Enabled TASE_HAS_CLIENTCMD (Directory.Build.props)"
} else {
  if (Test-Path $propsPath) {
    Remove-Item $propsPath -Force
    Write-Host "[TASE] Removed Directory.Build.props (no clientcmd)"
  }
}

# ---------------------------
# Build
# ---------------------------
Write-Host "[TASE] Restoring packages..."
dotnet restore

Write-Host "[TASE] Building Release..."
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw "[TASE] Build failed." }

# Locate output DLL
$tfms = @("net8.0","net7.0","net6.0")
$dll = $null
foreach ($tfm in $tfms) {
  $outDir = Join-Path $RepoRoot ("bin\Release\{0}" -f $tfm)
  if (Test-Path $outDir) {
    $cand = Get-ChildItem $outDir -Filter "*.dll" | Where-Object { $_.Name -match "Tase.*\.dll" } | Select-Object -First 1
    if ($cand) { $dll = $cand; break }
  }
}
if (-not $dll) { throw "[TASE] Build succeeded but no Tase*.dll found under bin\Release\<tfm>." }
Write-Host ("[TASE] Built: {0}" -f $dll.FullName)

# ---------------------------
# Deploy plugin to server
# ---------------------------
$pluginsDir = Join-Path $serverRoot "game\csgo\addons\counterstrikesharp\plugins"
New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
Copy-Item $dll.FullName (Join-Path $pluginsDir $dll.Name) -Force
Write-Host ("[TASE] Deployed plugin -> {0}\{1}" -f $pluginsDir, $dll.Name)

# Ensure data directories/files exist on server (first-run convenience)
$dataRoot = Join-Path $serverRoot "game\csgo\addons\counterstrikesharp\data\tase"
$classesDir = Join-Path $dataRoot "titles"
New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null
New-Item -ItemType Directory -Force -Path $classesDir | Out-Null

# Seed minimal files if missing
$titlesJson = Join-Path $dataRoot "titles.json"
$rolesJson  = Join-Path $dataRoot "roles.json"
$configJson = Join-Path $dataRoot "tase_config.json"

if (-not (Test-Path $titlesJson)) { "{}`n" | Out-File -Encoding UTF8 $titlesJson; Write-Host "[TASE] Seeded titles.json" }
if (-not (Test-Path $rolesJson))  { "{}`n" | Out-File -Encoding UTF8 $rolesJson;  Write-Host "[TASE] Seeded roles.json"  }

if (-not (Test-Path $configJson)) {
  $cfg = @{
    UseClanTagForTitles = $true
    PhysMaxDistance     = 1200
    PhysSafeMode        = $true
    AirMove             = 10
    AirAccelerate       = 100
    Emulate128Tick      = $false
  } | ConvertTo-Json -Depth 4
  $cfg | Out-File -Encoding UTF8 $configJson
  Write-Host "[TASE] Seeded tase_config.json"
}

# ---------------------------
# Optional: deploy Panorama
# ---------------------------
if ($clientRoot -and (Test-Path $tasePanoRoot)) {
  $srcLayout  = Join-Path $RepoRoot "client\panorama\layout\custom_game"
  $srcScripts = Join-Path $RepoRoot "client\panorama\scripts\custom_game"
  $srcStyles  = Join-Path $RepoRoot "client\panorama\styles\custom_game"

  $dstLayout  = Join-Path $tasePanoRoot "layout\custom_game"
  $dstScripts = Join-Path $tasePanoRoot "scripts\custom_game"
  $dstStyles  = Join-Path $tasePanoRoot "styles\custom_game"

  New-Item -ItemType Directory -Force -Path $dstLayout | Out-Null
  New-Item -ItemType Directory -Force -Path $dstScripts | Out-Null
  New-Item -ItemType Directory -Force -Path $dstStyles | Out-Null

  if (Test-Path $srcLayout)  { Copy-Item ($srcLayout + "\*")  -Destination $dstLayout  -Recurse -Force }
  if (Test-Path $srcScripts) { Copy-Item ($srcScripts + "\*") -Destination $dstScripts -Recurse -Force }
  if (Test-Path $srcStyles)  { Copy-Item ($srcStyles + "\*")  -Destination $dstStyles  -Recurse -Force }

  Write-Host "[TASE] Panorama assets deployed."
} else {
  Write-Host "[TASE] Panorama deploy skipped (no client path)."
}

Write-Host "[TASE] Build and deploy complete."
Write-Host ("  CSSHARP_DIR     = {0}" -f (Get-EnvCached "CSSHARP_DIR"))
Write-Host ("  CS2_SERVER_ROOT = {0}" -f (Get-EnvCached "CS2_SERVER_ROOT"))
if ($clientRoot) {
  Write-Host ("  CS2_CLIENT_ROOT = {0}" -f (Get-EnvCached "CS2_CLIENT_ROOT"))
}
