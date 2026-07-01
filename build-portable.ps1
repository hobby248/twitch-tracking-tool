param(
  [string]$OutputDirectory = ".\dist",
  [string]$RuntimeSource
)

$ErrorActionPreference = "Stop"

if ($RuntimeSource) {
  & "$PSScriptRoot\prepare-webview2-runtime.ps1" -Source $RuntimeSource
}

& "$PSScriptRoot\build-exe.ps1" -OutputDirectory $OutputDirectory -IncludeBundledRuntime -RequireBundledRuntime

$outDir = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputDirectory))
$runtimeRoot = Join-Path $outDir "WebView2Runtime"
$runtimeExe = Get-ChildItem -Path $runtimeRoot -Filter "msedgewebview2.exe" -File -Recurse -ErrorAction SilentlyContinue |
  Select-Object -First 1
if (-not $runtimeExe) {
  throw "可攜版缺少 WebView2Runtime 底下的 msedgewebview2.exe。"
}

Write-Host "Portable package ready: $outDir"
