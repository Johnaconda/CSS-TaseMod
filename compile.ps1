<#
Builds the plugin using env vars set by the server/client setup scripts.
- Auto-loads persisted env vars if not in current process
- Uses CSSHARP_DIR to stage ./lib/CounterStrikeSharp.API.dll
- dotnet restore + build Release
- Copies built DLL to server plugins folder
- Optional: deploy Panorama from ./panorama_src if client env is present
#>
[CmdletBinding()] param(
  [switch]$DefineClientCmd  # adds TASE_HAS_CLIENTCMD compile symbol
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Split-Path -Parent $PSCommandPath)).Path
Set-Location $ProjectRoot

function Get-EnvCached {
  param([Parameter(Mandatory)][string]$Name)
  if ($env:$Name) { return $env:$Name }
  $u = [Environment]::GetEnvironmentVariable($Name,'User')
  if ([string]::IsNullOrWhiteSpace($u) -eq $false) { $env:$Name = $u; return $u }
  $m = [Environment]::GetEnvironmentVariable($Name,'Machine')
  if ([string]::IsNullOrWhiteSpace($m) -eq $false) { $env:$Name = $m; return $m }
  return $null
}

# Load/prefill env
$cssharpDir     = Get-EnvCached -Name 'CSSHARP_DIR'
$serverRoot     = Get-EnvCached -Name 'CS2_SERVER_ROOT'
$clientRoot     = Get-EnvCached -Name 'CS2_CLIENT_ROOT'
$tasePanoRoot   = Get-EnvCached -Name 'TASE_PANO_ROOT'

# Fallback: derive CSSHARP_DIR from serverRoot if not set
if (-not $cssharpDir -and $serverRoot) {
  $try1 = Join-Path $serverRoot 'game\csgo\addons\counterstrikesharp\api'
  $try2 = Join-Path $serverRoot 'addons\counterstrikesharp\api'
  if (Test-Path (Join-Path $try1 'CounterStrikeSharp.API.dll')) { $cssharpDir = $try1 }
  elseif (Test-Path (Join-Path $try2 'CounterStrikeSharp.API.dll')) { $cssharpDir = $try2 }
  if ($cssharpDir) { $env:CSSHARP_DIR = $cssharpDir }
}

if (-not $cssharpDir) {
  throw "CSSHARP_DIR not set and not found. Run C:\cs2-ds\setup-server-env.ps1 or set CSSHARP_DIR manually."
}
if (-not $serverRoot) {
  throw "CS2_SERVER_ROOT not set. Run C:\cs2-ds\setup-server-env.ps1 first."
}

# Verify API DLL and stage local fallback
$apiDll = Join-Path $cssharpDir 'CounterStrikeSharp.API.dll'
if (-not (Test-Path $apiDll)) {
  throw "CounterStrikeSharp.API.dll not found at $apiDll"
}

$libDir = Join-Path $ProjectRoot 'lib'
$null = New-Item -ItemType Directory -Force -Path $libDir
Copy-Item $apiDll (Join-Path $libDir 'CounterStrikeSharp.API.dll') -Force

# Ensure Newtonsoft.Json
dotnet add package Newtonsoft.Json -v 13.0.3 | Out-Null

# Optional compile constant (without editing .csproj)
if ($DefineClientCmd) {
  $props = @"
<Project>
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);TASE_HAS_CLIENTCMD</DefineConstants>
  </PropertyGroup>
</Project>
"@
  $propsPath = Join-Path $ProjectRoot 'Directory.Build.props'
  $props | Out-File -Encoding UTF8 $propsPath
  Write-Host "Added TASE_HAS_CLIENTCMD via Directory.Build.props" -ForegroundColor DarkGray
}

dotnet restore
dotnet build -c Release

# Find built DLL
$bin = Join-Path $ProjectRoot 'bin\Release\net8.0'
$dll = Get-ChildItem $bin -Filter "*.dll" | Where-Object { $_.Name -like "*Tase*.dll" } | Select-Object -First 1
if (-not $dll) { throw "Built DLL not found in $bin" }

# Deploy to server plugins
$pluginsDir = Join-Path $serverRoot 'game\csgo\addons\counterstrikesharp\plugins'
$null = New-Item -ItemType Directory -Force -Path $pluginsDir
Copy-Item $dll.FullName (Join-Path $pluginsDir $dll.Name) -Force
Write-Host "Deployed plugin -> $pluginsDir\$($dll.Name)" -ForegroundColor Green

# OPTIONAL: deploy Panorama if env present and repo contains sources
if ($clientRoot -and $tasePanoRoot) {
  $srcLayout  = Join-Path $ProjectRoot 'panorama_src\layout'
  $srcScripts = Join-Path $ProjectRoot 'panorama_src\scripts'
  $srcStyles  = Join-Path $ProjectRoot 'panorama_src\styles'
  if (Test-Path $srcLayout)  { Copy-Item $srcLayout\*  (Join-Path $tasePanoRoot 'layout\custom_game\tase')  -Recurse -Force }
  if (Test-Path $srcScripts) { Copy-Item $srcScripts\* (Join-Path $tasePanoRoot 'scripts\custom_game\tase') -Recurse -Force }
  if (Test-Path $srcStyles)  { Copy-Item $srcStyles\*  (Join-Path $tasePanoRoot 'styles\custom_game\tase')  -Recurse -Force }
  Write-Host "Panorama (optional) deployed to client root." -ForegroundColor DarkGray
}

Write-Host "Build + deploy done." -ForegroundColor Cyan
Write-Host "CSSHARP_DIR = $cssharpDir" -ForegroundColor DarkGray
Write-Host "CS2_SERVER_ROOT = $serverRoot" -ForegroundColor DarkGray
