[Português (Brasil)](README.md) · **English** · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md) · [Polski](README.pl.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">Convert and edit files without opening any program: drag the file, press <b>Space</b> and drop it on the option.</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="Convert wheel">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="Tools wheel">
  <img src="assets/telas/roda-acoes.png" width="260" alt="Actions wheel">
</p>

Mexerica is a Windows version of the idea behind [Tangerine](https://tangerineformac.com), for the Mac. It sits in the system tray. When you drag one or more files and press Space without letting go of the mouse, a wheel appears around the cursor showing what you can do with them. The result is saved next to the original, which is never changed. Conversions happen on your computer and none of your files are sent to the internet. Mexerica only uses the internet to check for new versions and to download what you drag from a website.

**New in version 1.1:** many more formats (RAW, PSD, Kindle, Outlook, subtitles, fonts, 3D, maps, CAD, ISO, DMG and others), Word, Excel and PowerPoint without LibreOffice, folders that convert on their own, recent conversions, a customizable wheel with presets, dragging straight from the browser, 13 languages, a command line and update notices. See [how to use each feature](#features-and-where-to-find-them).

**Language:** Mexerica opens in Portuguese. To switch to English, open **Configurações** (Settings) and choose **Idioma** (Language).

## How to use

| Action | What to do |
|---|---|
| Open the wheel | Drag files and, without letting go, press **Space** |
| Switch wheels | **Space** again, or **Shift** (Convert), **Ctrl** (Tools), **Alt** (Actions) |
| Choose | Drop the file on the option, or use the **arrow keys** and **Enter** |
| Cancel | **Esc** |
| With the keyboard | Select files in File Explorer or on the desktop and press **Ctrl + Shift + Space** |
| Touch screen | Hold your finger still for a moment and drag |
| Right click | Select the files, right click and use the **Mexerica** submenu (on Windows 11, under "Show more options") |

Several files are processed at the same time. Changing only the video container (MKV, MOV, MP4) is done without re-encoding when possible, in seconds and with no quality loss. When re-encoding is needed, Mexerica uses the graphics card (NVIDIA, Intel or AMD), if there is one.

Keys, quality and output folder can be changed in Settings (double click the tray icon, or open Mexerica again).

## What it does

**Convert**
- Images: JPG, PNG, WebP, HEIC, AVIF, JPEG XL, JPEG 2000, TIFF, BMP, GIF, APNG, ICO, ICNS, PSD, TGA, DDS, QOI, PCX, SVG (by vectorizing the image) and PDF
- Images as input only: camera RAW (CR2, CR3, NEF, ARW, DNG and others), PSB, DICOM, EXR, HDR, XCF, JPEG XR, CUR, PPM, EPS, PS, AI, EMF, WMF and SVGZ
- Audio: MP3, M4A (AAC and ALAC), WAV, FLAC, OGG, Opus, AIFF, WMA, WavPack, TTA, AC3, E-AC3 and W64 (also reads APE, DSF, DFF, DTS, AMR, MKA, CAF, AU and MPC)
- Video: MP4 (H.264, HEVC or AV1), MOV, MKV, WebM, AVI, WMV, animated GIF and WebP, APNG, MP4 720p and MP3 (also reads VOB, OGV, MXF, ASF, RM/RMVB, DivX and F4V)
- iPhone Live Photo and Android Motion Photo: pulls the video out of the photo
- PDF to DOCX, PPTX, XLSX, EPUB, TXT, Markdown, JPG and PNG (all pages)
- Office documents, spreadsheets and presentations, with or without LibreOffice; HWP and HWPX; HTML, Markdown, RTF and ODT
- E-books: EPUB, KEPUB (Kobo), MOBI, AZW, AZW3, FB2, HTMLZ and TXTZ; CBZ, CBR, CB7 and CBT comics to PDF and EPUB
- EML and MSG (Outlook) emails to HTML, TXT or PDF, with the attachments in a ZIP
- Subtitles: SRT, ASS/SSA, VTT, SMI, LRC, TTML, SUB and SCC, keeping the accented letters of old files
- Data: JSON, JSONL, YAML, TOML, XML, CSV and TSV between each other; vCard contacts to a spreadsheet and back
- Fonts: TTF, OTF, WOFF, WOFF2 and EOT
- 3D models: GLB, glTF, STL, OBJ, 3MF and PLY (FBX as input) and an image preview
- Maps: GeoJSON, KML, KMZ, GPX, Shapefile and CSV with coordinates
- CAD: DWG and DXF between each other and to PDF, SVG and image; SVG to DXF and DWG
- Archives: ZIP, 7Z, TAR, TAR.GZ, TAR.XZ and TAR.BZ2 (also reads RAR, ISO, CAB, DMG, WIM, ARJ, LZH, CPIO, RPM, DEB, ZST, LZ, LZMA and Z)

The wheel only offers what makes sense for that file: a video without sound does not offer MP3, a still photo does not offer APNG, a spreadsheet that is not a contact list does not offer vCard.

**Tools**
- Compress images, videos, audio and PDFs by level, by percentage or to an exact size
- Photo editor: crop, adjust color, arrows, rectangles, pen, text and redaction (black, blur or pixel)
- Background and margin for images, collage of several photos, images to PDF, grayscale, QR Code reading
- Metadata: see everything, edit title, author and other fields, remove the location or everything
- Video: cut a section, crop the picture, speed, split, take one frame or all of them, extract the audio and subtitles, rotate, change the resolution, remove the sound, join
- Audio: cut a section, normalize the volume, beep over sections, mono and stereo, speed, split, join
- PDF: organize pages (reorder, rotate, delete), split, join and redact for real (the hidden text is removed)
- Subtitles: shift the timing earlier or later; fonts: sample sheet; archives: extract and test integrity

**Actions**: share (Windows Nearby Sharing), keep floating on the screen, copy or move to a folder, copy the path and open the terminal in the folder.

## Features and where to find them

Settings opens with a double click on the Mexerica icon near the clock, or by opening Mexerica again from the Start menu. The icon menu (right click) has everything else.

| Feature | What it is for | Where to find it |
|---|---|---|
| **Watched Folders** | Everything that lands in a folder is converted on its own. Example: every HEIC photo that arrives in Downloads becomes a JPG | Settings → Result → Watched Folders → **Choose…** → **Add folder…**, choose the format and save |
| **Recent conversions** | A list of what was done, with search, thumbnails and buttons to open, open with another program or show in the folder | Right click the icon → **Recent conversions…** |
| **Customize the wheel** | Hide what you do not use and change the order of the slices | Settings → Wheel appearance → **Customize…** |
| **Presets** | Your own slice on the wheel. Example: "Website" = JPG, quality 80, up to 1600 px, no location; or "Light MP3" | In the same window: **New preset…** |
| **Theme and transparency** | Light, dark or same as Windows wheel, more or less transparent | Settings → Wheel appearance → Theme and Opacity |
| **Wheel with sides** | When there are many options, the wheel gets more than one side | **Tab** or the mouse wheel while the wheel is open |
| **Straight from the browser** | Convert an image or a file from a website without downloading it first | Drag the image or the link from the website, press **Space** and drop it on the option. The file ends up in Downloads |
| **Language** | Portuguese, English, Spanish, French, German, Italian, Japanese, Korean, Chinese, Russian, Polish, Hindi and Arabic. Mexerica opens in Portuguese | **Configurações** (Settings) → **Idioma** (Language) |
| **Updates** | A notice near the clock when a new version comes out; one click updates. The downloaded file is checked (SHA-256) before installing | Settings → Updates (check now or turn off the notice) |
| **Command line** | Convert from the Command Prompt or in scripts | See [Command line](#command-line) |

Shortcuts (.lnk) are followed to the real file, and an empty file is reported before trying.

## Install

1. Download `Mexerica-<versão>-instalador.exe` from the **Releases** page (on the right side of this page).
2. Open the downloaded file and click **Avançar** (Next) until the end. The installer is in Portuguese. No administrator password is needed.
3. If Windows shows "Windows protected your PC", click **More info** and then **Run anyway**. The warning appears because the installer does not have a paid digital signature, not because something is wrong.

When it finishes, a window explains how to use it. Mexerica opens on its own when the computer starts (you can turn this off in the tray menu). To remove it: **Settings → Apps → Installed apps → Mexerica → Uninstall**. This also removes the right click menu and the settings.

Requirements: Windows 10 (version 2004 or newer) or Windows 11, 64 bit. Nothing else needs to be installed. [LibreOffice](https://pt-br.libreoffice.org) is optional: with it, documents, spreadsheets and presentations come out closer to the original. If it is missing, the installer offers it; if you say no, Mexerica converts on its own.

## Command line

During installation, check **Usar o comando "mexerica" no Prompt de Comando e no PowerShell** (Use the "mexerica" command in the Command Prompt and in PowerShell) so you can call `mexerica` from any folder:

```powershell
mexerica converter *.heic --para jpg                      # converts everything to JPG, next to the originals
mexerica converter video.mov --para mp4 -o D:\Saida      # chooses the output folder
mexerica opcoes planilha.xlsx                             # lists what can be done with the file
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # converts whatever arrives until you press Ctrl+C
```

The exit code is 0 when everything went well, 1 when some file failed and 2 when there is an error in the parameters.

## Build

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg, Ghostscript, ExifTool, qpdf, 7-Zip, lzip and rhwp in tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # right click extension (C++; downloads llvm-mingw on its own)
dotnet build src -c Release                                             # for testing
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # ready version in dist\ and the Release zip
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # test suite without a screen
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # rare formats with real files downloaded from public collections
```

New interface texts need translation: `python scripts\extrair-textos.py --check` shows what is missing and `python scripts\gerar-traducoes.py` creates a draft in `src\Idiomas`, which must be reviewed.

The program itself also has a test mode without a screen:

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## License

Code under the MIT license. The external libraries and programs used are listed, with their licenses, in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
