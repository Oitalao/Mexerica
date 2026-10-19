# Compiles the right-click menu extension (src\menu\MexericaMenu.cpp -> src\menu\MexericaMenu.dll).
# Uses llvm-mingw (portable C++ compiler, nothing to install); downloads it to build-tools\ on first use.
# Usage:  powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tools = Join-Path $root 'build-tools'
$clang = Get-ChildItem $tools -Recurse -Filter 'x86_64-w64-mingw32-clang++.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $clang) {
    Write-Host 'Downloading llvm-mingw...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $rel = Invoke-RestMethod 'https://api.github.com/repos/mstorsjo/llvm-mingw/releases/latest'
    $asset = $rel.assets | Where-Object { $_.name -match 'ucrt-x86_64\.zip$' } | Select-Object -First 1
    New-Item -ItemType Directory -Force $tools | Out-Null
    $zip = Join-Path $tools 'llvm-mingw.zip'
    Invoke-WebRequest $asset.browser_download_url -OutFile $zip -UseBasicParsing
    Expand-Archive $zip $tools -Force
    Remove-Item $zip
    $clang = Get-ChildItem $tools -Recurse -Filter 'x86_64-w64-mingw32-clang++.exe' | Select-Object -First 1
}
$src = Join-Path $root 'src\menu'
& $clang.FullName -std=c++17 -O2 -shared -static -municode -fno-exceptions -fno-rtti `
    -o (Join-Path $src 'MexericaMenu.dll') (Join-Path $src 'MexericaMenu.cpp') (Join-Path $src 'MexericaMenu.def') `
    -lole32 -lshell32 -luuid -lgdi32 -luser32 -s
if ($LASTEXITCODE -ne 0) { throw 'Menu extension build failed' }
Write-Host "OK: $(Join-Path $src 'MexericaMenu.dll')" -ForegroundColor Green
