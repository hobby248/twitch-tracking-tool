param(
  [Parameter(Mandatory = $true)]
  [string]$Source,

  [string]$Destination = "D:\TwitchPinDeps\WebView2Runtime"
)

$ErrorActionPreference = "Stop"

function Resolve-ExistingPath {
  param([string]$PathValue)

  $resolved = Resolve-Path -LiteralPath $PathValue -ErrorAction SilentlyContinue
  if (-not $resolved) {
    throw "找不到來源：$PathValue"
  }

  return $resolved.ProviderPath
}

function Assert-SafeDestination {
  param([string]$PathValue)

  $fullPath = [System.IO.Path]::GetFullPath($PathValue)
  $rootPath = [System.IO.Path]::GetPathRoot($fullPath)
  if ($fullPath.TrimEnd('\') -eq $rootPath.TrimEnd('\')) {
    throw "拒絕把磁碟根目錄當作目的地：$fullPath"
  }

  if ($fullPath -notmatch '\\WebView2Runtime$') {
    throw "目的地必須是 WebView2Runtime 資料夾：$fullPath"
  }

  return $fullPath
}

$sourcePath = Resolve-ExistingPath $Source
$destinationPath = Assert-SafeDestination $Destination
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("TwitchPin-WebView2Runtime-" + [System.Guid]::NewGuid().ToString("N"))
$extractRoot = $null
$usingTemp = $false

try {
  if (Test-Path -LiteralPath $sourcePath -PathType Container) {
    $extractRoot = $sourcePath
  } else {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    $usingTemp = $true

    $extension = [System.IO.Path]::GetExtension($sourcePath).ToLowerInvariant()
    switch ($extension) {
      ".zip" {
        Expand-Archive -LiteralPath $sourcePath -DestinationPath $tempRoot -Force
      }
      ".cab" {
        $expandExe = Join-Path $env:WINDIR "System32\expand.exe"
        & $expandExe -F:* $sourcePath $tempRoot | Out-Null
        if ($LASTEXITCODE -ne 0) {
          throw "解壓 cab 失敗，expand.exe 回傳 $LASTEXITCODE。"
        }
      }
      default {
        throw "不支援的來源格式：$extension。請使用資料夾、zip 或 cab。"
      }
    }

    $extractRoot = $tempRoot
  }

  $runtimeExe = Get-ChildItem -Path $extractRoot -Filter "msedgewebview2.exe" -File -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1

  if (-not $runtimeExe) {
    throw "來源中找不到 msedgewebview2.exe。請確認下載的是 Microsoft WebView2 Fixed Version Runtime。"
  }

  $runtimeRoot = $runtimeExe.Directory.FullName
  $destinationParent = Split-Path -Parent $destinationPath
  New-Item -ItemType Directory -Force -Path $destinationParent | Out-Null

  if (Test-Path -LiteralPath $destinationPath) {
    Remove-Item -LiteralPath $destinationPath -Recurse -Force
  }

  New-Item -ItemType Directory -Force -Path $destinationPath | Out-Null
  Get-ChildItem -LiteralPath $runtimeRoot -Force | Copy-Item -Destination $destinationPath -Recurse -Force

  $copiedRuntimeExe = Get-ChildItem -Path $destinationPath -Filter "msedgewebview2.exe" -File -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1

  if (-not $copiedRuntimeExe) {
    throw "整理後仍找不到 msedgewebview2.exe：$destinationPath"
  }

  Write-Host "Prepared WebView2 runtime: $destinationPath"
  Write-Host "Runtime executable: $($copiedRuntimeExe.FullName)"
}
finally {
  if ($usingTemp -and (Test-Path -LiteralPath $tempRoot)) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
  }
}
