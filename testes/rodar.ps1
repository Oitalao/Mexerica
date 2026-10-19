# Bateria de testes sem tela: converte e aplica as ferramentas em amostras e confere se cada operação terminou.
# Uso:  powershell -ExecutionPolicy Bypass -File testes\rodar.ps1 [-Exe caminho\Mexerica.exe]
param([string]$Exe)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
if (-not $Exe) {
    $Exe = @("$raiz\dist\Mexerica\Mexerica.exe") + (Get-ChildItem "$raiz\src\bin" -Recurse -Filter Mexerica.exe -ErrorAction SilentlyContinue | % FullName) | ? { Test-Path $_ } | Select-Object -First 1
}
if (-not $Exe) { throw 'Compile antes (dotnet build src -c Release) ou passe -Exe' }
$exe = $Exe
$t = Join-Path $env:TEMP ('mexerica-testes-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory $t | Out-Null
Copy-Item "$PSScriptRoot\amostras\*" $t
Set-Location $t
Write-Host "Testando $exe em $t"
# amostras derivadas
$ff = "$raiz\tools\ffmpeg.exe"
& $ff -hide_banner -loglevel error -y -i video.mp4 -t 6 -c:v libx264 -c:a aac curto.mp4
& $ff -hide_banner -loglevel error -y -i curto.mp4 -c copy video.mkv
& $ff -hide_banner -loglevel error -y -i curto.mp4 -c:v libvpx-vp9 -b:v 300k -c:a libopus video.webm
& $ff -hide_banner -loglevel error -y -i audio.wav -c:a libmp3lame -q:a 4 -metadata title="Teste" musica.mp3
& $ff -hide_banner -loglevel error -y -i audio.wav -c:a flac musica.flac
& $ff -hide_banner -loglevel error -y -i audio.wav -c:a libopus musica.opus
$p = 1..120 | % { "Parágrafo $_ do documento de teste. Ação, coração, maçã e pão. " * 4 }
[IO.File]::WriteAllText("$t\longo.txt", ($p -join "`r`n`r`n"), [Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllText("$t\pagina.html", "<html><head><meta charset='utf-8'><title>P</title></head><body><h1>Título</h1><p>Um <b>negrito</b> e <a href='https://exemplo.com'>link</a>.</p><ul><li>um</li><li>dois</li></ul></body></html>", [Text.UTF8Encoding]::new($false))
Copy-Item foto2.jpg "ação $([char]::ConvertFromUtf32(0x1F34A)).jpg"
$ok=0; $falhas=@()
function Rodar([string[]]$files, $mode, $label, [string[]]$p=@()){
  $rel="$t\r$(Get-Random).txt"; $a=@('--executar',$mode,$label,$rel); foreach($x in $p){ $a+='--p'; $a+=$x }; foreach($f in $files){ $a+="$t\$f" }
  Start-Process $exe -ArgumentList ($a | % { '"' + $_ + '"' }) -Wait | Out-Null
  foreach($l in (Get-Content $rel -Encoding UTF8)){ if($l -like 'OK*'){ $script:ok++ } elseif($l -like 'ERRO*'){ $script:falhas += "$($files -join '+') [$mode] $l" } }
  Remove-Item $rel
}
foreach($f in 'foto.png','foto2.jpg','desenho.svg','qr.png','audio.wav','musica.mp3','musica.opus','musica.flac','curto.mp4','video.webm','video.mkv','legenda.srt','amostra.7z','pacote.zip','texto.docx','nota.md','longo.txt','pagina.html','planilha.csv',"ação $([char]::ConvertFromUtf32(0x1F34A)).jpg"){ Rodar @($f) Convert '*' }
foreach($f in 'longo.pdf','texto.epub','planilha.xlsx','foto.heic','longo.pptx'){ Rodar @($f) Convert '*' }
$img='foto2.jpg'
Rodar @($img) Tools Comprimir @('modo=mb','mb=0.02'); Rodar @('foto.png') Tools Comprimir @('modo=pct','pct=40'); Rodar @($img) Tools Comprimir @('modo=nivel','nivel=forte')
Rodar @($img) Tools Tamanho @('modo=largura','valor=300'); Rodar @($img) Tools Girar @('giro=h'); Rodar @('foto.png') Tools Fundo @('cor=#FF0000','margem=10','formato=quadrado')
Rodar @($img) Tools Editar @('tarjas=10,10,80,60,preto;100,10,80,60,desfoque;190,10,80,60,pixel','brilho=20','saturacao=-40','nitidez=30','recorte=0,0,700,560',"camada=$t\camada.png",'giro=90')
Rodar @('qr.png','qr2.png') Tools 'Ler QR'; Rodar @('foto.png','foto2.jpg') Tools PDF; Rodar @('foto.png','foto2.jpg','qr.png') Tools Colagem @('colunas=0','largura=1200','espaco=10','cor=#202020')
Rodar @($img) Tools Metadados @('m.Title=Título ção','m.Artist=Ítalo'); Rodar @($img) Tools Metadados @('limpar=1')
$v='curto.mp4'
Rodar @($v) Tools Comprimir @('modo=mb','mb=0.4'); Rodar @($v) Tools Comprimir @('modo=nivel','nivel=forte'); Rodar @($v) Tools Cortar @('inicio=1','fim=3.5')
Rodar @('video.mkv') Tools Cortar @('inicio=1','fim=3','modo=rapido'); Rodar @($v) Tools Recortar @('recorte=100,50,320,240'); Rodar @($v) Tools Velocidade @('fator=2')
Rodar @('video.webm') Tools Velocidade @('fator=0.5'); Rodar @($v) Tools Dividir @('modo=partes','partes=3'); Rodar @($v) Tools Dividir @('modo=pontos','pontos=1.5;3')
Rodar @($v) Tools Quadro @('tempo=2.5','formato=png'); Rodar @($v) Tools 'Áudio' @('formato=mp3'); Rodar @($v) Tools Girar @('giro=270'); Rodar @('video.webm') Tools Tamanho @('altura=50%')
Rodar @($v) Tools Mudo; Rodar @($v) Tools Metadados @('m.Title=Vídeo'); Rodar @('video.mkv') Tools Metadados @('limpar=1'); Rodar @($v,'video.webm') Tools Juntar
$a='musica.mp3'
Rodar @($a) Tools Comprimir @('modo=nivel','nivel=forte'); Rodar @('musica.flac') Tools Comprimir @('modo=mb','mb=0.01'); Rodar @($a) Tools Cortar @('inicio=0.5','fim=2')
Rodar @('musica.flac') Tools Normalizar @('alvo=-14'); Rodar @($a) Tools Bipe @('trechos=0.5-1;2-2.5','freq=1000'); Rodar @('musica.opus') Tools Bipe @('trechos=0.5-1','freq=0')
Rodar @($a) Tools Canais @('canais=estereo'); Rodar @('audio.wav') Tools Canais @('canais=mono'); Rodar @($a) Tools Velocidade @('fator=1.5'); Rodar @($a) Tools Dividir @('modo=partes','partes=2')
Rodar @($a) Tools Metadados @('m.Title=Canção'); Rodar @($a,'musica.opus','audio.wav') Tools Juntar
$d='longo.pdf'
Rodar @($d) Tools Comprimir @('modo=nivel','nivel=forte'); Rodar @($d) Tools Organizar @('ordem=3,1,2r90,10r180'); Rodar @($d) Tools Dividir @('modo=cada','cada=4')
Rodar @($d) Tools Dividir @('modo=intervalos','intervalos=1-2, 5, 7-99'); Rodar @($d) Tools Tarjar @('tarjas=1:0.1,0.1,0.8,0.2;3:0,0,0.5,0.5'); Rodar @($d) Tools Metadados @('m.Title=Relatório')
Rodar @($d,'texto.pdf') Tools Juntar; Rodar @('amostra.7z') Tools Extrair; Rodar @('pacote.zip') Tools Extrair; Rodar @('foto2.jpg','texto.txt') Tools ZIP
"RESUMO: $ok ok, $($falhas.Count) falhas"; $falhas

Set-Location $raiz
if ($falhas.Count -eq 0) { Remove-Item $t -Recurse -Force; Write-Host 'Tudo certo.' -ForegroundColor Green } else { Write-Host "Arquivos do teste mantidos em $t" -ForegroundColor Yellow; exit 1 }
