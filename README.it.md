[Português (Brasil)](README.md) · [English](README.en.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · **Italiano** · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md) · [Polski](README.pl.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">Converti e modifica file senza aprire nessun programma: trascina il file, premi <b>Spazio</b> e rilascialo sull'opzione.</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="Ruota Convertire">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="Ruota Strumenti">
  <img src="assets/telas/roda-acoes.png" width="260" alt="Ruota Azioni">
</p>

Mexerica è una versione per Windows dell'idea di [Tangerine](https://tangerineformac.com), per Mac. Resta nell'area di notifica. Quando trascini uno o più file e premi Spazio senza lasciare il mouse, intorno al cursore compare una ruota con quello che si può fare con quei file. Il risultato viene salvato accanto all'originale, che non viene mai modificato. Le conversioni avvengono sul tuo computer e nessun tuo file viene inviato su internet. Mexerica usa internet solo per cercare nuove versioni e per scaricare quello che trascini da un sito.

**Novità della versione 1.1:** molti più formati (RAW, PSD, Kindle, Outlook, sottotitoli, font, 3D, mappe, CAD, ISO, DMG e altri), Word, Excel e PowerPoint senza LibreOffice, cartelle che convertono da sole, conversioni recenti, ruota personalizzabile con preimpostazioni, trascinamento diretto dal browser, 13 lingue, riga di comando e avviso di aggiornamento. Vedi [come usare ogni funzione](#funzioni-e-dove-si-trovano).

**Lingua:** Mexerica si apre in portoghese. Per passare all'italiano, apri **Configurações** (Impostazioni) e scegli **Idioma** (Lingua).

## Come si usa

| Azione | Cosa fare |
|---|---|
| Aprire la ruota | Trascina i file e, senza lasciarli, premi **Spazio** |
| Cambiare ruota | Di nuovo **Spazio**, oppure **Maiusc** (Convertire), **Ctrl** (Strumenti), **Alt** (Azioni) |
| Scegliere | Rilascia il file sull'opzione, oppure usa le **frecce** e **Invio** |
| Annullare | **Esc** |
| Da tastiera | Seleziona i file in Esplora file o sul desktop e premi **Ctrl + Maiusc + Spazio** |
| Schermo touch | Tieni il dito fermo un istante e trascina |
| Tasto destro | Seleziona i file, fai clic con il tasto destro e usa il sottomenu **Mexerica** (su Windows 11, in "Mostra più opzioni") |

Più file vengono elaborati nello stesso momento. Cambiare solo il formato del video (MKV, MOV, MP4) si fa senza ricodificare quando è possibile, in pochi secondi e senza perdere qualità. Quando serve ricodificare, Mexerica usa la scheda video (NVIDIA, Intel o AMD), se c'è.

Tasti, qualità e cartella di destinazione si cambiano nelle Impostazioni (doppio clic sull'icona nell'area di notifica, oppure apri di nuovo Mexerica).

## Cosa fa

**Convertire**
- Immagini: JPG, PNG, WebP, HEIC, AVIF, JPEG XL, JPEG 2000, TIFF, BMP, GIF, APNG, ICO, ICNS, PSD, TGA, DDS, QOI, PCX, SVG (vettorizzando l'immagine) e PDF
- Immagini solo in ingresso: RAW di fotocamera (CR2, CR3, NEF, ARW, DNG e altri), PSB, DICOM, EXR, HDR, XCF, JPEG XR, CUR, PPM, EPS, PS, AI, EMF, WMF e SVGZ
- Audio: MP3, M4A (AAC e ALAC), WAV, FLAC, OGG, Opus, AIFF, WMA, WavPack, TTA, AC3, E-AC3 e W64 (legge anche APE, DSF, DFF, DTS, AMR, MKA, CAF, AU e MPC)
- Video: MP4 (H.264, HEVC o AV1), MOV, MKV, WebM, AVI, WMV, GIF e WebP animati, APNG, MP4 720p e MP3 (legge anche VOB, OGV, MXF, ASF, RM/RMVB, DivX e F4V)
- Live Photo dell'iPhone e Motion Photo di Android: estrae il video dalla foto
- PDF in DOCX, PPTX, XLSX, EPUB, TXT, Markdown, JPG e PNG (tutte le pagine)
- Documenti, fogli di calcolo e presentazioni di Office, con o senza LibreOffice; HWP e HWPX; HTML, Markdown, RTF e ODT
- E-book: EPUB, KEPUB (Kobo), MOBI, AZW, AZW3, FB2, HTMLZ e TXTZ; fumetti CBZ, CBR, CB7 e CBT in PDF ed EPUB
- E-mail EML e MSG (Outlook) in HTML, TXT o PDF, con gli allegati in uno ZIP
- Sottotitoli: SRT, ASS/SSA, VTT, SMI, LRC, TTML, SUB e SCC, conservando gli accenti dei file vecchi
- Dati: JSON, JSONL, YAML, TOML, XML, CSV e TSV tra loro; contatti vCard in foglio di calcolo e ritorno
- Font: TTF, OTF, WOFF, WOFF2 ed EOT
- Modelli 3D: GLB, glTF, STL, OBJ, 3MF e PLY (FBX in ingresso) e anteprima come immagine
- Mappe: GeoJSON, KML, KMZ, GPX, Shapefile e CSV con coordinate
- CAD: DWG e DXF tra loro e in PDF, SVG e immagine; SVG in DXF e DWG
- Archivi compressi: ZIP, 7Z, TAR, TAR.GZ, TAR.XZ e TAR.BZ2 (legge anche RAR, ISO, CAB, DMG, WIM, ARJ, LZH, CPIO, RPM, DEB, ZST, LZ, LZMA e Z)

La ruota offre solo quello che ha senso per quel file: un video senza audio non offre MP3, una foto ferma non offre APNG, un foglio di calcolo che non è di contatti non offre vCard.

**Strumenti**
- Comprimere immagini, video, audio e PDF per livello, per percentuale o fino a una dimensione esatta
- Editor di foto: ritagliare, regolare il colore, frecce, rettangoli, penna, testo e oscuramento (nero, sfocatura o pixel)
- Sfondo e margine per le immagini, collage di più foto, immagini in PDF, scala di grigi, lettura di QR Code
- Metadati: vedere tutto, modificare titolo, autore e altri campi, cancellare la posizione o tutto
- Video: tagliare un tratto, ritagliare l'immagine, velocità, dividere, estrarre un fotogramma o tutti, estrarre l'audio e i sottotitoli, ruotare, cambiare la risoluzione, togliere l'audio, unire
- Audio: tagliare un tratto, normalizzare il volume, bip sopra alcuni tratti, mono e stereo, velocità, dividere, unire
- PDF: organizzare le pagine (riordinare, ruotare, eliminare), dividere, unire e oscurare davvero (il testo nascosto sparisce)
- Sottotitoli: ritardare o anticipare i tempi; font: foglio di esempio; archivi: estrarre e verificare l'integrità

**Azioni**: condividere (Condivisione nelle vicinanze di Windows), lasciare fluttuante sullo schermo, copiare o spostare in una cartella, copiare il percorso e aprire il terminale nella cartella.

## Funzioni e dove si trovano

Le Impostazioni si aprono con doppio clic sull'icona di Mexerica vicino all'orologio, oppure aprendo di nuovo Mexerica dal menu Start. Il menu dell'icona (tasto destro) contiene il resto.

| Funzione | A cosa serve | Dove si trova |
|---|---|---|
| **Cartelle monitorate** | Tutto quello che arriva in una cartella viene convertito da solo. Es.: ogni foto HEIC che arriva in Download diventa JPG | Impostazioni → Risultato → Cartelle monitorate → **Scegli…** → **Aggiungi cartella…**, scegli il formato e salva |
| **Conversioni recenti** | Elenco di quello che è stato fatto, con ricerca, miniature e pulsanti per aprire, aprire con un altro programma o mostrare nella cartella | Tasto destro sull'icona → **Conversioni recenti…** |
| **Personalizzare la ruota** | Nascondere quello che non usi e cambiare l'ordine degli spicchi | Impostazioni → Aspetto della ruota → **Personalizza…** |
| **Preimpostazioni** | Uno spicchio tuo sulla ruota. Es.: "Sito" = JPG, qualità 80, fino a 1600 px, senza posizione; oppure "MP3 leggero" | Nella stessa finestra: **Nuova preimpostazione…** |
| **Tema e trasparenza** | Ruota chiara, scura o come Windows, più o meno trasparente | Impostazioni → Aspetto della ruota → Tema e Opacità |
| **Ruota con più facce** | Quando ci sono molte opzioni, la ruota ha più di una faccia | **Tab** o la rotellina del mouse con la ruota aperta |
| **Direttamente dal browser** | Convertire un'immagine o un file di un sito senza scaricarlo prima | Trascina l'immagine o il link dal sito, premi **Spazio** e rilascia sull'opzione. Il file finisce in Download |
| **Lingua** | Portoghese, inglese, spagnolo, francese, tedesco, italiano, giapponese, coreano, cinese, russo, polacco, hindi e arabo. Mexerica si apre in portoghese | **Configurações** (Impostazioni) → **Idioma** (Lingua) |
| **Aggiornamenti** | Avviso vicino all'orologio quando esce una nuova versione; un clic aggiorna. Il file scaricato viene verificato (SHA-256) prima dell'installazione | Impostazioni → Aggiornamenti (controlla ora o disattiva l'avviso) |
| **Riga di comando** | Convertire dal Prompt dei comandi o negli script | Vedi [Riga di comando](#riga-di-comando) |

I collegamenti (.lnk) vengono seguiti fino al file vero, e un file vuoto viene segnalato prima di provare.

## Installare

1. Scarica `Mexerica-<versão>-instalador.exe` dalla pagina **Releases** (a destra, in questa pagina).
2. Apri il file scaricato e fai clic su **Avançar** (Avanti) fino alla fine. Il programma di installazione è in portoghese. Non serve la password di amministratore.
3. Se Windows mostra "PC protetto da Windows", fai clic su **Ulteriori informazioni** e poi su **Esegui comunque**. L'avviso compare perché il programma di installazione non ha una firma digitale a pagamento, non perché ci sia qualcosa che non va.

Alla fine, una finestra spiega come usarlo. Mexerica si apre da solo quando il computer si accende (si può disattivare dal menu dell'area di notifica). Per rimuoverlo: **Impostazioni → App → App installate → Mexerica → Disinstalla**. In questo modo spariscono anche il menu del tasto destro e le impostazioni.

Requisiti: Windows 10 (versione 2004 o più recente) o Windows 11, 64 bit. Non serve installare nient'altro. [LibreOffice](https://pt-br.libreoffice.org) è facoltativo: con lui, documenti, fogli di calcolo e presentazioni escono più fedeli all'originale. Se manca, il programma di installazione lo propone; se rifiuti, Mexerica converte per conto suo.

## Riga di comando

Durante l'installazione, spunta **Usar o comando "mexerica" no Prompt de Comando e no PowerShell** (Usa il comando "mexerica" nel Prompt dei comandi e in PowerShell) per poter chiamare `mexerica` da qualsiasi cartella:

```powershell
mexerica converter *.heic --para jpg                      # converte tutto in JPG, accanto agli originali
mexerica converter video.mov --para mp4 -o D:\Saida      # sceglie la cartella di destinazione
mexerica opcoes planilha.xlsx                             # elenca cosa si può fare con il file
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # converte quello che arriva finché non premi Ctrl+C
```

Il codice di uscita è 0 quando è andato tutto bene, 1 quando qualche file non è riuscito e 2 quando c'è un errore nei parametri.

## Compilare

Serve il [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg, Ghostscript, ExifTool, qpdf, 7-Zip, lzip e rhwp in tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # estensione del tasto destro (C++; scarica llvm-mingw da solo)
dotnet build src -c Release                                             # per provare
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # versione pronta in dist\ e lo zip della Release
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # serie di test senza schermo
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # formati rari con file veri scaricati da archivi pubblici
```

I nuovi testi dell'interfaccia vanno tradotti: `python scripts\extrair-textos.py --check` mostra cosa manca e `python scripts\gerar-traducoes.py` crea una bozza in `src\Idiomas`, da rivedere.

Il programma stesso ha anche una modalità di test senza schermo:

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## Licenza

Codice sotto licenza MIT. Le librerie e i programmi esterni usati sono elencati, con le loro licenze, in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
