<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">Converta e edite arquivos sem abrir programa nenhum: arraste o arquivo, aperte <b>Espaço</b> e solte na opção.</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="Roda Converter">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="Roda Ferramentas">
  <img src="assets/telas/roda-acoes.png" width="260" alt="Roda Ações">
</p>

O Mexerica é uma versão para Windows da ideia do [Tangerine](https://tangerineformac.com), do Mac. Ele fica na bandeja do sistema. Quando você arrasta um ou mais arquivos e aperta Espaço sem soltar o mouse, aparece uma roda em volta do cursor com o que dá para fazer com eles. O resultado é salvo ao lado do original, que nunca é alterado. Tudo roda no seu computador; nada é enviado para a internet.

## Como usar

| Ação | O que fazer |
|---|---|
| Abrir a roda | Arraste arquivos e, sem soltar, aperte **Espaço** |
| Trocar de roda | **Espaço** de novo, ou **Shift** (Converter), **Ctrl** (Ferramentas), **Alt** (Ações) |
| Escolher | Solte o arquivo em cima da opção, ou use as **setas** e **Enter** |
| Cancelar | **Esc** |
| Pelo teclado | Selecione arquivos no Explorador ou na área de trabalho e aperte **Ctrl + Shift + Espaço** |
| Tela de toque | Segure o dedo parado um instante e arraste |
| Botão direito | Selecione os arquivos, clique com o botão direito e use o submenu **Mexerica** (no Windows 11, em "Mostrar mais opções") |

Vários arquivos são processados ao mesmo tempo. Trocar só o formato do vídeo (MKV, MOV, MP4) é feito sem recodificar quando dá, em segundos e sem perder qualidade. Quando precisa recodificar, o Mexerica usa a placa de vídeo (NVIDIA, Intel ou AMD), se houver.

Teclas, qualidade e pasta de saída mudam nas Configurações (clique duplo no ícone da bandeja, ou abra o Mexerica de novo).

## O que ele faz

**Converter**
- Imagens: JPG, PNG, WebP, HEIC, AVIF, TIFF, BMP, GIF, ICO, PDF (SVG como entrada)
- Áudio: MP3, M4A, WAV, FLAC, OGG, Opus, AIFF, WMA
- Vídeo: MP4, MOV, MKV, WebM, AVI, WMV, GIF animado, MP3
- PDF para DOCX, PPTX, XLSX, EPUB, TXT, Markdown, JPG e PNG (todas as páginas)
- Documentos: DOCX, ODT, RTF, Markdown, TXT, EPUB, HTML (EPUB e arquivos do iWork como entrada)
- Apresentações para PDF, PNG e JPG; planilhas entre XLSX, ODS, CSV, TSV e PDF
- Legendas SRT, VTT e TXT; compactados ZIP, TAR e TAR.GZ (RAR e 7z como entrada)

**Ferramentas**
- Comprimir imagem, vídeo, áudio e PDF por nível, por porcentagem ou para um tamanho exato
- Editor de foto: recortar, ajustar cor, setas, retângulos, caneta, texto e tarjas (preta, desfoque ou pixel)
- Fundo e margem para imagens, colagem de várias fotos, imagens em PDF, leitura de QR Code
- Metadados: ver tudo, editar título, autor e outros campos, apagar localização ou tudo
- Vídeo: cortar trecho, recortar a imagem, velocidade, dividir, tirar um quadro, extrair o áudio, girar, mudar a resolução, tirar o som, juntar
- Áudio: cortar trecho, normalizar o volume, bipe por cima de trechos, mono e estéreo, velocidade, dividir, juntar
- PDF: organizar páginas (reordenar, girar, apagar), dividir, juntar e tarjar de verdade (o texto escondido some)

**Ações**: compartilhar (Compartilhamento por Proximidade do Windows), deixar flutuando na tela, copiar ou mover para uma pasta, copiar o caminho e abrir o terminal na pasta.

## Instalar

1. Baixe o `Mexerica-1.0.0-instalador.exe` na página de **Releases** (à direita, nesta página).
2. Abra o arquivo baixado e clique em **Avançar** até o fim. Não precisa de senha de administrador.
3. Se o Windows mostrar "O Windows protegeu o computador", clique em **Mais informações** e depois em **Executar assim mesmo**. O aviso aparece porque o instalador não tem assinatura digital paga, não por haver algo errado.

Ao terminar, uma janela explica como usar. O Mexerica abre sozinho quando o computador liga (dá para desligar no menu da bandeja). Para remover: **Configurações → Aplicativos → Aplicativos instalados → Mexerica → Desinstalar**. Isso tira também o menu do botão direito e as configurações.

Requisitos: Windows 10 (versão 2004 ou mais nova) ou Windows 11, 64 bits. Não é preciso instalar mais nada à mão: se faltar o [LibreOffice](https://pt-br.libreoffice.org) (usado para documentos, planilhas e apresentações), o instalador avisa, pede autorização e instala. Se você recusar, o Mexerica oferece de novo quando for converter um documento.

## Compilar

Precisa do [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg, Ghostscript, ExifTool e qpdf em tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # extensão do botão direito (C++; baixa o llvm-mingw sozinho)
dotnet build src -c Release                                             # para testar
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # versão pronta em dist\ e o zip da Release
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # bateria de testes sem tela
```

O próprio programa também tem um modo de teste sem tela:

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## Licença

Código sob a licença MIT. As bibliotecas e programas externos usados estão listados, com as suas licenças, em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
