# Baixa as ferramentas externas que o Mexerica usa para a pasta tools\ (na raiz do projeto).
# Tudo vem das fontes oficiais. Rode uma vez antes de compilar:  powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$raiz  = Split-Path $PSScriptRoot -Parent
$tools = Join-Path $raiz 'tools'
$tmp   = Join-Path $env:TEMP ('mexerica-ferramentas-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory $tools, $tmp -Force | Out-Null
$ua = @{ 'User-Agent' = 'Mexerica' }

function Baixar($url, $arquivo) {
    Write-Host "  baixando $url"
    Invoke-WebRequest $url -OutFile $arquivo -Headers $ua -UseBasicParsing
}
function UltimaVersao($repo) { Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -Headers $ua }

try {
    # ffmpeg e ffprobe (build GPL de BtbN, o mesmo usado nos testes)
    Write-Host 'ffmpeg...'
    Baixar 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip' "$tmp\ffmpeg.zip"
    Expand-Archive "$tmp\ffmpeg.zip" "$tmp\ffmpeg" -Force
    Get-ChildItem "$tmp\ffmpeg" -Recurse -Include ffmpeg.exe, ffprobe.exe | Copy-Item -Destination $tools -Force

    # qpdf (organizar, dividir e juntar PDF)
    Write-Host 'qpdf...'
    $rel = UltimaVersao 'qpdf/qpdf'
    $zip = $rel.assets | Where-Object { $_.name -like '*msvc64.zip' } | Select-Object -First 1
    Baixar $zip.browser_download_url "$tmp\qpdf.zip"
    Expand-Archive "$tmp\qpdf.zip" "$tmp\qpdf" -Force
    New-Item -ItemType Directory "$tools\qpdf" -Force | Out-Null
    $bin = (Get-ChildItem "$tmp\qpdf" -Recurse -Directory -Filter bin | Select-Object -First 1).FullName
    Copy-Item (Join-Path $bin '*') "$tools\qpdf" -Force

    # ExifTool (metadados), versão Windows de Oliver Betz
    Write-Host 'ExifTool...'
    $ver = (Invoke-WebRequest 'https://exiftool.org/ver.txt' -UseBasicParsing -Headers $ua).Content.Trim()
    Baixar "https://sourceforge.net/projects/exiftool/files/exiftool-$($ver)_64.zip/download" "$tmp\exiftool.zip"
    Expand-Archive "$tmp\exiftool.zip" "$tmp\exiftool" -Force
    $exe = Get-ChildItem "$tmp\exiftool" -Recurse -Filter 'exiftool*.exe' | Select-Object -First 1
    Copy-Item $exe.FullName "$tools\exiftool.exe" -Force
    $files = Get-ChildItem "$tmp\exiftool" -Recurse -Directory -Filter exiftool_files | Select-Object -First 1
    if (Test-Path "$tools\exiftool_files") { Remove-Item "$tools\exiftool_files" -Recurse -Force }
    Copy-Item $files.FullName "$tools\exiftool_files" -Recurse -Force

    # Ghostscript (comprimir e tarjar PDF). Não existe zip portátil: o instalador é aberto com o 7-Zip,
    # que por sua vez é extraído pelo 7zr.exe oficial (nada é instalado no computador).
    Write-Host 'Ghostscript...'
    $rel = UltimaVersao 'ArtifexSoftware/ghostpdl-downloads'
    $gs = $rel.assets | Where-Object { $_.name -match 'w64\.exe$' } | Select-Object -First 1
    Baixar $gs.browser_download_url "$tmp\gs.exe"
    $sete = (Get-Command 7z.exe -ErrorAction SilentlyContinue).Source
    if (-not $sete -and (Test-Path "$env:ProgramFiles\7-Zip\7z.exe")) { $sete = "$env:ProgramFiles\7-Zip\7z.exe" }
    if (-not $sete) {
        Baixar 'https://www.7-zip.org/a/7zr.exe' "$tmp\7zr.exe"
        $pagina = (Invoke-WebRequest 'https://www.7-zip.org/download.html' -UseBasicParsing -Headers $ua).Content
        $inst = [regex]::Match($pagina, 'a/7z\d+-x64\.exe').Value
        Baixar "https://www.7-zip.org/$inst" "$tmp\7zip.exe"
        & "$tmp\7zr.exe" x "$tmp\7zip.exe" "-o$tmp\7zip" -y | Out-Null
        $sete = "$tmp\7zip\7z.exe"
    }
    & $sete e "$tmp\gs.exe" "-o$tools" 'bin\gswin64c.exe' 'bin\gsdll64.dll' -y | Out-Null

    # O Ghostscript precisa do runtime do Visual C++, que nem todo Windows tem. A Microsoft permite
    # distribuir esses arquivos junto com o programa: vão ao lado dele e ninguém precisa instalar nada.
    Write-Host 'Runtime do Visual C++...'
    $vc = "$tmp\vc_redist.x64.exe"
    Baixar 'https://aka.ms/vs/17/release/vc_redist.x64.exe' $vc
    & $sete e $vc "-o$tmp\vc" -y | Out-Null           # o vc_redist é um pacote WiX: o 7-Zip abre os .cab de dentro
    Get-ChildItem "$tmp\vc" -Filter '*.cab' -ErrorAction SilentlyContinue | ForEach-Object { & $sete e $_.FullName "-o$tmp\vc" -y | Out-Null }
    foreach ($n in 'vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll') {
        $achado = Get-ChildItem "$tmp\vc" -Recurse | Where-Object { $_.Name -like "$n*" } | Select-Object -First 1
        if ($achado) { Copy-Item $achado.FullName (Join-Path $tools $n) -Force }
        else { Copy-Item (Join-Path $env:WINDIR "System32\$n") (Join-Path $tools $n) -Force }   # plano B: a cópia desta máquina
    }
    foreach ($f in 'ffmpeg.exe', 'ffprobe.exe', 'qpdf\qpdf.exe', 'exiftool.exe', 'gswin64c.exe', 'gsdll64.dll', 'vcruntime140.dll', 'msvcp140.dll') {
        if (-not (Test-Path (Join-Path $tools $f))) { throw "Faltou $f" }
    }
    Write-Host "Pronto: ferramentas em $tools" -ForegroundColor Green
}
finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
