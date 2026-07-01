param(
  [int]$Port = 5173
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$serverScript = Join-Path $root "server.ps1"
$pidFile = Join-Path $root ".server.pid"
$urlFile = Join-Path $root ".server.url"

function Test-PortFree {
  param([int]$TestPort)

  $tcp = $null
  try {
    $tcp = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $TestPort)
    $tcp.Start()
    return $true
  } catch {
    return $false
  } finally {
    if ($tcp) {
      $tcp.Stop()
    }
  }
}

function Join-ProcessArguments {
  param([string[]]$Items)

  ($Items | ForEach-Object {
    if ($_ -match '[\s"]') {
      '"' + ($_ -replace '"', '\"') + '"'
    } else {
      $_
    }
  }) -join " "
}

if (Test-Path $pidFile) {
  $existingPid = Get-Content $pidFile -ErrorAction SilentlyContinue
  if ($existingPid) {
    $existing = Get-Process -Id ([int]$existingPid) -ErrorAction SilentlyContinue
    if ($existing) {
      $existingUrl = "http://localhost:$Port/"
      if (Test-Path $urlFile) {
        $existingUrl = Get-Content $urlFile -ErrorAction SilentlyContinue
      }
      Write-Host "Server is already running: $existingUrl"
      exit 0
    }
  }
}

$selectedPort = $Port
while (-not (Test-PortFree -TestPort $selectedPort)) {
  $selectedPort++
  if ($selectedPort -gt ($Port + 30)) {
    throw "No free port found between $Port and $($Port + 30)."
  }
}

$arguments = @(
  "-NoProfile",
  "-ExecutionPolicy", "Bypass",
  "-File", $serverScript,
  "-Port", $selectedPort,
  "-Root", $root
)

$powershellPath = (Get-Command powershell.exe).Source
$processInfo = [System.Diagnostics.ProcessStartInfo]::new()
$processInfo.FileName = $powershellPath
$processInfo.Arguments = Join-ProcessArguments -Items $arguments
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
$processInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden

$process = [System.Diagnostics.Process]::Start($processInfo)
Set-Content -Path $pidFile -Value $process.Id -Encoding ASCII
Set-Content -Path $urlFile -Value "http://localhost:$selectedPort/" -Encoding ASCII

Write-Host "Server started: http://localhost:$selectedPort/"
Write-Host "PID: $($process.Id)"
Write-Host "Stop it with: .\stop.ps1"
