param(
  [int]$Port = 5173,
  [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

$rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$rootPrefix = $rootPath + [System.IO.Path]::DirectorySeparatorChar
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)

$mimeTypes = @{
  ".html" = "text/html; charset=utf-8"
  ".css"  = "text/css; charset=utf-8"
  ".js"   = "text/javascript; charset=utf-8"
  ".json" = "application/json; charset=utf-8"
  ".svg"  = "image/svg+xml"
  ".png"  = "image/png"
  ".jpg"  = "image/jpeg"
  ".jpeg" = "image/jpeg"
  ".ico"  = "image/x-icon"
  ".txt"  = "text/plain; charset=utf-8"
}

$statusText = @{
  200 = "OK"
  400 = "Bad Request"
  403 = "Forbidden"
  404 = "Not Found"
  405 = "Method Not Allowed"
  500 = "Internal Server Error"
}

function Write-HttpResponse {
  param(
    [System.Net.Sockets.NetworkStream]$Stream,
    [int]$StatusCode,
    [string]$ContentType,
    [byte[]]$Body,
    [bool]$HeadOnly = $false
  )

  $reason = $statusText[$StatusCode]
  if (-not $reason) {
    $reason = "OK"
  }

  $headers = @(
    "HTTP/1.1 $StatusCode $reason",
    "Content-Type: $ContentType",
    "Content-Length: $($Body.Length)",
    "Cache-Control: no-store",
    "Connection: close",
    "",
    ""
  ) -join "`r`n"

  $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($headers)
  $Stream.Write($headerBytes, 0, $headerBytes.Length)

  if (-not $HeadOnly -and $Body.Length -gt 0) {
    $Stream.Write($Body, 0, $Body.Length)
  }
}

function Write-TextResponse {
  param(
    [System.Net.Sockets.NetworkStream]$Stream,
    [int]$StatusCode,
    [string]$Text,
    [bool]$HeadOnly = $false
  )

  $body = [System.Text.Encoding]::UTF8.GetBytes($Text)
  Write-HttpResponse -Stream $Stream -StatusCode $StatusCode -ContentType "text/plain; charset=utf-8" -Body $body -HeadOnly $HeadOnly
}

function Resolve-StaticFile {
  param([string]$RawPath)

  $pathOnly = $RawPath
  if ($pathOnly.StartsWith("http://") -or $pathOnly.StartsWith("https://")) {
    $pathOnly = ([Uri]$pathOnly).AbsolutePath
  }

  $pathOnly = $pathOnly.Split("?")[0]
  $relative = [Uri]::UnescapeDataString($pathOnly.TrimStart("/"))
  if ([string]::IsNullOrWhiteSpace($relative)) {
    $relative = "index.html"
  }

  $relative = $relative.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
  $target = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($rootPath, $relative))
  $insideRoot = $target.Equals($rootPath, [System.StringComparison]::OrdinalIgnoreCase) -or $target.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)

  if (-not $insideRoot) {
    return @{
      Status = 403
      Path = $null
    }
  }

  if ([System.IO.Directory]::Exists($target)) {
    $target = [System.IO.Path]::Combine($target, "index.html")
  }

  if (-not [System.IO.File]::Exists($target)) {
    return @{
      Status = 404
      Path = $null
    }
  }

  return @{
    Status = 200
    Path = $target
  }
}

try {
  $listener.Start()
  Write-Host "Twitch pin server running at http://localhost:$Port/"
  Write-Host "Press Ctrl+C to stop."

  while ($true) {
    $client = $listener.AcceptTcpClient()
    $stream = $null
    try {
      $stream = $client.GetStream()
      $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 1024, $true)
      $requestLine = $reader.ReadLine()

      if ([string]::IsNullOrWhiteSpace($requestLine)) {
        Write-TextResponse -Stream $stream -StatusCode 400 -Text "Bad request"
        continue
      }

      do {
        $line = $reader.ReadLine()
      } while ($null -ne $line -and $line.Length -gt 0)

      $parts = $requestLine.Split(" ")
      if ($parts.Count -lt 2) {
        Write-TextResponse -Stream $stream -StatusCode 400 -Text "Bad request"
        continue
      }

      $method = $parts[0].ToUpperInvariant()
      $headOnly = $method -eq "HEAD"
      if ($method -ne "GET" -and -not $headOnly) {
        Write-TextResponse -Stream $stream -StatusCode 405 -Text "Method not allowed"
        continue
      }

      $resolved = Resolve-StaticFile -RawPath $parts[1]
      if ($resolved["Status"] -eq 403) {
        Write-TextResponse -Stream $stream -StatusCode 403 -Text "Forbidden" -HeadOnly $headOnly
        continue
      }

      if ($resolved["Status"] -eq 404) {
        Write-TextResponse -Stream $stream -StatusCode 404 -Text "Not found" -HeadOnly $headOnly
        continue
      }

      $extension = [System.IO.Path]::GetExtension($resolved["Path"]).ToLowerInvariant()
      $contentType = $mimeTypes[$extension]
      if (-not $contentType) {
        $contentType = "application/octet-stream"
      }

      $bytes = [System.IO.File]::ReadAllBytes($resolved["Path"])
      Write-HttpResponse -Stream $stream -StatusCode 200 -ContentType $contentType -Body $bytes -HeadOnly $headOnly
    } catch {
      if ($stream) {
        Write-TextResponse -Stream $stream -StatusCode 500 -Text $_.Exception.Message
      }
    } finally {
      $client.Close()
    }
  }
} finally {
  $listener.Stop()
}
