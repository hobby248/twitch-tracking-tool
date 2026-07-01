$ErrorActionPreference = "Stop"

$pidFile = Join-Path $PSScriptRoot ".server.pid"
$urlFile = Join-Path $PSScriptRoot ".server.url"

if (-not (Test-Path $pidFile)) {
  Write-Host "No server pid file found."
  exit 0
}

$serverPid = Get-Content $pidFile -ErrorAction SilentlyContinue
if (-not $serverPid) {
  Remove-Item -LiteralPath $pidFile -Force
  if (Test-Path $urlFile) {
    Remove-Item -LiteralPath $urlFile -Force
  }
  Write-Host "Pid file was empty."
  exit 0
}

$process = Get-Process -Id ([int]$serverPid) -ErrorAction SilentlyContinue
if ($process) {
  Stop-Process -Id $process.Id
  Write-Host "Stopped server process $($process.Id)."
} else {
  Write-Host "Server process was not running."
}

Remove-Item -LiteralPath $pidFile -Force
if (Test-Path $urlFile) {
  Remove-Item -LiteralPath $urlFile -Force
}
