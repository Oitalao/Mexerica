# Componentes de terceiros

O código do Mexerica é MIT (veja `LICENSE`). Ele usa as bibliotecas e programas abaixo, cada um com a sua licença.

## Código adaptado do Filee

Vários conversores (legendas, dados, fontes, e-mail, e-books, 3D, mapas, CAD, imagens, Office sem LibreOffice, Live Photo e Motion Photo) foram adaptados do [Filee](https://github.com/KnifeLemon/Filee). Cada arquivo adaptado diz no topo de onde veio. Licença do Filee:

```
MIT License

Copyright (c) 2026 Myeonggu Kim (KnifeLemon) and Filee contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Bibliotecas (NuGet, embutidas no programa)

| Componente | Licença | Fonte |
|---|---|---|
| Magick.NET (ImageMagick) | Apache 2.0 | https://github.com/dlemstra/Magick.NET |
| PdfPig | Apache 2.0 | https://github.com/UglyToad/PdfPig |
| ZXing.Net | Apache 2.0 | https://github.com/micjahn/ZXing.Net |
| Markdig | BSD 2-Clause | https://github.com/xoofx/markdig |
| ReverseMarkdown | MIT | https://github.com/mysticmind/reversemarkdown-net |
| YamlDotNet | MIT | https://github.com/aaubry/YamlDotNet |
| Tomlyn | BSD 2-Clause | https://github.com/xoofx/Tomlyn |
| AngleSharp | MIT | https://github.com/AngleSharp/AngleSharp |
| ExCSS | MIT | https://github.com/TylerBrinks/ExCSS |
| MimeKit | MIT | https://github.com/jstedfast/MimeKit |
| BouncyCastle.Cryptography | MIT | https://github.com/bcgit/bc-csharp |
| MsgReader | MIT | https://github.com/Sicos1977/MSGReader |
| RtfPipe | MIT | https://github.com/erdomke/RtfPipe |
| OpenMcdf | MPL 2.0 | https://github.com/ironfede/openmcdf |
| UTF.Unknown | MPL 1.1 | https://github.com/CharsetDetector/UTF-unknown |
| PDFsharp | MIT | https://github.com/empira/PDFsharp |
| PDFtoImage | MIT | https://github.com/sungaila/PDFtoImage |
| PDFium (binários de bblanchon) | Apache 2.0 e BSD 3-Clause | https://github.com/bblanchon/pdfium-binaries |
| SharpCompress | MIT | https://github.com/adamhathcock/sharpcompress |
| ExcelDataReader | MIT | https://github.com/ExcelDataReader/ExcelDataReader |
| ExcelNumberFormat | MIT | https://github.com/andersnm/ExcelNumberFormat |
| SkiaSharp e HarfBuzzSharp | MIT (Skia: BSD 3-Clause) | https://github.com/mono/SkiaSharp |
| Svg.Skia e componentes (Svg.Custom, Svg.Model, ShimSkiaSharp) | MIT | https://github.com/wieslawsoltes/Svg.Skia |
| SharpGLTF | MIT | https://github.com/vpenades/SharpGLTF |
| ACadSharp | MIT | https://github.com/DomCR/ACadSharp |
| .NET 8 (runtime embutido na versão pronta) | MIT | https://github.com/dotnet/runtime |

## Programas externos (pasta `tools`, chamados como processos separados)

Não ficam no repositório; `scripts/baixar-ferramentas.ps1` baixa das fontes oficiais. A versão pronta (zip da Release) os inclui sem modificação.

| Programa | Licença | Fonte e código-fonte |
|---|---|---|
| FFmpeg (build GPL de BtbN) | GPL 3.0 | https://github.com/BtbN/FFmpeg-Builds e https://ffmpeg.org |
| Ghostscript | AGPL 3.0 | https://ghostscript.com e https://github.com/ArtifexSoftware/ghostpdl |
| ExifTool (Phil Harvey; versão Windows de Oliver Betz) | Artistic License ou GPL | https://exiftool.org |
| qpdf | Apache 2.0 | https://github.com/qpdf/qpdf |
| 7-Zip 26.05 (Igor Pavlov) | LGPL 2.1, com a restrição do unRAR para o código de RAR | https://www.7-zip.org e https://github.com/ip7z/7zip |
| Lzip 1.22 (Antonio Diaz Diaz) | GPL 2.0 ou posterior; o código-fonte vai junto, em `tools\lzip` | https://www.nongnu.org/lzip |
| rhwp 0.8.7 | MIT | https://github.com/edwardkim/rhwp |
| Documento-modelo `blank.hwpx` (pypandoc-hwpx) | MIT | https://github.com/msjang/pypandoc-hwpx |

## Usados se estiverem instalados no computador

| Programa | Para quê |
|---|---|
| LibreOffice | converter documentos, planilhas e apresentações com mais fidelidade (sem ele, o Mexerica converte por conta própria) |
| Extensões de Imagem HEIF e de Vídeo HEVC da Microsoft | gravar HEIC |
