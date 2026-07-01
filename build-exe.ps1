param(
  [string]$OutputDirectory = ".\dist",
  [switch]$RequireBundledRuntime
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$outDir = [System.IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
$outFile = Join-Path $outDir "Twitch 追台工具.exe"
$legacyOutFile = Join-Path $outDir "TwitchPin.exe"
$webViewPackageRoot = "D:\TwitchPinDeps\NuGet\Microsoft.Web.WebView2"
$fixedRuntimeSource = "D:\TwitchPinDeps\WebView2Runtime"
$fixedRuntimeOut = Join-Path $outDir "WebView2Runtime"
$candidates = @(
  (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
  (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
  throw "找不到 Windows 內建 C# 編譯器 csc.exe。"
}

$webViewPackage = Get-ChildItem -Path $webViewPackageRoot -Directory -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -notmatch "-" } |
  Sort-Object { [version]$_.Name } |
  Select-Object -Last 1

if (-not $webViewPackage) {
  throw "找不到 WebView2 SDK。請先下載 Microsoft.Web.WebView2 到 $webViewPackageRoot。"
}

$webViewLib = Join-Path $webViewPackage.FullName "lib\net462"
$webViewCore = Join-Path $webViewLib "Microsoft.Web.WebView2.Core.dll"
$webViewWinForms = Join-Path $webViewLib "Microsoft.Web.WebView2.WinForms.dll"
$webViewLoader = Join-Path $webViewPackage.FullName "runtimes\win-x64\native\WebView2Loader.dll"

foreach ($required in @($webViewCore, $webViewWinForms, $webViewLoader)) {
  if (-not (Test-Path $required)) {
    throw "找不到 WebView2 必要檔案：$required"
  }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$arguments = @(
  "/nologo",
  "/target:winexe",
  "/platform:anycpu",
  "/optimize+",
  "/out:$outFile",
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll",
  "/reference:$webViewCore",
  "/reference:$webViewWinForms",
  "/resource:$(Join-Path $root 'index.html'),wwwroot.index.html",
  "/resource:$(Join-Path $root 'styles.css'),wwwroot.styles.css",
  "/resource:$(Join-Path $root 'app.js'),wwwroot.app.js",
  (Join-Path $root "src\TwitchPinLauncher\Program.cs")
)

& $csc @arguments
if ($LASTEXITCODE -ne 0) {
  throw "編譯失敗，csc.exe 回傳 $LASTEXITCODE。"
}

if (-not (Test-Path $outFile)) {
  throw "編譯失敗，沒有產生 $outFile。"
}

Copy-Item -LiteralPath $webViewCore -Destination (Join-Path $outDir "Microsoft.Web.WebView2.Core.dll") -Force
Copy-Item -LiteralPath $webViewWinForms -Destination (Join-Path $outDir "Microsoft.Web.WebView2.WinForms.dll") -Force
Copy-Item -LiteralPath $webViewLoader -Destination (Join-Path $outDir "WebView2Loader.dll") -Force

$extensionsSource = Join-Path $root "Extensions"
if (Test-Path $extensionsSource) {
  $extensionsOut = Join-Path $outDir "Extensions"
  New-Item -ItemType Directory -Force -Path $extensionsOut | Out-Null
  Copy-Item -Path (Join-Path $extensionsSource "*") -Destination $extensionsOut -Recurse -Force
}

$fixedRuntimeExe = Get-ChildItem -Path $fixedRuntimeSource -Filter "msedgewebview2.exe" -File -Recurse -ErrorAction SilentlyContinue |
  Select-Object -First 1

if ($fixedRuntimeExe) {
  if (Test-Path $fixedRuntimeOut) {
    Remove-Item -LiteralPath $fixedRuntimeOut -Recurse -Force
  }

  Copy-Item -LiteralPath $fixedRuntimeSource -Destination $fixedRuntimeOut -Recurse -Force
  Write-Host "Bundled WebView2 runtime: $fixedRuntimeOut"
  Write-Host "Runtime executable: $($fixedRuntimeExe.FullName)"
} else {
  $runtimeMessage = "Bundled WebView2 runtime: not found at $fixedRuntimeSource; app will use installed Runtime."
  if ($RequireBundledRuntime) {
    throw $runtimeMessage
  }

  Write-Host $runtimeMessage
}

Copy-Item -LiteralPath $outFile -Destination $legacyOutFile -Force

Write-Host "Built: $outFile"
Write-Host "Legacy copy: $legacyOutFile"
