[Português (Brasil)](README.md) · [English](README.en.md) · [Español](README.es.md) · [Français](README.fr.md) · **Deutsch** · [Italiano](README.it.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md) · [Polski](README.pl.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">Dateien umwandeln und bearbeiten, ohne ein Programm zu öffnen: Datei ziehen, <b>Leertaste</b> drücken und auf der Option loslassen.</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="Rad Konvertieren">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="Rad Werkzeuge">
  <img src="assets/telas/roda-acoes.png" width="260" alt="Rad Aktionen">
</p>

Mexerica ist eine Windows-Version der Idee von [Tangerine](https://tangerineformac.com) für den Mac. Es sitzt im Infobereich der Taskleiste. Wenn Sie eine oder mehrere Dateien ziehen und die Leertaste drücken, ohne die Maus loszulassen, erscheint um den Mauszeiger ein Rad mit allem, was man mit ihnen machen kann. Das Ergebnis wird neben dem Original gespeichert, das nie verändert wird. Die Umwandlungen laufen auf Ihrem Computer, und keine Ihrer Dateien wird ins Internet geschickt. Mexerica nutzt das Internet nur, um nach neuen Versionen zu suchen und um herunterzuladen, was Sie von einer Website ziehen.

**Neu in Version 1.1:** viel mehr Formate (RAW, PSD, Kindle, Outlook, Untertitel, Schriftarten, 3D, Karten, CAD, ISO, DMG und andere), Word, Excel und PowerPoint ohne LibreOffice, Ordner, die von selbst umwandeln, letzte Umwandlungen, ein anpassbares Rad mit Voreinstellungen, Ziehen direkt aus dem Browser, 13 Sprachen, Befehlszeile und Hinweis auf Updates. Siehe [wie man jede Funktion benutzt](#funktionen-und-wo-sie-sind).

**Sprache:** Mexerica startet auf Portugiesisch. Um auf Deutsch umzustellen, öffnen Sie **Configurações** (Einstellungen) und wählen Sie **Idioma** (Sprache).

## So benutzen Sie es

| Aktion | Was zu tun ist |
|---|---|
| Rad öffnen | Dateien ziehen und, ohne loszulassen, **Leertaste** drücken |
| Rad wechseln | Noch einmal **Leertaste**, oder **Umschalt** (Konvertieren), **Strg** (Werkzeuge), **Alt** (Aktionen) |
| Auswählen | Die Datei auf der Option loslassen, oder **Pfeiltasten** und **Eingabe** benutzen |
| Abbrechen | **Esc** |
| Mit der Tastatur | Dateien im Datei-Explorer oder auf dem Desktop markieren und **Strg + Umschalt + Leertaste** drücken |
| Touchscreen | Den Finger einen Moment still halten und dann ziehen |
| Rechtsklick | Dateien markieren, mit der rechten Maustaste klicken und das Untermenü **Mexerica** benutzen (unter Windows 11 bei "Weitere Optionen anzeigen") |

Mehrere Dateien werden gleichzeitig verarbeitet. Wenn nur das Videoformat (MKV, MOV, MP4) gewechselt wird, geschieht das wenn möglich ohne Neukodierung, in Sekunden und ohne Qualitätsverlust. Muss neu kodiert werden, nutzt Mexerica die Grafikkarte (NVIDIA, Intel oder AMD), falls vorhanden.

Tasten, Qualität und Zielordner ändern Sie in den Einstellungen (Doppelklick auf das Symbol im Infobereich, oder Mexerica noch einmal öffnen).

## Was es kann

**Konvertieren**
- Bilder: JPG, PNG, WebP, HEIC, AVIF, JPEG XL, JPEG 2000, TIFF, BMP, GIF, APNG, ICO, ICNS, PSD, TGA, DDS, QOI, PCX, SVG (durch Vektorisieren des Bildes) und PDF
- Bilder nur als Eingabe: Kamera-RAW (CR2, CR3, NEF, ARW, DNG und andere), PSB, DICOM, EXR, HDR, XCF, JPEG XR, CUR, PPM, EPS, PS, AI, EMF, WMF und SVGZ
- Audio: MP3, M4A (AAC und ALAC), WAV, FLAC, OGG, Opus, AIFF, WMA, WavPack, TTA, AC3, E-AC3 und W64 (liest auch APE, DSF, DFF, DTS, AMR, MKA, CAF, AU und MPC)
- Video: MP4 (H.264, HEVC oder AV1), MOV, MKV, WebM, AVI, WMV, animierte GIF und WebP, APNG, MP4 720p und MP3 (liest auch VOB, OGV, MXF, ASF, RM/RMVB, DivX und F4V)
- Live Photo vom iPhone und Motion Photo von Android: holt das Video aus dem Foto
- PDF nach DOCX, PPTX, XLSX, EPUB, TXT, Markdown, JPG und PNG (alle Seiten)
- Office-Dokumente, Tabellen und Präsentationen, mit oder ohne LibreOffice; HWP und HWPX; HTML, Markdown, RTF und ODT
- E-Books: EPUB, KEPUB (Kobo), MOBI, AZW, AZW3, FB2, HTMLZ und TXTZ; Comics CBZ, CBR, CB7 und CBT nach PDF und EPUB
- E-Mails EML und MSG (Outlook) nach HTML, TXT oder PDF, mit den Anhängen in einer ZIP-Datei
- Untertitel: SRT, ASS/SSA, VTT, SMI, LRC, TTML, SUB und SCC, wobei Sonderzeichen alter Dateien erhalten bleiben
- Daten: JSON, JSONL, YAML, TOML, XML, CSV und TSV untereinander; vCard-Kontakte in eine Tabelle und zurück
- Schriftarten: TTF, OTF, WOFF, WOFF2 und EOT
- 3D-Modelle: GLB, glTF, STL, OBJ, 3MF und PLY (FBX als Eingabe) und eine Bildvorschau
- Karten: GeoJSON, KML, KMZ, GPX, Shapefile und CSV mit Koordinaten
- CAD: DWG und DXF untereinander und nach PDF, SVG und Bild; SVG nach DXF und DWG
- Archive: ZIP, 7Z, TAR, TAR.GZ, TAR.XZ und TAR.BZ2 (liest auch RAR, ISO, CAB, DMG, WIM, ARJ, LZH, CPIO, RPM, DEB, ZST, LZ, LZMA und Z)

Das Rad bietet nur an, was für diese Datei sinnvoll ist: ein Video ohne Ton bietet kein MP3 an, ein unbewegtes Foto kein APNG, eine Tabelle, die keine Kontaktliste ist, kein vCard.

**Werkzeuge**
- Bilder, Videos, Audio und PDF komprimieren, nach Stufe, nach Prozent oder auf eine genaue Größe
- Fotoeditor: zuschneiden, Farbe anpassen, Pfeile, Rechtecke, Stift, Text und Schwärzen (schwarz, unscharf oder verpixelt)
- Hintergrund und Rand für Bilder, Collage aus mehreren Fotos, Bilder zu PDF, Graustufen, QR-Code lesen
- Metadaten: alles ansehen, Titel, Autor und andere Felder bearbeiten, den Standort oder alles löschen
- Video: einen Abschnitt schneiden, das Bild zuschneiden, Geschwindigkeit, teilen, ein Einzelbild oder alle speichern, Ton und Untertitel herausziehen, drehen, Auflösung ändern, Ton entfernen, zusammenfügen
- Audio: einen Abschnitt schneiden, Lautstärke angleichen, Piepton über Abschnitte legen, Mono und Stereo, Geschwindigkeit, teilen, zusammenfügen
- PDF: Seiten ordnen (umsortieren, drehen, löschen), teilen, zusammenfügen und wirklich schwärzen (der verdeckte Text verschwindet)
- Untertitel: Zeit verzögern oder vorziehen; Schriftarten: Musterblatt; Archive: entpacken und auf Fehler prüfen

**Aktionen**: teilen (Windows-Umgebungsfreigabe), auf dem Bildschirm schweben lassen, in einen Ordner kopieren oder verschieben, den Pfad kopieren und das Terminal im Ordner öffnen.

## Funktionen und wo sie sind

Die Einstellungen öffnen sich mit einem Doppelklick auf das Mexerica-Symbol neben der Uhr, oder indem Sie Mexerica noch einmal über das Startmenü öffnen. Das Menü des Symbols (Rechtsklick) enthält den Rest.

| Funktion | Wofür | Wo |
|---|---|---|
| **Überwachte Ordner** | Alles, was in einem Ordner landet, wird von selbst umgewandelt. Beispiel: Jedes HEIC-Foto, das in Downloads ankommt, wird zu JPG | Einstellungen → Ergebnis → Überwachte Ordner → **Auswählen…** → **Ordner hinzufügen…**, Format wählen und speichern |
| **Aktuelle Konvertierungen** | Liste dessen, was gemacht wurde, mit Suche, Vorschaubildern und Knöpfen zum Öffnen, Öffnen mit einem anderen Programm oder Anzeigen im Ordner | Rechtsklick auf das Symbol → **Aktuelle Konvertierungen…** |
| **Rad anpassen** | Ausblenden, was Sie nicht benutzen, und die Reihenfolge der Felder ändern | Einstellungen → Aussehen des Rades → **Anpassen…** |
| **Voreinstellungen** | Ein eigenes Feld auf dem Rad. Beispiel: "Website" = JPG, Qualität 80, bis 1600 px, ohne Standort; oder "MP3 klein" | Im selben Fenster: **Neue Voreinstellung…** |
| **Thema und Transparenz** | Helles, dunkles oder an Windows angepasstes Rad, mehr oder weniger durchsichtig | Einstellungen → Aussehen des Rades → Thema und Deckkraft |
| **Rad mit Seiten** | Bei vielen Optionen bekommt das Rad mehr als eine Seite | **Tab** oder Mausrad, während das Rad offen ist |
| **Direkt aus dem Browser** | Ein Bild oder eine Datei von einer Website umwandeln, ohne sie vorher herunterzuladen | Bild oder Link von der Website ziehen, **Leertaste** drücken und auf der Option loslassen. Die Datei landet in Downloads |
| **Sprache** | Portugiesisch, Englisch, Spanisch, Französisch, Deutsch, Italienisch, Japanisch, Koreanisch, Chinesisch, Russisch, Polnisch, Hindi und Arabisch. Mexerica startet auf Portugiesisch | **Configurações** (Einstellungen) → **Idioma** (Sprache) |
| **Aktualisierungen** | Hinweis neben der Uhr, wenn eine neue Version erscheint; ein Klick aktualisiert. Die heruntergeladene Datei wird vor der Installation geprüft (SHA-256) | Einstellungen → Aktualisierungen (jetzt prüfen oder den Hinweis abschalten) |
| **Befehlszeile** | Umwandeln über die Eingabeaufforderung oder in Skripten | Siehe [Befehlszeile](#befehlszeile) |

Verknüpfungen (.lnk) werden bis zur echten Datei verfolgt, und bei einer leeren Datei gibt es vorher einen Hinweis.

## Installieren

1. Laden Sie `Mexerica-<versão>-instalador.exe` auf der Seite **Releases** herunter (rechts auf dieser Seite).
2. Öffnen Sie die heruntergeladene Datei und klicken Sie bis zum Ende auf **Avançar** (Weiter). Das Installationsprogramm ist auf Portugiesisch. Ein Administratorkennwort ist nicht nötig.
3. Wenn Windows "Der Computer wurde durch Windows geschützt" anzeigt, klicken Sie auf **Weitere Informationen** und dann auf **Trotzdem ausführen**. Die Warnung erscheint, weil das Installationsprogramm keine bezahlte digitale Signatur hat, nicht weil etwas nicht stimmt.

Am Ende erklärt ein Fenster die Benutzung. Mexerica startet von selbst, wenn der Computer hochfährt (das lässt sich im Menü des Infobereichs abschalten). Zum Entfernen: **Einstellungen → Apps → Installierte Apps → Mexerica → Deinstallieren**. Damit verschwinden auch das Rechtsklickmenü und die Einstellungen.

Voraussetzungen: Windows 10 (Version 2004 oder neuer) oder Windows 11, 64 Bit. Sonst muss nichts installiert werden. [LibreOffice](https://pt-br.libreoffice.org) ist optional: Damit werden Dokumente, Tabellen und Präsentationen dem Original ähnlicher. Fehlt es, bietet das Installationsprogramm es an; wenn Sie ablehnen, wandelt Mexerica selbst um.

## Befehlszeile

Setzen Sie bei der Installation den Haken bei **Usar o comando "mexerica" no Prompt de Comando e no PowerShell** (Den Befehl "mexerica" in der Eingabeaufforderung und in PowerShell verwenden), damit Sie `mexerica` aus jedem Ordner aufrufen können:

```powershell
mexerica converter *.heic --para jpg                      # wandelt alles in JPG um, neben den Originalen
mexerica converter video.mov --para mp4 -o D:\Saida      # wählt den Zielordner
mexerica opcoes planilha.xlsx                             # zeigt, was man mit der Datei machen kann
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # wandelt alles Ankommende um, bis Strg+C gedrückt wird
```

Der Rückgabewert ist 0, wenn alles geklappt hat, 1, wenn eine Datei fehlgeschlagen ist, und 2 bei einem Fehler in den Parametern.

## Kompilieren

Sie brauchen das [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg, Ghostscript, ExifTool, qpdf, 7-Zip, lzip und rhwp in tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # Rechtsklick-Erweiterung (C++; lädt llvm-mingw selbst herunter)
dotnet build src -c Release                                             # zum Testen
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # fertige Version in dist\ und das Zip für die Release
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # Testreihe ohne Bildschirm
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # seltene Formate mit echten Dateien aus öffentlichen Sammlungen
```

Neue Texte der Oberfläche brauchen eine Übersetzung: `python scripts\extrair-textos.py --check` zeigt, was fehlt, und `python scripts\gerar-traducoes.py` erstellt einen Entwurf in `src\Idiomas`, der geprüft werden muss.

Das Programm selbst hat auch einen Testmodus ohne Bildschirm:

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## Lizenz

Code unter der MIT-Lizenz. Die verwendeten externen Bibliotheken und Programme sind mit ihren Lizenzen in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) aufgeführt.
