# Componentes de terceiros

O código do Mexerica é MIT (veja `LICENSE`). Ele usa as bibliotecas e programas abaixo, cada um com a sua licença.

## Bibliotecas (NuGet, embutidas no programa)

| Componente | Licença | Fonte |
|---|---|---|
| Magick.NET (ImageMagick) | Apache 2.0 | https://github.com/dlemstra/Magick.NET |
| PdfPig | Apache 2.0 | https://github.com/UglyToad/PdfPig |
| ZXing.Net | Apache 2.0 | https://github.com/micjahn/ZXing.Net |
| Markdig | BSD 2-Clause | https://github.com/xoofx/markdig |
| ReverseMarkdown | MIT | https://github.com/mysticmind/reversemarkdown-net |
| .NET 8 (runtime embutido na versão pronta) | MIT | https://github.com/dotnet/runtime |

## Programas externos (pasta `tools`, chamados como processos separados)

Não ficam no repositório; `scripts/baixar-ferramentas.ps1` baixa das fontes oficiais. A versão pronta (zip da Release) os inclui sem modificação.

| Programa | Licença | Fonte e código-fonte |
|---|---|---|
| FFmpeg (build GPL de BtbN) | GPL 3.0 | https://github.com/BtbN/FFmpeg-Builds e https://ffmpeg.org |
| Ghostscript | AGPL 3.0 | https://ghostscript.com e https://github.com/ArtifexSoftware/ghostpdl |
| ExifTool (Phil Harvey; versão Windows de Oliver Betz) | Artistic License ou GPL | https://exiftool.org |
| qpdf | Apache 2.0 | https://github.com/qpdf/qpdf |

## Usados se estiverem instalados no computador

| Programa | Para quê |
|---|---|
| LibreOffice | converter documentos, planilhas e apresentações |
| Extensões de Imagem HEIF e de Vídeo HEVC da Microsoft | gravar HEIC |
