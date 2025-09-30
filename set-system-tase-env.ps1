<# 
Sets system-wide environment variables for CS2 + CounterStrikeSharp:
- CS2_SERVER_ROOT
- CSSHARP_DIR
- CS2_CLIENT_ROOT
- TASE_PANO_ROOT
Optionally adds CSSHARP_DIR to the system PATH (no duplicates).
Requires elevation; will self-elevate if needed.
#>

[CmdletBinding()]
param(
  [string]$ServerRoot   = "C:\cs2-ds",
  [string]$ClientRoot   = "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
  [switch]$AddCssharpDirToPath
)

$ErrorActionPreference = "Stop"

function Ensure-Elevated {
  $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  if (-not $isAdmin) {
    Write-Host "Re-launching with elevation..." -ForegroundColor Yellow
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "powershell.exe"
    $psi.Arguments = "-ExecutionPolicy Bypass -File `"$PSCommandPath`" " + ($MyInvocation.UnboundArguments -join ' ')
    $psi.Verb = "runas"
    [Diagnostics.Process]::Start($psi) | Out-Null
    exit
  }
}
Ensure-Elevated

function Test-ServerPaths([string]$root) {
  $api1 = Join-Path $root "game\csgo\addons\counterstrikesharp\api\CounterStrikeSharp.API.dll"
  $api2 = Join-Path $root "addons\counterstrikesharp\api\CounterStrikeSharp.API.dll"
  if (Test-Path $api1) { return (Split-Path -Parent $api1) }
  if (Test-Path $api2) { return (Split-Path -Parent $api2) }
  return $null
}

function Test-ClientPaths([string]$root) {
  $pano = Join-Path $root "game\csgo\panorama"
  if (Test-Path $pano) { return $pano }
  return $null
}

function Set-MachineEnv([string]$name, [string]$value) {
  [Environment]::SetEnvironmentVariable($name, $value, 'Machine')
  Write-Host "Set [Machine] $name = $value" -ForegroundColor Green
}

function Add-ToMachinePath([string]$dir) {
  $old = [Environment]::GetEnvironmentVariable('Path','Machine')
  if (-not $old) { $old = "" }
  $parts = $old -split ';' | Where-Object { $_ -and $_.Trim().Length -gt 0 }
  if ($parts -contains $dir) {
    Write-Host "PATH already contains: $dir" -ForegroundColor DarkGray
    return
  }
  $new = ($parts + $dir) -join ';'
  [Environment]::SetEnvironmentVariable('Path', $new, 'Machine')
  Write-Host "Appended to PATH: $dir" -ForegroundColor Green
}

# ---- Resolve/validate inputs ----
if (-not (Test-Path $ServerRoot)) { throw "Server root not found: $ServerRoot" }
$cssharpDir = Test-ServerPaths $ServerRoot
if (-not $cssharpDir) { throw "CounterStrikeSharp.API.dll not found under $ServerRoot. Is CounterStrikeSharp installed?" }

if (-not (Test-Path $ClientRoot)) { throw "Client root not found: $ClientRoot" }
$panoRoot = Test-ClientPaths $ClientRoot
if (-not $panoRoot) { throw "Panorama root not found under $ClientRoot (expected game\csgo\panorama)" }

# ---- Set machine-level env vars ----
Set-MachineEnv -name 'CS2_SERVER_ROOT' $ServerRoot
Set-MachineEnv -name 'CSSHARP_DIR'     $cssharpDir
Set-MachineEnv -name 'CS2_CLIENT_ROOT' $ClientRoot
Set-MachineEnv -name 'TASE_PANO_ROOT'  $panoRoot

if ($AddCssharpDirToPath) {
  Add-ToMachinePath -dir $cssharpDir
}

# ---- Broadcast environment change to running apps (new processes will have the values) ----
$signature = @"
using System;
using System.Runtime.InteropServices;
public class NativeMethods {
  [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
  public static extern IntPtr SendMessageTimeout(
    IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
    uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);
}
"@
Add-Type -TypeDefinition $signature -ErrorAction SilentlyContinue | Out-Null
$HWND_BROADCAST = [intptr]0xffff
$WM_SETTINGCHANGE = 0x1A
$SMTO_ABORTIFHUNG = 0x0002
[UIntPtr]$result = [UIntPtr]::Zero
[void][NativeMethods]::SendMessageTimeout($HWND_BROADCAST, $WM_SETTINGCHANGE, [UIntPtr]::Zero, "Environment", $SMTO_ABORTIFHUNG, 5000, [ref]$result)

Write-Host "Done. Machine-level variables set. NEW terminals and services will inherit them." -ForegroundColor Cyan
Write-Host "Verify with:  [Environment]::GetEnvironmentVariable('CSSHARP_DIR','Machine')" -ForegroundColor DarkGray
