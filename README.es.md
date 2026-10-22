[Português (Brasil)](README.md) · [English](README.en.md) · **Español** · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md) · [Polski](README.pl.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">Convierte y edita archivos sin abrir ningún programa: arrastra el archivo, pulsa <b>Espacio</b> y suéltalo sobre la opción.</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="Rueda Convertir">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="Rueda Herramientas">
  <img src="assets/telas/roda-acoes.png" width="260" alt="Rueda Acciones">
</p>

Mexerica es una versión para Windows de la idea de [Tangerine](https://tangerineformac.com), del Mac. Se queda en la bandeja del sistema. Cuando arrastras uno o más archivos y pulsas Espacio sin soltar el ratón, aparece una rueda alrededor del cursor con lo que se puede hacer con ellos. El resultado se guarda junto al original, que nunca se modifica. Las conversiones se hacen en tu computadora y ningún archivo tuyo se envía a internet. Mexerica solo usa internet para buscar versiones nuevas y para descargar lo que arrastres desde un sitio web.

**Novedades de la versión 1.1:** muchos más formatos (RAW, PSD, Kindle, Outlook, subtítulos, fuentes, 3D, mapas, CAD, ISO, DMG y otros), Word, Excel y PowerPoint sin LibreOffice, carpetas que convierten solas, conversiones recientes, rueda personalizable con preajustes, arrastrar directo desde el navegador, 13 idiomas, línea de comandos y aviso de actualización. Mira [cómo usar cada función](#funciones-y-dónde-están).

**Idioma:** Mexerica se abre en portugués. Para cambiarlo a español, abre **Configurações** (Configuración) y elige **Idioma**.

## Cómo usarlo

| Acción | Qué hacer |
|---|---|
| Abrir la rueda | Arrastra archivos y, sin soltar, pulsa **Espacio** |
| Cambiar de rueda | **Espacio** otra vez, o **Shift** (Convertir), **Ctrl** (Herramientas), **Alt** (Acciones) |
| Elegir | Suelta el archivo sobre la opción, o usa las **flechas** y **Enter** |
| Cancelar | **Esc** |
| Con el teclado | Selecciona archivos en el Explorador de archivos o en el escritorio y pulsa **Ctrl + Shift + Espacio** |
| Pantalla táctil | Mantén el dedo quieto un instante y arrastra |
| Botón derecho | Selecciona los archivos, haz clic con el botón derecho y usa el submenú **Mexerica** (en Windows 11, en "Mostrar más opciones") |

Se procesan varios archivos a la vez. Cambiar solo el formato del video (MKV, MOV, MP4) se hace sin recodificar cuando es posible, en segundos y sin perder calidad. Cuando hay que recodificar, Mexerica usa la tarjeta de video (NVIDIA, Intel o AMD), si la hay.

Las teclas, la calidad y la carpeta de salida se cambian en la Configuración (doble clic en el icono de la bandeja, o abre Mexerica otra vez).

## Qué hace

**Convertir**
- Imágenes: JPG, PNG, WebP, HEIC, AVIF, JPEG XL, JPEG 2000, TIFF, BMP, GIF, APNG, ICO, ICNS, PSD, TGA, DDS, QOI, PCX, SVG (vectorizando la imagen) y PDF
- Imágenes solo como entrada: RAW de cámara (CR2, CR3, NEF, ARW, DNG y otros), PSB, DICOM, EXR, HDR, XCF, JPEG XR, CUR, PPM, EPS, PS, AI, EMF, WMF y SVGZ
- Audio: MP3, M4A (AAC y ALAC), WAV, FLAC, OGG, Opus, AIFF, WMA, WavPack, TTA, AC3, E-AC3 y W64 (también lee APE, DSF, DFF, DTS, AMR, MKA, CAF, AU y MPC)
- Video: MP4 (H.264, HEVC o AV1), MOV, MKV, WebM, AVI, WMV, GIF y WebP animados, APNG, MP4 720p y MP3 (también lee VOB, OGV, MXF, ASF, RM/RMVB, DivX y F4V)
- Live Photo del iPhone y Motion Photo de Android: saca el video de la foto
- PDF a DOCX, PPTX, XLSX, EPUB, TXT, Markdown, JPG y PNG (todas las páginas)
- Documentos, hojas de cálculo y presentaciones de Office, con o sin LibreOffice; HWP y HWPX; HTML, Markdown, RTF y ODT
- Libros electrónicos: EPUB, KEPUB (Kobo), MOBI, AZW, AZW3, FB2, HTMLZ y TXTZ; cómics CBZ, CBR, CB7 y CBT a PDF y EPUB
- Correos EML y MSG (Outlook) a HTML, TXT o PDF, con los adjuntos en un ZIP
- Subtítulos: SRT, ASS/SSA, VTT, SMI, LRC, TTML, SUB y SCC, conservando los acentos de archivos antiguos
- Datos: JSON, JSONL, YAML, TOML, XML, CSV y TSV entre sí; contactos vCard a hoja de cálculo y de vuelta
- Fuentes: TTF, OTF, WOFF, WOFF2 y EOT
- Modelos 3D: GLB, glTF, STL, OBJ, 3MF y PLY (FBX como entrada) y vista previa en imagen
- Mapas: GeoJSON, KML, KMZ, GPX, Shapefile y CSV con coordenadas
- CAD: DWG y DXF entre sí y a PDF, SVG e imagen; SVG a DXF y DWG
- Comprimidos: ZIP, 7Z, TAR, TAR.GZ, TAR.XZ y TAR.BZ2 (también lee RAR, ISO, CAB, DMG, WIM, ARJ, LZH, CPIO, RPM, DEB, ZST, LZ, LZMA y Z)

La rueda solo ofrece lo que tiene sentido para ese archivo: un video sin sonido no ofrece MP3, una foto fija no ofrece APNG, una hoja de cálculo que no es de contactos no ofrece vCard.

**Herramientas**
- Comprimir imagen, video, audio y PDF por nivel, por porcentaje o a un tamaño exacto
- Editor de fotos: recortar, ajustar el color, flechas, rectángulos, lápiz, texto y tachado (negro, desenfoque o píxeles)
- Fondo y margen para imágenes, collage de varias fotos, imágenes a PDF, escala de grises, lectura de códigos QR
- Metadatos: ver todo, editar título, autor y otros campos, borrar la ubicación o todo
- Video: cortar un tramo, recortar la imagen, velocidad, dividir, sacar un fotograma o todos, extraer el audio y los subtítulos, girar, cambiar la resolución, quitar el sonido, unir
- Audio: cortar un tramo, normalizar el volumen, pitido sobre tramos, mono y estéreo, velocidad, dividir, unir
- PDF: organizar páginas (reordenar, girar, borrar), dividir, unir y tachar de verdad (el texto oculto desaparece)
- Subtítulos: atrasar o adelantar el tiempo; fuentes: hoja de muestra; comprimidos: extraer y comprobar la integridad

**Acciones**: compartir (Uso compartido en proximidad de Windows), dejar flotando en la pantalla, copiar o mover a una carpeta, copiar la ruta y abrir la terminal en la carpeta.

## Funciones y dónde están

La Configuración se abre con doble clic en el icono de Mexerica junto al reloj, o abriendo Mexerica otra vez desde el menú Inicio. El menú del icono (botón derecho) tiene el resto.

| Función | Para qué sirve | Dónde está |
|---|---|---|
| **Carpetas vigiladas** | Todo lo que cae en una carpeta se convierte solo. Ej.: toda foto HEIC que llega a Descargas se convierte en JPG | Configuración → Resultado → Carpetas vigiladas → **Elige…** → **Agregar carpeta…**, elige el formato y guarda |
| **Conversiones recientes** | Lista de lo que se hizo, con búsqueda, miniaturas y botones para abrir, abrir con otro programa o mostrar en la carpeta | Botón derecho en el icono → **Conversiones recientes…** |
| **Personalizar la rueda** | Ocultar lo que no usas y cambiar el orden de las porciones | Configuración → Aspecto de la rueda → **Personalizar…** |
| **Preajustes** | Una porción tuya en la rueda. Ej.: "Web" = JPG, calidad 80, hasta 1600 px, sin ubicación; o "MP3 ligero" | En la misma ventana: **Nuevo preajuste…** |
| **Tema y transparencia** | Rueda clara, oscura o igual que Windows, más o menos transparente | Configuración → Aspecto de la rueda → Tema y Opacidad |
| **Rueda con caras** | Cuando hay muchas opciones, la rueda tiene más de una cara | **Tab** o la rueda del ratón con la rueda abierta |
| **Directo desde el navegador** | Convertir una imagen o un archivo de un sitio web sin descargarlo antes | Arrastra la imagen o el enlace del sitio, pulsa **Espacio** y suelta sobre la opción. El archivo queda en Descargas |
| **Idioma** | Portugués, inglés, español, francés, alemán, italiano, japonés, coreano, chino, ruso, polaco, hindi y árabe. Mexerica se abre en portugués | **Configurações** (Configuración) → **Idioma** |
| **Actualizaciones** | Aviso junto al reloj cuando sale una versión nueva; un clic actualiza. El archivo descargado se comprueba (SHA-256) antes de instalar | Configuración → Actualizaciones (buscar ahora o desactivar el aviso) |
| **Línea de comandos** | Convertir desde el Símbolo del sistema o en scripts | Mira [Línea de comandos](#línea-de-comandos) |

Los accesos directos (.lnk) se siguen hasta el archivo real, y un archivo vacío se avisa antes de intentarlo.

## Instalar

1. Descarga `Mexerica-<versão>-instalador.exe` en la página de **Releases** (a la derecha, en esta página).
2. Abre el archivo descargado y haz clic en **Avançar** (Siguiente) hasta el final. El instalador está en portugués. No hace falta contraseña de administrador.
3. Si Windows muestra "Windows protegió su PC", haz clic en **Más información** y después en **Ejecutar de todas formas**. El aviso aparece porque el instalador no tiene firma digital de pago, no porque haya algo mal.

Al terminar, una ventana explica cómo usarlo. Mexerica se abre solo cuando se enciende la computadora (se puede desactivar en el menú de la bandeja). Para quitarlo: **Configuración → Aplicaciones → Aplicaciones instaladas → Mexerica → Desinstalar**. Esto quita también el menú del botón derecho y la configuración.

Requisitos: Windows 10 (versión 2004 o más nueva) o Windows 11, 64 bits. No hace falta instalar nada más. [LibreOffice](https://pt-br.libreoffice.org) es opcional: con él, los documentos, hojas de cálculo y presentaciones salen más fieles al original. Si falta, el instalador lo ofrece; si lo rechazas, Mexerica convierte por su cuenta.

## Línea de comandos

En la instalación, marca **Usar o comando "mexerica" no Prompt de Comando e no PowerShell** (Usar el comando "mexerica" en el Símbolo del sistema y en PowerShell) para poder llamar a `mexerica` desde cualquier carpeta:

```powershell
mexerica converter *.heic --para jpg                      # convierte todo a JPG, junto a los originales
mexerica converter video.mov --para mp4 -o D:\Saida      # elige la carpeta de salida
mexerica opcoes planilha.xlsx                             # lista lo que se puede hacer con el archivo
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # convierte lo que llegue hasta pulsar Ctrl+C
```

El código de salida es 0 cuando todo salió bien, 1 cuando algún archivo falló y 2 cuando hay un error en los parámetros.

## Compilar

Necesitas el [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg, Ghostscript, ExifTool, qpdf, 7-Zip, lzip y rhwp en tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # extensión del botón derecho (C++; descarga llvm-mingw solo)
dotnet build src -c Release                                             # para probar
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # versión lista en dist\ y el zip de la Release
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # batería de pruebas sin pantalla
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # formatos raros con archivos reales descargados de acervos públicos
```

Los textos nuevos de la interfaz necesitan traducción: `python scripts\extrair-textos.py --check` muestra lo que falta y `python scripts\gerar-traducoes.py` crea un borrador en `src\Idiomas`, que se debe revisar.

El propio programa también tiene un modo de prueba sin pantalla:

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## Licencia

Código bajo la licencia MIT. Las bibliotecas y programas externos usados están listados, con sus licencias, en [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
