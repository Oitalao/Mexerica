[Português (Brasil)](README.md) · [English](README.en.md) · [Español](README.es.md) · **Français** · [Deutsch](README.de.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md) · [Polski](README.pl.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">Convertissez et modifiez des fichiers sans ouvrir aucun programme : faites glisser le fichier, appuyez sur <b>Espace</b> et déposez-le sur l'option.</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="Roue Convertir">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="Roue Outils">
  <img src="assets/telas/roda-acoes.png" width="260" alt="Roue Actions">
</p>

Mexerica est une version pour Windows de l'idée de [Tangerine](https://tangerineformac.com), sur Mac. Il reste dans la zone de notification. Quand vous faites glisser un ou plusieurs fichiers et appuyez sur Espace sans lâcher la souris, une roue apparaît autour du curseur avec ce que vous pouvez en faire. Le résultat est enregistré à côté de l'original, qui n'est jamais modifié. Les conversions se font sur votre ordinateur et aucun de vos fichiers n'est envoyé sur internet. Mexerica n'utilise internet que pour chercher les nouvelles versions et pour télécharger ce que vous faites glisser depuis un site.

**Nouveau dans la version 1.1 :** beaucoup plus de formats (RAW, PSD, Kindle, Outlook, sous-titres, polices, 3D, cartes, CAO, ISO, DMG et autres), Word, Excel et PowerPoint sans LibreOffice, dossiers qui convertissent tout seuls, conversions récentes, roue personnalisable avec préréglages, glisser directement depuis le navigateur, 13 langues, ligne de commande et avis de mise à jour. Voir [comment utiliser chaque fonction](#fonctions-et-où-les-trouver).

**Langue :** Mexerica s'ouvre en portugais. Pour passer au français, ouvrez **Configurações** (Paramètres) et choisissez **Idioma** (Langue).

## Comment l'utiliser

| Action | Que faire |
|---|---|
| Ouvrir la roue | Faites glisser des fichiers et, sans lâcher, appuyez sur **Espace** |
| Changer de roue | **Espace** à nouveau, ou **Maj** (Convertir), **Ctrl** (Outils), **Alt** (Actions) |
| Choisir | Déposez le fichier sur l'option, ou utilisez les **flèches** et **Entrée** |
| Annuler | **Échap** |
| Au clavier | Sélectionnez des fichiers dans l'Explorateur de fichiers ou sur le bureau et appuyez sur **Ctrl + Maj + Espace** |
| Écran tactile | Gardez le doigt immobile un instant, puis faites glisser |
| Clic droit | Sélectionnez les fichiers, faites un clic droit et utilisez le sous-menu **Mexerica** (sous Windows 11, dans "Afficher plus d'options") |

Plusieurs fichiers sont traités en même temps. Changer seulement le format de la vidéo (MKV, MOV, MP4) se fait sans réencoder quand c'est possible, en quelques secondes et sans perte de qualité. Quand il faut réencoder, Mexerica utilise la carte graphique (NVIDIA, Intel ou AMD), s'il y en a une.

Les touches, la qualité et le dossier de sortie se changent dans les Paramètres (double clic sur l'icône de la zone de notification, ou rouvrez Mexerica).

## Ce qu'il fait

**Convertir**
- Images : JPG, PNG, WebP, HEIC, AVIF, JPEG XL, JPEG 2000, TIFF, BMP, GIF, APNG, ICO, ICNS, PSD, TGA, DDS, QOI, PCX, SVG (en vectorisant l'image) et PDF
- Images en entrée seulement : RAW d'appareil photo (CR2, CR3, NEF, ARW, DNG et autres), PSB, DICOM, EXR, HDR, XCF, JPEG XR, CUR, PPM, EPS, PS, AI, EMF, WMF et SVGZ
- Audio : MP3, M4A (AAC et ALAC), WAV, FLAC, OGG, Opus, AIFF, WMA, WavPack, TTA, AC3, E-AC3 et W64 (lit aussi APE, DSF, DFF, DTS, AMR, MKA, CAF, AU et MPC)
- Vidéo : MP4 (H.264, HEVC ou AV1), MOV, MKV, WebM, AVI, WMV, GIF et WebP animés, APNG, MP4 720p et MP3 (lit aussi VOB, OGV, MXF, ASF, RM/RMVB, DivX et F4V)
- Live Photo de l'iPhone et Motion Photo d'Android : extrait la vidéo de la photo
- PDF vers DOCX, PPTX, XLSX, EPUB, TXT, Markdown, JPG et PNG (toutes les pages)
- Documents, feuilles de calcul et présentations Office, avec ou sans LibreOffice ; HWP et HWPX ; HTML, Markdown, RTF et ODT
- Livres numériques : EPUB, KEPUB (Kobo), MOBI, AZW, AZW3, FB2, HTMLZ et TXTZ ; bandes dessinées CBZ, CBR, CB7 et CBT vers PDF et EPUB
- E-mails EML et MSG (Outlook) vers HTML, TXT ou PDF, avec les pièces jointes dans un ZIP
- Sous-titres : SRT, ASS/SSA, VTT, SMI, LRC, TTML, SUB et SCC, en gardant les accents des anciens fichiers
- Données : JSON, JSONL, YAML, TOML, XML, CSV et TSV entre eux ; contacts vCard vers feuille de calcul et retour
- Polices : TTF, OTF, WOFF, WOFF2 et EOT
- Modèles 3D : GLB, glTF, STL, OBJ, 3MF et PLY (FBX en entrée) et aperçu en image
- Cartes : GeoJSON, KML, KMZ, GPX, Shapefile et CSV avec coordonnées
- CAO : DWG et DXF entre eux et vers PDF, SVG et image ; SVG vers DXF et DWG
- Archives : ZIP, 7Z, TAR, TAR.GZ, TAR.XZ et TAR.BZ2 (lit aussi RAR, ISO, CAB, DMG, WIM, ARJ, LZH, CPIO, RPM, DEB, ZST, LZ, LZMA et Z)

La roue ne propose que ce qui a du sens pour ce fichier : une vidéo sans son ne propose pas MP3, une photo fixe ne propose pas APNG, une feuille de calcul qui n'est pas une liste de contacts ne propose pas vCard.

**Outils**
- Compresser image, vidéo, audio et PDF par niveau, par pourcentage ou vers une taille exacte
- Éditeur photo : recadrer, ajuster la couleur, flèches, rectangles, stylo, texte et caviardage (noir, flou ou pixels)
- Fond et marge pour les images, collage de plusieurs photos, images vers PDF, niveaux de gris, lecture de QR Code
- Métadonnées : tout voir, modifier le titre, l'auteur et d'autres champs, effacer la localisation ou tout
- Vidéo : couper un passage, recadrer l'image, vitesse, découper, extraire une image ou toutes, extraire l'audio et les sous-titres, pivoter, changer la résolution, enlever le son, assembler
- Audio : couper un passage, normaliser le volume, bip sur des passages, mono et stéréo, vitesse, découper, assembler
- PDF : organiser les pages (réordonner, pivoter, supprimer), découper, assembler et caviarder pour de vrai (le texte caché disparaît)
- Sous-titres : retarder ou avancer le minutage ; polices : planche d'exemple ; archives : extraire et tester l'intégrité

**Actions** : partager (Partage de proximité de Windows), laisser flottant à l'écran, copier ou déplacer vers un dossier, copier le chemin et ouvrir le terminal dans le dossier.

## Fonctions et où les trouver

Les Paramètres s'ouvrent par un double clic sur l'icône de Mexerica près de l'horloge, ou en rouvrant Mexerica depuis le menu Démarrer. Le menu de l'icône (clic droit) contient le reste.

| Fonction | À quoi elle sert | Où la trouver |
|---|---|---|
| **Dossiers surveillés** | Tout ce qui arrive dans un dossier est converti tout seul. Ex. : chaque photo HEIC qui arrive dans Téléchargements devient un JPG | Paramètres → Résultat → Dossiers surveillés → **Choisissez…** → **Ajouter un dossier…**, choisissez le format et enregistrez |
| **Conversions récentes** | Liste de ce qui a été fait, avec recherche, miniatures et boutons pour ouvrir, ouvrir avec un autre programme ou afficher dans le dossier | Clic droit sur l'icône → **Conversions récentes…** |
| **Personnaliser la roue** | Masquer ce que vous n'utilisez pas et changer l'ordre des parts | Paramètres → Apparence de la roue → **Personnaliser…** |
| **Préréglages** | Votre propre part sur la roue. Ex. : "Web" = JPG, qualité 80, jusqu'à 1600 px, sans localisation ; ou "MP3 léger" | Dans la même fenêtre : **Nouveau préréglage…** |
| **Thème et transparence** | Roue claire, sombre ou comme Windows, plus ou moins transparente | Paramètres → Apparence de la roue → Thème et Opacité |
| **Roue à plusieurs faces** | Quand il y a beaucoup d'options, la roue a plus d'une face | **Tab** ou la molette de la souris quand la roue est ouverte |
| **Directement depuis le navigateur** | Convertir une image ou un fichier d'un site sans le télécharger avant | Faites glisser l'image ou le lien du site, appuyez sur **Espace** et déposez sur l'option. Le fichier va dans Téléchargements |
| **Langue** | Portugais, anglais, espagnol, français, allemand, italien, japonais, coréen, chinois, russe, polonais, hindi et arabe. Mexerica s'ouvre en portugais | **Configurações** (Paramètres) → **Idioma** (Langue) |
| **Mises à jour** | Avis près de l'horloge quand une nouvelle version sort ; un clic suffit pour mettre à jour. Le fichier téléchargé est vérifié (SHA-256) avant l'installation | Paramètres → Mises à jour (vérifier maintenant ou désactiver l'avis) |
| **Ligne de commande** | Convertir depuis l'Invite de commandes ou dans des scripts | Voir [Ligne de commande](#ligne-de-commande) |

Les raccourcis (.lnk) sont suivis jusqu'au vrai fichier, et un fichier vide est signalé avant d'essayer.

## Installer

1. Téléchargez `Mexerica-<versão>-instalador.exe` sur la page **Releases** (à droite, sur cette page).
2. Ouvrez le fichier téléchargé et cliquez sur **Avançar** (Suivant) jusqu'à la fin. L'installateur est en portugais. Aucun mot de passe administrateur n'est nécessaire.
3. Si Windows affiche "Windows a protégé votre ordinateur", cliquez sur **Informations complémentaires** puis sur **Exécuter quand même**. L'avertissement apparaît parce que l'installateur n'a pas de signature numérique payante, pas parce qu'il y a un problème.

À la fin, une fenêtre explique comment l'utiliser. Mexerica s'ouvre tout seul au démarrage de l'ordinateur (vous pouvez le désactiver dans le menu de la zone de notification). Pour le supprimer : **Paramètres → Applications → Applications installées → Mexerica → Désinstaller**. Cela enlève aussi le menu du clic droit et les paramètres.

Configuration requise : Windows 10 (version 2004 ou plus récente) ou Windows 11, 64 bits. Rien d'autre à installer. [LibreOffice](https://pt-br.libreoffice.org) est facultatif : avec lui, les documents, feuilles de calcul et présentations sont plus fidèles à l'original. S'il manque, l'installateur le propose ; si vous refusez, Mexerica convertit par ses propres moyens.

## Ligne de commande

Pendant l'installation, cochez **Usar o comando "mexerica" no Prompt de Comando e no PowerShell** (Utiliser la commande "mexerica" dans l'Invite de commandes et dans PowerShell) pour pouvoir appeler `mexerica` depuis n'importe quel dossier :

```powershell
mexerica converter *.heic --para jpg                      # convertit tout en JPG, à côté des originaux
mexerica converter video.mov --para mp4 -o D:\Saida      # choisit le dossier de sortie
mexerica opcoes planilha.xlsx                             # liste ce qu'on peut faire avec le fichier
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # convertit ce qui arrive jusqu'à Ctrl+C
```

Le code de sortie vaut 0 quand tout s'est bien passé, 1 quand un fichier a échoué et 2 quand il y a une erreur dans les paramètres.

## Compiler

Il faut le [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg, Ghostscript, ExifTool, qpdf, 7-Zip, lzip et rhwp dans tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # extension du clic droit (C++ ; télécharge llvm-mingw tout seul)
dotnet build src -c Release                                             # pour tester
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # version prête dans dist\ et le zip de la Release
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # batterie de tests sans écran
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # formats rares avec de vrais fichiers téléchargés depuis des collections publiques
```

Les nouveaux textes de l'interface doivent être traduits : `python scripts\extrair-textos.py --check` montre ce qui manque et `python scripts\gerar-traducoes.py` crée un brouillon dans `src\Idiomas`, qui doit être relu.

Le programme lui-même a aussi un mode de test sans écran :

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## Licence

Code sous licence MIT. Les bibliothèques et programmes externes utilisés sont listés, avec leurs licences, dans [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
