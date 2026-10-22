[Português (Brasil)](README.md) · [English](README.en.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md) · **Polski** · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">Konwertuj i edytuj pliki bez otwierania żadnego programu: przeciągnij plik, naciśnij <b>Spację</b> i upuść go na wybranej opcji.</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="Koło Konwertuj">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="Koło Narzędzia">
  <img src="assets/telas/roda-acoes.png" width="260" alt="Koło Działania">
</p>

Mexerica to wersja dla Windows pomysłu z [Tangerine](https://tangerineformac.com) na Maca. Siedzi w obszarze powiadomień paska zadań. Gdy przeciągasz jeden lub więcej plików i naciśniesz Spację, nie puszczając myszy, wokół kursora pojawia się koło z tym, co można z nimi zrobić. Wynik zapisuje się obok oryginału, który nigdy nie jest zmieniany. Konwersje odbywają się na Twoim komputerze i żaden Twój plik nie jest wysyłany do internetu. Mexerica korzysta z internetu tylko po to, by sprawdzić nowe wersje i pobrać to, co przeciągniesz ze strony.

**Nowość w wersji 1.1:** dużo więcej formatów (RAW, PSD, Kindle, Outlook, napisy, czcionki, 3D, mapy, CAD, ISO, DMG i inne), Word, Excel i PowerPoint bez LibreOffice, foldery, które konwertują same, ostatnie konwersje, koło do dostosowania z ustawieniami wstępnymi, przeciąganie prosto z przeglądarki, 13 języków, wiersz poleceń i powiadomienia o aktualizacjach. Zobacz, [jak używać każdej funkcji](#funkcje-i-gdzie-je-znaleźć).

**Język:** Mexerica otwiera się po portugalsku. Aby przełączyć na polski, otwórz **Configurações** (Ustawienia) i wybierz **Idioma** (Język).

## Jak używać

| Czynność | Co zrobić |
|---|---|
| Otworzyć koło | Przeciągnij pliki i, nie puszczając, naciśnij **Spację** |
| Zmienić koło | Jeszcze raz **Spacja**, albo **Shift** (Konwertuj), **Ctrl** (Narzędzia), **Alt** (Działania) |
| Wybrać | Upuść plik na opcji albo użyj **strzałek** i **Enter** |
| Anulować | **Esc** |
| Klawiaturą | Zaznacz pliki w Eksploratorze plików lub na pulpicie i naciśnij **Ctrl + Shift + Spacja** |
| Ekran dotykowy | Przytrzymaj palec chwilę w miejscu i przeciągnij |
| Prawy przycisk | Zaznacz pliki, kliknij prawym przyciskiem i użyj podmenu **Mexerica** (w Windows 11 w "Pokaż więcej opcji") |

Wiele plików jest przetwarzanych jednocześnie. Zmiana samego formatu wideo (MKV, MOV, MP4) odbywa się bez ponownego kodowania, gdy to możliwe: w kilka sekund i bez utraty jakości. Gdy trzeba kodować od nowa, Mexerica używa karty graficznej (NVIDIA, Intel lub AMD), jeśli jest.

Klawisze, jakość i folder wyników zmienia się w Ustawieniach (podwójne kliknięcie ikony w obszarze powiadomień albo ponowne uruchomienie Mexerica).

## Co potrafi

**Konwertuj**
- Obrazy: JPG, PNG, WebP, HEIC, AVIF, JPEG XL, JPEG 2000, TIFF, BMP, GIF, APNG, ICO, ICNS, PSD, TGA, DDS, QOI, PCX, SVG (przez wektoryzację obrazu) i PDF
- Obrazy tylko na wejściu: RAW z aparatów (CR2, CR3, NEF, ARW, DNG i inne), PSB, DICOM, EXR, HDR, XCF, JPEG XR, CUR, PPM, EPS, PS, AI, EMF, WMF i SVGZ
- Dźwięk: MP3, M4A (AAC i ALAC), WAV, FLAC, OGG, Opus, AIFF, WMA, WavPack, TTA, AC3, E-AC3 i W64 (czyta też APE, DSF, DFF, DTS, AMR, MKA, CAF, AU i MPC)
- Wideo: MP4 (H.264, HEVC lub AV1), MOV, MKV, WebM, AVI, WMV, animowane GIF i WebP, APNG, MP4 720p i MP3 (czyta też VOB, OGV, MXF, ASF, RM/RMVB, DivX i F4V)
- Live Photo z iPhone'a i Motion Photo z Androida: wyciąga wideo ze zdjęcia
- PDF do DOCX, PPTX, XLSX, EPUB, TXT, Markdown, JPG i PNG (wszystkie strony)
- Dokumenty, arkusze i prezentacje Office, z LibreOffice lub bez; HWP i HWPX; HTML, Markdown, RTF i ODT
- E-booki: EPUB, KEPUB (Kobo), MOBI, AZW, AZW3, FB2, HTMLZ i TXTZ; komiksy CBZ, CBR, CB7 i CBT do PDF i EPUB
- E-maile EML i MSG (Outlook) do HTML, TXT lub PDF, z załącznikami w pliku ZIP
- Napisy: SRT, ASS/SSA, VTT, SMI, LRC, TTML, SUB i SCC, z zachowaniem polskich znaków ze starych plików
- Dane: JSON, JSONL, YAML, TOML, XML, CSV i TSV między sobą; kontakty vCard do arkusza i z powrotem
- Czcionki: TTF, OTF, WOFF, WOFF2 i EOT
- Modele 3D: GLB, glTF, STL, OBJ, 3MF i PLY (FBX tylko na wejściu) oraz podgląd jako obraz
- Mapy: GeoJSON, KML, KMZ, GPX, Shapefile i CSV ze współrzędnymi
- CAD: DWG i DXF między sobą oraz do PDF, SVG i obrazu; SVG do DXF i DWG
- Archiwa: ZIP, 7Z, TAR, TAR.GZ, TAR.XZ i TAR.BZ2 (czyta też RAR, ISO, CAB, DMG, WIM, ARJ, LZH, CPIO, RPM, DEB, ZST, LZ, LZMA i Z)

Koło pokazuje tylko to, co ma sens dla danego pliku: wideo bez dźwięku nie proponuje MP3, nieruchome zdjęcie nie proponuje APNG, arkusz, który nie jest listą kontaktów, nie proponuje vCard.

**Narzędzia**
- Kompresja obrazów, wideo, dźwięku i PDF: według poziomu, procentowo albo do dokładnego rozmiaru
- Edytor zdjęć: kadrowanie, korekta kolorów, strzałki, prostokąty, pióro, tekst i zamazywanie (czarne, rozmycie lub piksele)
- Tło i margines dla obrazów, kolaż z kilku zdjęć, obrazy do PDF, odcienie szarości, odczyt kodów QR
- Metadane: zobaczyć wszystko, edytować tytuł, autora i inne pola, usunąć lokalizację albo wszystko
- Wideo: wyciąć fragment, przyciąć obraz, prędkość, podzielić, zapisać jedną klatkę lub wszystkie, wyodrębnić dźwięk i napisy, obrócić, zmienić rozdzielczość, usunąć dźwięk, połączyć
- Dźwięk: wyciąć fragment, wyrównać głośność, zapiszczeć fragmenty, mono i stereo, prędkość, podzielić, połączyć
- PDF: porządkowanie stron (zmiana kolejności, obracanie, usuwanie), dzielenie, łączenie i prawdziwe zamazywanie (ukryty tekst znika)
- Napisy: opóźnić lub przyspieszyć czas; czcionki: arkusz z próbką; archiwa: rozpakować i sprawdzić spójność

**Działania**: udostępnić (Udostępnianie w pobliżu w Windows), zostawić unoszące się na ekranie, skopiować lub przenieść do folderu, skopiować ścieżkę i otworzyć terminal w folderze.

## Funkcje i gdzie je znaleźć

Ustawienia otwiera się podwójnym kliknięciem ikony Mexerica obok zegara albo ponownym uruchomieniem Mexerica z menu Start. Menu ikony (prawy przycisk) zawiera resztę.

| Funkcja | Do czego służy | Gdzie jest |
|---|---|---|
| **Obserwowane foldery** | Wszystko, co trafi do folderu, jest konwertowane samo. Np.: każde zdjęcie HEIC, które trafi do Pobranych, staje się JPG | Ustawienia → Wynik → Obserwowane foldery → **Wybierz…** → **Dodaj folder…**, wybierz format i zapisz |
| **Ostatnie konwersje** | Lista tego, co zostało zrobione, z wyszukiwaniem, miniaturami i przyciskami do otwarcia, otwarcia innym programem lub pokazania w folderze | Prawy przycisk na ikonie → **Ostatnie konwersje…** |
| **Dostosuj koło** | Ukryć to, czego nie używasz, i zmienić kolejność wycinków | Ustawienia → Wygląd koła → **Dostosuj…** |
| **Ustawienia wstępne** | Twój własny wycinek na kole. Np.: "Strona" = JPG, jakość 80, do 1600 px, bez lokalizacji; albo "Lekki MP3" | W tym samym oknie: **Nowe ustawienie wstępne…** |
| **Motyw i przezroczystość** | Koło jasne, ciemne lub takie jak Windows, bardziej lub mniej przezroczyste | Ustawienia → Wygląd koła → Motyw i Nieprzezroczystość |
| **Koło z kilkoma stronami** | Gdy opcji jest dużo, koło ma więcej niż jedną stronę | **Tab** lub kółko myszy przy otwartym kole |
| **Prosto z przeglądarki** | Skonwertować obraz lub plik ze strony bez pobierania go wcześniej | Przeciągnij obraz lub link ze strony, naciśnij **Spację** i upuść na opcji. Plik trafi do Pobranych |
| **Język** | Portugalski, angielski, hiszpański, francuski, niemiecki, włoski, japoński, koreański, chiński, rosyjski, polski, hindi i arabski. Mexerica otwiera się po portugalsku | **Configurações** (Ustawienia) → **Idioma** (Język) |
| **Aktualizacje** | Powiadomienie obok zegara, gdy wyjdzie nowa wersja; jedno kliknięcie aktualizuje. Pobrany plik jest sprawdzany (SHA-256) przed instalacją | Ustawienia → Aktualizacje (sprawdź teraz lub wyłącz powiadomienie) |
| **Wiersz poleceń** | Konwertowanie z wiersza polecenia lub w skryptach | Zobacz [Wiersz poleceń](#wiersz-poleceń) |

Skróty (.lnk) prowadzą do prawdziwego pliku, a o pustym pliku program ostrzega przed próbą.

## Instalacja

1. Pobierz `Mexerica-<versão>-instalador.exe` ze strony **Releases** (po prawej stronie tej strony).
2. Otwórz pobrany plik i klikaj **Avançar** (Dalej) aż do końca. Instalator jest po portugalsku. Hasło administratora nie jest potrzebne.
3. Jeśli Windows pokaże "System Windows ochronił Twój komputer", kliknij **Więcej informacji**, a potem **Uruchom mimo to**. Ostrzeżenie pojawia się, bo instalator nie ma płatnego podpisu cyfrowego, a nie dlatego, że coś jest nie tak.

Na koniec okno wyjaśni, jak używać programu. Mexerica uruchamia się sam przy starcie komputera (można to wyłączyć w menu ikony). Aby usunąć: **Ustawienia → Aplikacje → Zainstalowane aplikacje → Mexerica → Odinstaluj**. Usuwa to również menu prawego przycisku i ustawienia.

Wymagania: Windows 10 (wersja 2004 lub nowsza) albo Windows 11, 64 bity. Nic więcej nie trzeba instalować. [LibreOffice](https://pt-br.libreoffice.org) jest opcjonalny: z nim dokumenty, arkusze i prezentacje wychodzą wierniej. Jeśli go brakuje, instalator go zaproponuje; jeśli odmówisz, Mexerica skonwertuje samodzielnie.

## Wiersz poleceń

Podczas instalacji zaznacz **Usar o comando "mexerica" no Prompt de Comando e no PowerShell** (Używaj polecenia "mexerica" w wierszu polecenia i w PowerShell), aby móc wywołać `mexerica` z dowolnego folderu:

```powershell
mexerica converter *.heic --para jpg                      # konwertuje wszystko do JPG, obok oryginałów
mexerica converter video.mov --para mp4 -o D:\Saida      # wybiera folder wyników
mexerica opcoes planilha.xlsx                             # pokazuje, co można zrobić z plikiem
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # konwertuje to, co przychodzi, aż do Ctrl+C
```

Kod wyjścia to 0, gdy wszystko się udało, 1, gdy jakiś plik się nie powiódł, i 2, gdy w parametrach jest błąd.

## Kompilacja

Potrzebny jest [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg, Ghostscript, ExifTool, qpdf, 7-Zip, lzip i rhwp w tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # rozszerzenie prawego przycisku (C++; samo pobiera llvm-mingw)
dotnet build src -c Release                                             # do testów
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # gotowa wersja w dist\ i zip do Release
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # zestaw testów bez okna
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # rzadkie formaty na prawdziwych plikach z publicznych zbiorów
```

Nowe teksty interfejsu wymagają tłumaczenia: `python scripts\extrair-textos.py --check` pokazuje, czego brakuje, a `python scripts\gerar-traducoes.py` tworzy szkic w `src\Idiomas`, który trzeba przejrzeć.

Sam program ma też tryb testowy bez okna:

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## Licencja

Kod na licencji MIT. Użyte zewnętrzne biblioteki i programy są wymienione, razem z ich licencjami, w [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
