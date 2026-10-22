[Português (Brasil)](README.md) · [English](README.en.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · **日本語** · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md) · [Polski](README.pl.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">プログラムを開かずにファイルを変換・編集。ファイルをドラッグして <b>スペース</b> を押し、項目の上で離すだけです。</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="変換ホイール">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="ツールホイール">
  <img src="assets/telas/roda-acoes.png" width="260" alt="アクションホイール">
</p>

Mexerica は、Mac 用の [Tangerine](https://tangerineformac.com) のアイデアを Windows で実現したものです。タスクバーの通知領域に常駐します。ファイルを 1 つ以上ドラッグし、マウスを離さずにスペースを押すと、カーソルのまわりにホイールが現れ、そのファイルでできることが表示されます。結果は元のファイルの隣に保存され、元のファイルは一切変更されません。変換はあなたのパソコンの中で行われ、ファイルがインターネットに送られることはありません。Mexerica がインターネットを使うのは、新しいバージョンの確認と、Web サイトからドラッグしたもののダウンロードだけです。

**バージョン 1.1 の新機能:** 対応形式が大幅に増えました (RAW、PSD、Kindle、Outlook、字幕、フォント、3D、地図、CAD、ISO、DMG など)。さらに、LibreOffice なしでの Word、Excel、PowerPoint、自動で変換するフォルダー、最近の変換、プリセット付きでカスタマイズできるホイール、ブラウザーからの直接ドラッグ、13 言語、コマンドライン、更新のお知らせ。[各機能の使い方](#機能と場所) をご覧ください。

**言語:** Mexerica はポルトガル語で起動します。日本語にするには、**Configurações** (設定) を開き、**Idioma** (言語) を選んでください。

## 使い方

| 操作 | 方法 |
|---|---|
| ホイールを開く | ファイルをドラッグし、離さずに **スペース** を押す |
| ホイールを切り替える | もう一度 **スペース**、または **Shift** (変換)、**Ctrl** (ツール)、**Alt** (アクション) |
| 選ぶ | 項目の上でファイルを離す、または **矢印キー** と **Enter** |
| キャンセル | **Esc** |
| キーボードで | エクスプローラーまたはデスクトップでファイルを選び、**Ctrl + Shift + スペース** を押す |
| タッチ画面 | 指を少しの間動かさずに置いてからドラッグ |
| 右クリック | ファイルを選んで右クリックし、**Mexerica** サブメニューを使う (Windows 11 では「さらにオプションを表示」の中) |

複数のファイルを同時に処理します。動画の形式 (MKV、MOV、MP4) だけを変える場合は、可能なら再エンコードせずに数秒で、画質を落とさずに行います。再エンコードが必要なときは、グラフィックボード (NVIDIA、Intel、AMD) があればそれを使います。

キー、画質、保存先フォルダーは設定で変更できます (通知領域のアイコンをダブルクリック、または Mexerica をもう一度開く)。

## できること

**変換**
- 画像: JPG、PNG、WebP、HEIC、AVIF、JPEG XL、JPEG 2000、TIFF、BMP、GIF、APNG、ICO、ICNS、PSD、TGA、DDS、QOI、PCX、SVG (画像をベクター化)、PDF
- 入力のみの画像: カメラの RAW (CR2、CR3、NEF、ARW、DNG など)、PSB、DICOM、EXR、HDR、XCF、JPEG XR、CUR、PPM、EPS、PS、AI、EMF、WMF、SVGZ
- 音声: MP3、M4A (AAC と ALAC)、WAV、FLAC、OGG、Opus、AIFF、WMA、WavPack、TTA、AC3、E-AC3、W64 (APE、DSF、DFF、DTS、AMR、MKA、CAF、AU、MPC も読み込み可)
- 動画: MP4 (H.264、HEVC、AV1)、MOV、MKV、WebM、AVI、WMV、アニメーション GIF と WebP、APNG、MP4 720p、MP3 (VOB、OGV、MXF、ASF、RM/RMVB、DivX、F4V も読み込み可)
- iPhone の Live Photo と Android の Motion Photo: 写真から動画を取り出す
- PDF から DOCX、PPTX、XLSX、EPUB、TXT、Markdown、JPG、PNG へ (全ページ)
- Office の文書、表計算、プレゼンテーション (LibreOffice の有無を問わず)、HWP と HWPX、HTML、Markdown、RTF、ODT
- 電子書籍: EPUB、KEPUB (Kobo)、MOBI、AZW、AZW3、FB2、HTMLZ、TXTZ。コミック CBZ、CBR、CB7、CBT を PDF と EPUB へ
- メール EML と MSG (Outlook) を HTML、TXT、PDF へ。添付ファイルは ZIP にまとめる
- 字幕: SRT、ASS/SSA、VTT、SMI、LRC、TTML、SUB、SCC。古いファイルのアクセント付き文字も保持
- データ: JSON、JSONL、YAML、TOML、XML、CSV、TSV の相互変換。vCard の連絡先を表計算へ、またその逆
- フォント: TTF、OTF、WOFF、WOFF2、EOT
- 3D モデル: GLB、glTF、STL、OBJ、3MF、PLY (FBX は入力のみ)、画像でのプレビュー
- 地図: GeoJSON、KML、KMZ、GPX、Shapefile、座標入りの CSV
- CAD: DWG と DXF の相互変換、および PDF、SVG、画像へ。SVG から DXF と DWG へ
- 圧縮ファイル: ZIP、7Z、TAR、TAR.GZ、TAR.XZ、TAR.BZ2 (RAR、ISO、CAB、DMG、WIM、ARJ、LZH、CPIO、RPM、DEB、ZST、LZ、LZMA、Z も読み込み可)

ホイールには、そのファイルで意味のある項目だけが出ます。音のない動画には MP3 が、静止画には APNG が、連絡先ではない表には vCard が出ません。

**ツール**
- 画像、動画、音声、PDF の圧縮 (レベル、パーセント、または正確なサイズを指定)
- 写真エディター: 切り抜き、色の調整、矢印、四角形、ペン、テキスト、黒塗り (黒、ぼかし、モザイク)
- 画像の背景と余白、複数写真のコラージュ、画像から PDF、グレースケール、QR コードの読み取り
- メタデータ: すべて表示、タイトルや作成者などの編集、位置情報またはすべての削除
- 動画: 区間の切り出し、画面の切り抜き、速度、分割、1 コマまたは全コマの保存、音声と字幕の取り出し、回転、解像度の変更、音声の削除、結合
- 音声: 区間の切り出し、音量の正規化、区間にピー音をかぶせる、モノラルとステレオ、速度、分割、結合
- PDF: ページの整理 (並べ替え、回転、削除)、分割、結合、本当の黒塗り (隠した文字が消える)
- 字幕: 時間を遅らせる・早める。フォント: 見本シート。圧縮ファイル: 展開と整合性の確認

**アクション**: 共有 (Windows の近距離共有)、画面上に浮かべておく、フォルダーへのコピーや移動、パスのコピー、そのフォルダーでターミナルを開く。

## 機能と場所

設定は、時計の近くにある Mexerica のアイコンをダブルクリックするか、スタートメニューから Mexerica をもう一度開くと表示されます。それ以外はアイコンのメニュー (右クリック) にあります。

| 機能 | 用途 | 場所 |
|---|---|---|
| **監視フォルダー** | フォルダーに入ったものを自動で変換します。例: ダウンロードに届いた HEIC の写真をすべて JPG にする | 設定 → 結果 → 監視フォルダー → **選択…** → **フォルダを追加…**、形式を選んで保存 |
| **最近の変換** | 行った処理の一覧。検索、サムネイル、開く・別のプログラムで開く・フォルダーで表示するボタン付き | アイコンを右クリック → **最近の変換…** |
| **ホイールのカスタマイズ** | 使わない項目を隠し、項目の順番を変える | 設定 → ホイールの外観 → **カスタマイズ…** |
| **プリセット** | ホイールに自分用の項目を追加。例: 「Web」= JPG、画質 80、最大 1600 px、位置情報なし。または「軽い MP3」 | 同じウィンドウで **新しいプリセット…** |
| **テーマと透明度** | ホイールを明るい色、暗い色、または Windows と同じにし、透明度を調整 | 設定 → ホイールの外観 → テーマ と 不透明度 |
| **複数の面があるホイール** | 項目が多いと、ホイールに複数の面ができます | ホイールを開いた状態で **Tab** またはマウスホイール |
| **ブラウザーから直接** | Web サイトの画像やファイルを、先にダウンロードせずに変換 | サイトの画像やリンクをドラッグし、**スペース** を押して項目の上で離す。ファイルはダウンロードに保存されます |
| **言語** | ポルトガル語、英語、スペイン語、フランス語、ドイツ語、イタリア語、日本語、韓国語、中国語、ロシア語、ポーランド語、ヒンディー語、アラビア語。Mexerica はポルトガル語で起動します | **Configurações** (設定) → **Idioma** (言語) |
| **アップデート** | 新しいバージョンが出ると時計の近くにお知らせが出て、クリック 1 回で更新。ダウンロードしたファイルはインストール前に確認 (SHA-256) されます | 設定 → アップデート (今すぐ確認、またはお知らせをオフ) |
| **コマンドライン** | コマンドプロンプトやスクリプトから変換 | [コマンドライン](#コマンドライン) を参照 |

ショートカット (.lnk) は実際のファイルまでたどり、空のファイルは処理の前にお知らせします。

## インストール

1. **Releases** ページ (このページの右側) から `Mexerica-<versão>-instalador.exe` をダウンロードします。
2. ダウンロードしたファイルを開き、最後まで **Avançar** (次へ) をクリックします。インストーラーはポルトガル語です。管理者のパスワードは必要ありません。
3. Windows に「Windows によって PC が保護されました」と表示されたら、**詳細情報** をクリックしてから **実行** をクリックします。この警告は、インストーラーに有料のデジタル署名がないために出るもので、問題があるわけではありません。

完了すると、使い方を説明するウィンドウが開きます。Mexerica はパソコンの起動時に自動で開きます (通知領域のメニューでオフにできます)。削除するには: **設定 → アプリ → インストールされているアプリ → Mexerica → アンインストール**。右クリックメニューと設定も一緒に削除されます。

動作環境: Windows 10 (バージョン 2004 以降) または Windows 11、64 ビット。ほかに何もインストールする必要はありません。[LibreOffice](https://pt-br.libreoffice.org) は任意です。あると、文書、表計算、プレゼンテーションがより元に近い形で変換されます。ない場合はインストーラーが勧めますが、断っても Mexerica が自力で変換します。

## コマンドライン

インストール時に **Usar o comando "mexerica" no Prompt de Comando e no PowerShell** (コマンドプロンプトと PowerShell で「mexerica」コマンドを使う) にチェックを入れると、どのフォルダーからでも `mexerica` を呼び出せます。

```powershell
mexerica converter *.heic --para jpg                      # すべて JPG に変換し、元のファイルの隣に保存
mexerica converter video.mov --para mp4 -o D:\Saida      # 保存先フォルダーを指定
mexerica opcoes planilha.xlsx                             # そのファイルでできることを一覧表示
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # Ctrl+C を押すまで、届いたものを変換
```

終了コードは、すべて成功なら 0、失敗したファイルがあれば 1、パラメーターに誤りがあれば 2 です。

## ビルド

[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) が必要です。

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg、Ghostscript、ExifTool、qpdf、7-Zip、lzip、rhwp を tools\ に
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # 右クリック拡張 (C++。llvm-mingw は自動でダウンロード)
dotnet build src -c Release                                             # テスト用
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # 完成版を dist\ に、Release 用の zip も作成
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # 画面なしのテスト一式
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # 公開アーカイブからダウンロードした実ファイルで珍しい形式をテスト
```

画面に新しい文言を追加したら翻訳が必要です。`python scripts\extrair-textos.py --check` で足りないものがわかり、`python scripts\gerar-traducoes.py` で `src\Idiomas` に下書きができます。下書きは見直してください。

プログラム自体にも画面なしのテストモードがあります。

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## ライセンス

コードは MIT ライセンスです。使用している外部のライブラリとプログラムは、それぞれのライセンスとともに [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) に記載しています。
