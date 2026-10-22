[Português (Brasil)](README.md) · [English](README.en.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · **简体中文** · [Русский](README.ru.md) · [Polski](README.pl.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">不用打开任何程序就能转换和编辑文件：拖动文件，按下 <b>空格键</b>，然后松开在选项上。</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="转换轮盘">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="工具轮盘">
  <img src="assets/telas/roda-acoes.png" width="260" alt="行动轮盘">
</p>

Mexerica 把 Mac 上 [Tangerine](https://tangerineformac.com) 的想法搬到了 Windows。它待在任务栏的通知区域。拖动一个或多个文件，不松开鼠标并按下空格键，光标周围就会出现一个轮盘，列出这些文件可以做的事。结果保存在原文件旁边，原文件永远不会被修改。转换在你的电脑上完成，你的文件不会被发送到互联网。Mexerica 只在检查新版本和下载你从网站拖来的内容时才使用互联网。

**1.1 版新功能：** 支持更多格式（RAW、PSD、Kindle、Outlook、字幕、字体、3D、地图、CAD、ISO、DMG 等），无需 LibreOffice 也能处理 Word、Excel 和 PowerPoint，能自动转换的文件夹，最近的转换，可自定义并带预设的轮盘，直接从浏览器拖入，13 种语言，命令行，以及更新提醒。请看[每个功能的用法](#功能及位置)。

**语言：** Mexerica 启动时是葡萄牙语。要改成中文，请打开 **Configurações**（设置），然后选择 **Idioma**（语言）。

## 使用方法

| 操作 | 怎么做 |
|---|---|
| 打开轮盘 | 拖动文件，不松开，按 **空格键** |
| 切换轮盘 | 再按一次 **空格键**，或 **Shift**（转换）、**Ctrl**（工具）、**Alt**（行动） |
| 选择 | 把文件松开在选项上，或使用 **方向键** 和 **Enter** |
| 取消 | **Esc** |
| 用键盘 | 在文件资源管理器或桌面上选中文件，按 **Ctrl + Shift + 空格键** |
| 触摸屏 | 手指按住不动片刻，然后拖动 |
| 右键 | 选中文件，点击右键，使用 **Mexerica** 子菜单（Windows 11 中在“显示更多选项”里） |

多个文件会同时处理。只改变视频格式（MKV、MOV、MP4）时，尽可能不重新编码，几秒钟就好，画质不受损失。需要重新编码时，如果有显卡（NVIDIA、Intel 或 AMD），Mexerica 会使用它。

按键、质量和输出文件夹可以在设置中更改（双击通知区域的图标，或再次打开 Mexerica）。

## 能做什么

**转换**
- 图片：JPG、PNG、WebP、HEIC、AVIF、JPEG XL、JPEG 2000、TIFF、BMP、GIF、APNG、ICO、ICNS、PSD、TGA、DDS、QOI、PCX、SVG（将图片矢量化）和 PDF
- 仅作输入的图片：相机 RAW（CR2、CR3、NEF、ARW、DNG 等）、PSB、DICOM、EXR、HDR、XCF、JPEG XR、CUR、PPM、EPS、PS、AI、EMF、WMF 和 SVGZ
- 音频：MP3、M4A（AAC 和 ALAC）、WAV、FLAC、OGG、Opus、AIFF、WMA、WavPack、TTA、AC3、E-AC3 和 W64（也能读取 APE、DSF、DFF、DTS、AMR、MKA、CAF、AU 和 MPC）
- 视频：MP4（H.264、HEVC 或 AV1）、MOV、MKV、WebM、AVI、WMV、动态 GIF 和 WebP、APNG、MP4 720p 和 MP3（也能读取 VOB、OGV、MXF、ASF、RM/RMVB、DivX 和 F4V）
- iPhone 的实况照片（Live Photo）和 Android 的动态照片（Motion Photo）：从照片中取出视频
- PDF 转 DOCX、PPTX、XLSX、EPUB、TXT、Markdown、JPG 和 PNG（所有页面）
- Office 文档、表格和演示文稿，有没有 LibreOffice 都行；HWP 和 HWPX；HTML、Markdown、RTF 和 ODT
- 电子书：EPUB、KEPUB（Kobo）、MOBI、AZW、AZW3、FB2、HTMLZ 和 TXTZ；漫画 CBZ、CBR、CB7 和 CBT 转 PDF 和 EPUB
- 邮件 EML 和 MSG（Outlook）转 HTML、TXT 或 PDF，附件打包成 ZIP
- 字幕：SRT、ASS/SSA、VTT、SMI、LRC、TTML、SUB 和 SCC，保留旧文件中的重音字母
- 数据：JSON、JSONL、YAML、TOML、XML、CSV 和 TSV 互相转换；vCard 联系人转表格，也能转回来
- 字体：TTF、OTF、WOFF、WOFF2 和 EOT
- 3D 模型：GLB、glTF、STL、OBJ、3MF 和 PLY（FBX 仅作输入），以及图片预览
- 地图：GeoJSON、KML、KMZ、GPX、Shapefile 和带坐标的 CSV
- CAD：DWG 和 DXF 互相转换，也能转 PDF、SVG 和图片；SVG 转 DXF 和 DWG
- 压缩包：ZIP、7Z、TAR、TAR.GZ、TAR.XZ 和 TAR.BZ2（也能读取 RAR、ISO、CAB、DMG、WIM、ARJ、LZH、CPIO、RPM、DEB、ZST、LZ、LZMA 和 Z）

轮盘只显示对该文件有意义的选项：没有声音的视频不会显示 MP3，静态照片不会显示 APNG，不是联系人的表格不会显示 vCard。

**工具**
- 压缩图片、视频、音频和 PDF：按级别、按百分比或压到指定大小
- 照片编辑器：裁剪、调整颜色、箭头、矩形、画笔、文字和遮盖（黑块、模糊或马赛克）
- 图片背景和边距，多张照片拼图，图片转 PDF，灰度，读取二维码
- 元数据：查看全部，编辑标题、作者和其他字段，删除位置信息或全部删除
- 视频：剪取片段、裁剪画面、速度、分割、导出一帧或所有帧、提取音频和字幕、旋转、改变分辨率、去掉声音、合并
- 音频：剪取片段、音量标准化、在片段上加哔声、单声道和立体声、速度、分割、合并
- PDF：整理页面（重新排序、旋转、删除）、分割、合并，以及真正的遮盖（被遮的文字会彻底消失）
- 字幕：推迟或提前时间；字体：样张；压缩包：解压和检查完整性

**行动**：分享（Windows 附近共享）、让文件悬浮在屏幕上、复制或移动到文件夹、复制路径，以及在该文件夹中打开终端。

## 功能及位置

双击时钟附近的 Mexerica 图标，或从开始菜单再次打开 Mexerica，即可打开设置。其余功能在图标菜单（右键）里。

| 功能 | 用途 | 位置 |
|---|---|---|
| **监视文件夹** | 放进某个文件夹的东西都会自动转换。例如：下载文件夹里收到的每张 HEIC 照片都变成 JPG | 设置 → 结果 → 监视文件夹 → **选择…** → **添加文件夹…**，选择格式并保存 |
| **最近的转换** | 做过的事情列表，带搜索、缩略图，以及打开、用其他程序打开或在文件夹中显示的按钮 | 右键点击图标 → **最近的转换…** |
| **自定义轮盘** | 隐藏不用的选项，调整各项的顺序 | 设置 → 轮盘外观 → **自定义…** |
| **预设** | 在轮盘上加一个你自己的选项。例如：“网站” = JPG、质量 80、最大 1600 px、不含位置；或“轻量 MP3” | 在同一窗口中：**新建预设…** |
| **主题和透明度** | 轮盘可以是浅色、深色或跟随 Windows，透明度可调 | 设置 → 轮盘外观 → 主题 和 不透明度 |
| **多面轮盘** | 选项很多时，轮盘会有不止一面 | 轮盘打开时按 **Tab** 或滚动鼠标滚轮 |
| **直接从浏览器** | 不必先下载，就能转换网站上的图片或文件 | 拖动网站上的图片或链接，按 **空格键**，松开在选项上。文件会保存在下载文件夹 |
| **语言** | 葡萄牙语、英语、西班牙语、法语、德语、意大利语、日语、韩语、中文、俄语、波兰语、印地语和阿拉伯语。Mexerica 启动时是葡萄牙语 | **Configurações**（设置）→ **Idioma**（语言） |
| **更新** | 有新版本时，时钟附近会出现提醒；点一下就能更新。下载的文件在安装前会经过校验（SHA-256） | 设置 → 更新（立即检查或关闭提醒） |
| **命令行** | 在命令提示符或脚本中转换 | 见[命令行](#命令行) |

快捷方式（.lnk）会被追踪到真正的文件，空文件会在处理前提示。

## 安装

1. 在 **Releases** 页面（本页右侧）下载 `Mexerica-<versão>-instalador.exe`。
2. 打开下载的文件，一直点击 **Avançar**（下一步）直到结束。安装程序是葡萄牙语的。不需要管理员密码。
3. 如果 Windows 显示“Windows 已保护你的电脑”，点击 **更多信息**，再点击 **仍要运行**。出现这个警告是因为安装程序没有付费的数字签名，并不是因为有什么问题。

安装完成后，会有一个窗口说明怎么使用。电脑开机时 Mexerica 会自动打开（可在通知区域的菜单中关闭）。卸载方法：**设置 → 应用 → 已安装的应用 → Mexerica → 卸载**。右键菜单和设置也会一起删除。

系统要求：Windows 10（2004 版或更新）或 Windows 11，64 位。不需要再安装其他东西。[LibreOffice](https://pt-br.libreoffice.org) 是可选的：有了它，文档、表格和演示文稿转换后更接近原样。如果没有，安装程序会推荐安装；如果你拒绝，Mexerica 会自己转换。

## 命令行

安装时勾选 **Usar o comando "mexerica" no Prompt de Comando e no PowerShell**（在命令提示符和 PowerShell 中使用“mexerica”命令），就能在任何文件夹中调用 `mexerica`：

```powershell
mexerica converter *.heic --para jpg                      # 全部转成 JPG，保存在原文件旁边
mexerica converter video.mov --para mp4 -o D:\Saida      # 指定输出文件夹
mexerica opcoes planilha.xlsx                             # 列出这个文件可以做的事
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # 转换新到的文件，直到按下 Ctrl+C
```

退出代码：全部成功为 0，有文件失败为 1，参数有错为 2。

## 编译

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # 把 ffmpeg、Ghostscript、ExifTool、qpdf、7-Zip、lzip 和 rhwp 放到 tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # 右键扩展（C++；会自动下载 llvm-mingw）
dotnet build src -c Release                                             # 用于测试
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # 成品放在 dist\，并生成 Release 的 zip
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # 无界面的整套测试
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # 用从公开资料库下载的真实文件测试少见格式
```

界面上的新文字需要翻译：`python scripts\extrair-textos.py --check` 显示缺少什么，`python scripts\gerar-traducoes.py` 在 `src\Idiomas` 中生成草稿，草稿需要审核。

程序本身也有无界面的测试模式：

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

## 许可证

代码采用 MIT 许可证。用到的外部库和程序及其许可证列在 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 中。
