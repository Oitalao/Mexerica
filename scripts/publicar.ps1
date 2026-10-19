# Gera o instalador para anexar numa Release do GitHub: dist\Mexerica-<versao>-instalador.exe
# Precisa do .NET 8 SDK e do Inno Setup 6 (winget install JRSoftware.InnoSetup).
# Uso:  powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path (Join-Path $raiz 'tools\ffmpeg.exe'))) { & (Join-Path $PSScriptRoot 'baixar-ferramentas.ps1') }
$dll = Join-Path $raiz 'src\menu\MexericaMenu.dll'
$cpp = Join-Path $raiz 'src\menu\MexericaMenu.cpp'
if (-not (Test-Path $dll) -or (Get-Item $cpp).LastWriteTime -gt (Get-Item $dll).LastWriteTime) {
    & (Join-Path $PSScriptRoot 'compilar-menu.ps1')   # extensão do botão direito (C++)
}

$csproj = Join-Path $raiz 'src\Mexerica.csproj'
$versao = ([xml](Get-Content $csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$dist = Join-Path $raiz 'dist'
$saida = Join-Path $dist 'Mexerica'
if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }
dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $saida -nologo
if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou' }
Copy-Item (Join-Path $raiz 'LICENSE'), (Join-Path $raiz 'THIRD-PARTY-NOTICES.md') $saida

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado (winget install JRSoftware.InnoSetup)' }
& $iscc "/DVersao=$versao" /Q (Join-Path $raiz 'instalador\Mexerica.iss')
if ($LASTEXITCODE -ne 0) { throw 'O instalador falhou' }
$exe = Join-Path $dist "Mexerica-$versao-instalador.exe"
Write-Host ("Pronto: {0} ({1:0} MB)" -f $exe, ((Get-Item $exe).Length / 1MB)) -ForegroundColor Green
