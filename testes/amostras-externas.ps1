# Testa formatos raros com arquivos reais baixados de acervos públicos (FFmpeg, libarchive, assimp, Khronos, dtrx).
# Os arquivos não ficam no repositório. Uso: powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1 [-Exe caminho\Mexerica.exe]
param([string]$Exe)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$raiz = Split-Path $PSScriptRoot -Parent
if (-not $Exe) {
    $Exe = @("$raiz\dist\Mexerica\Mexerica.exe") + (Get-ChildItem "$raiz\src\bin" -Recurse -Filter Mexerica.exe -ErrorAction SilentlyContinue | ForEach-Object FullName) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Exe) { throw 'Compile antes (dotnet build src -c Release) ou passe -Exe' }
$t = Join-Path $env:TEMP ('mexerica-externas-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory $t | Out-Null

$fate = 'https://fate-suite.ffmpeg.org'
$sam = 'https://samples.ffmpeg.org/A-codecs'
$dtrx = 'https://sources.debian.org/data/main/d/dtrx/8.5.3-3/tests'
$assimp = 'https://raw.githubusercontent.com/assimp/assimp/master/test/models/FBX'
$khronos = 'https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models/BoxTextured/glTF'
$lib = 'https://raw.githubusercontent.com/libarchive/libarchive/master/libarchive/test'
$baixar = [ordered]@{
    'apple.ape' = "$fate/lossless-audio/luckynight-mac394b1-c2000.ape"
    'dst.dff' = "$fate/dst/dst-64fs44-2ch.dff"
    'musepack.mpc' = "$fate/musepack/inside-mp8.mpc"
    'pcm.w64' = "$fate/w64/w64-pcm16.w64"
    'aac.caf' = "$fate/caf/aac.caf"
    'fala.amr' = "$sam/amr/sample.amr"
    'surround.dts' = "$sam/DTS/dts/3-1.dts"
    'stereo.ac3' = "$sam/AC3/monsters_inc_2.0_192.ac3"
    'arquivo.arj' = "$dtrx/test-1.23.arj"
    'disco.dmg' = "$dtrx/test.compressed.dmg"
    'pacote.rpm' = "$dtrx/test.rpm"
    'pacote.deb' = "$dtrx/test-1.23_all.deb"
    'arquivo.cpio' = "$dtrx/test-1.23.cpio"
    'arquivo.tar.lzma' = "$dtrx/test-1.23.tar.lzma"
    'caixa.fbx' = "$assimp/box.fbx"
    'aranha.fbx' = "$assimp/spider.fbx"
    'matriz-c.fbx' = "$assimp/boxWithCompressedCTypeArray.FBX"
    'gltf\BoxTextured.gltf' = "$khronos/BoxTextured.gltf"
    'gltf\BoxTextured0.bin' = "$khronos/BoxTextured0.bin"
    'gltf\CesiumLogoFlat.png' = "$khronos/CesiumLogoFlat.png"
}
$uu = [ordered]@{ 'lh7.lzh' = 'test_read_format_lha_lh7.lzh.uu'; 'lh6.lzh' = 'test_read_format_lha_lh6.lzh.uu'; 'header0.lzh' = 'test_read_format_lha_header0.lzh.uu'
                  'mac.tar.Z' = 'test_compat_mac-1.tar.Z.uu'; 'texto.txt.Z' = 'test_archive_string_conversion.txt.Z.uu' }
try {
    New-Item -ItemType Directory "$t\gltf" | Out-Null
    foreach ($k in $baixar.Keys) { Invoke-WebRequest $baixar[$k] -OutFile "$t\$k" -UseBasicParsing -Headers @{ 'User-Agent' = 'Mexerica-testes' } }
    # DSD: o arquivo de teste vem num zip
    Invoke-WebRequest 'https://samplerateconverter.com/free-files/samples/dsf/sine-176400hz-1000hz-15s-D64-2.8mhz.zip' -OutFile "$t\dsf.zip" -UseBasicParsing
    Expand-Archive "$t\dsf.zip" "$t\dsf" -Force; Get-ChildItem "$t\dsf" -Recurse -Filter *.dsf | Select-Object -First 1 | Copy-Item -Destination "$t\seno.dsf"
    Remove-Item "$t\dsf", "$t\dsf.zip" -Recurse -Force
    # libarchive guarda as amostras em uuencode
    foreach ($k in $uu.Keys) {
        $linhas = (Invoke-WebRequest "$lib/$($uu[$k])" -UseBasicParsing).Content -split "`r?`n"
        $bytes = New-Object System.Collections.Generic.List[byte]
        foreach ($l in ($linhas | Select-Object -Skip 1)) {
            if ($l -eq 'end' -or $l.Length -eq 0) { break }
            $n = ([int][char]$l[0] - 32) -band 63; $q = $l.Substring(1)
            for ($i = 0; $i -lt $q.Length - 3 -and $n -gt 0; $i += 4) {
                $c = 0..3 | ForEach-Object { ([int][char]$q[$i + $_] - 32) -band 63 }
                $b0 = (($c[0] -shl 2) -bor ($c[1] -shr 4)) -band 255
                $b1 = (($c[1] -shl 4) -bor ($c[2] -shr 2)) -band 255
                $b2 = (($c[2] -shl 6) -bor $c[3]) -band 255
                $bytes.Add([byte]$b0); if ($n -gt 1) { $bytes.Add([byte]$b1) }; if ($n -gt 2) { $bytes.Add([byte]$b2) }; $n -= 3
            }
        }
        [IO.File]::WriteAllBytes("$t\$k", $bytes.ToArray())
    }
    # WIM: o próprio 7-Zip do Mexerica cria um
    Copy-Item "$PSScriptRoot\amostras\foto2.jpg", "$PSScriptRoot\amostras\texto.txt" $t
    & (Join-Path (Split-Path $Exe) 'tools\7zip\7z.exe') a -twim "$t\imagem.wim" "$t\foto2.jpg" "$t\texto.txt" | Out-Null
} catch { throw "Não deu para baixar as amostras: $($_.Exception.Message)" }

$ok = 0; $falhas = @()
function Rodar([string]$file, $mode, $label) {
    $rel = "$t\r$(Get-Random).txt"
    Start-Process $Exe -ArgumentList ('--executar', $mode, $label, "`"$rel`"", "`"$file`"") -Wait -WindowStyle Hidden | Out-Null
    foreach ($l in (Get-Content $rel -Encoding UTF8)) { if ($l -like 'OK*') { $script:ok++ } elseif ($l -like 'ERRO*') { $script:falhas += "$(Split-Path $file -Leaf) [$mode] $l" } }
    Remove-Item $rel
}
$entradas = @($baixar.Keys | Where-Object { $_ -notlike 'gltf\Box*.bin' -and $_ -notlike 'gltf\*.png' }) + @($uu.Keys) + @('seno.dsf', 'imagem.wim')
foreach ($f in $entradas) { Rodar "$t\$f" Convert '*' }
foreach ($a in 'arquivo.arj', 'disco.dmg', 'pacote.rpm', 'imagem.wim', 'lh7.lzh', 'mac.tar.Z') { Rodar "$t\$a" Tools Extrair; Rodar "$t\$a" Tools Testar }
"RESUMO: $ok ok, $($falhas.Count) falhas"; $falhas
if ($falhas.Count -eq 0) { Remove-Item $t -Recurse -Force; 'Tudo certo.' } else { "Arquivos do teste mantidos em $t" }
