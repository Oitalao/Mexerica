[Português (Brasil)](README.md) · [English](README.en.md) · [Español](README.es.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md) · [Polski](README.pl.md) · [हिन्दी](README.hi.md) · **العربية**

<div dir="rtl">

<p align="center"><img src="assets/icone/mexerica-1024.png" width="128" alt="Mexerica"></p>

<h1 align="center">Mexerica</h1>

<p align="center">حوّل الملفات وعدّلها دون فتح أي برنامج: اسحب الملف، واضغط <b>مسافة</b>، ثم أفلته فوق الخيار.</p>

<p align="center">
  <img src="assets/telas/roda-converter.png" width="260" alt="عجلة تحويل">
  <img src="assets/telas/roda-ferramentas.png" width="260" alt="عجلة أدوات">
  <img src="assets/telas/roda-acoes.png" width="260" alt="عجلة الإجراءات">
</p>

Mexerica ينقل إلى Windows فكرة برنامج [Tangerine](https://tangerineformac.com) الخاص بأجهزة Mac. يبقى في منطقة الإعلامات بجوار الساعة. عندما تسحب ملفًا أو أكثر وتضغط مسافة دون أن تترك الفأرة، تظهر عجلة حول المؤشر فيها ما يمكن فعله بهذه الملفات. تُحفظ النتيجة بجوار الملف الأصلي، ولا يتغير الأصل أبدًا. تجري عمليات التحويل على جهازك، ولا يُرسل أي ملف من ملفاتك إلى الإنترنت. لا يستخدم Mexerica الإنترنت إلا للبحث عن إصدارات جديدة ولتنزيل ما تسحبه من موقع ويب.

**الجديد في الإصدار 1.1:** صيغ أكثر بكثير (RAW و PSD و Kindle و Outlook والترجمات النصية والخطوط و 3D والخرائط و CAD و ISO و DMG وغيرها)، و Word و Excel و PowerPoint دون LibreOffice، ومجلدات تحوّل من تلقاء نفسها، والتحويلات الأخيرة، وعجلة قابلة للتخصيص مع إعدادات مسبقة، والسحب مباشرة من المتصفح، و 13 لغة، وسطر الأوامر، وتنبيه بالتحديثات. اطّلع على [طريقة استخدام كل ميزة](#الميزات-وأماكنها).

**اللغة:** يفتح Mexerica باللغة البرتغالية. للتبديل إلى العربية، افتح **Configurações** (الإعدادات) واختر **Idioma** (اللغة).

## طريقة الاستخدام

| الإجراء | ماذا تفعل |
|---|---|
| فتح العجلة | اسحب الملفات، ودون أن تفلتها، اضغط **مسافة** |
| تبديل العجلة | **مسافة** مرة أخرى، أو **Shift** (تحويل)، **Ctrl** (أدوات)، **Alt** (الإجراءات) |
| الاختيار | أفلت الملف فوق الخيار، أو استخدم **الأسهم** و **Enter** |
| الإلغاء | **Esc** |
| من لوحة المفاتيح | حدّد الملفات في مستكشف الملفات أو على سطح المكتب واضغط **Ctrl + Shift + مسافة** |
| شاشة اللمس | أبقِ إصبعك ثابتًا لحظة ثم اسحب |
| زر الفأرة الأيمن | حدّد الملفات، وانقر بزر الفأرة الأيمن، واستخدم القائمة الفرعية **Mexerica** (في Windows 11 داخل "عرض المزيد من الخيارات") |

تُعالج عدة ملفات في الوقت نفسه. تغيير صيغة الفيديو فقط (MKV و MOV و MP4) يتم دون إعادة ترميز متى أمكن، في ثوانٍ ودون فقدان للجودة. وعندما تلزم إعادة الترميز، يستخدم Mexerica بطاقة الرسومات (NVIDIA أو Intel أو AMD) إن وُجدت.

تتغير المفاتيح والجودة ومجلد النتائج من الإعدادات (انقر نقرًا مزدوجًا على أيقونة منطقة الإعلامات، أو افتح Mexerica مرة أخرى).

## ماذا يفعل

**تحويل**
- الصور: JPG و PNG و WebP و HEIC و AVIF و JPEG XL و JPEG 2000 و TIFF و BMP و GIF و APNG و ICO و ICNS و PSD و TGA و DDS و QOI و PCX و SVG (بتحويل الصورة إلى رسم متجهي) و PDF
- صور للإدخال فقط: RAW من الكاميرات (CR2 و CR3 و NEF و ARW و DNG وغيرها) و PSB و DICOM و EXR و HDR و XCF و JPEG XR و CUR و PPM و EPS و PS و AI و EMF و WMF و SVGZ
- الصوت: MP3 و M4A (AAC و ALAC) و WAV و FLAC و OGG و Opus و AIFF و WMA و WavPack و TTA و AC3 و E-AC3 و W64 (ويقرأ أيضًا APE و DSF و DFF و DTS و AMR و MKA و CAF و AU و MPC)
- الفيديو: MP4 (H.264 أو HEVC أو AV1) و MOV و MKV و WebM و AVI و WMV و GIF و WebP المتحركان و APNG و MP4 بدقة 720p و MP3 (ويقرأ أيضًا VOB و OGV و MXF و ASF و RM/RMVB و DivX و F4V)
- Live Photo من iPhone و Motion Photo من Android: يستخرج الفيديو من الصورة
- PDF إلى DOCX و PPTX و XLSX و EPUB و TXT و Markdown و JPG و PNG (كل الصفحات)
- مستندات وجداول وعروض Office، مع LibreOffice أو بدونه؛ HWP و HWPX؛ HTML و Markdown و RTF و ODT
- الكتب الإلكترونية: EPUB و KEPUB (Kobo) و MOBI و AZW و AZW3 و FB2 و HTMLZ و TXTZ؛ القصص المصورة CBZ و CBR و CB7 و CBT إلى PDF و EPUB
- رسائل البريد EML و MSG (Outlook) إلى HTML أو TXT أو PDF، مع المرفقات في ملف ZIP
- الترجمات النصية: SRT و ASS/SSA و VTT و SMI و LRC و TTML و SUB و SCC، مع الحفاظ على الحروف الخاصة في الملفات القديمة
- البيانات: JSON و JSONL و YAML و TOML و XML و CSV و TSV فيما بينها؛ جهات اتصال vCard إلى جدول والعكس
- الخطوط: TTF و OTF و WOFF و WOFF2 و EOT
- النماذج ثلاثية الأبعاد: GLB و glTF و STL و OBJ و 3MF و PLY (و FBX للإدخال) ومعاينة في صورة
- الخرائط: GeoJSON و KML و KMZ و GPX و Shapefile و CSV بإحداثيات
- CAD: DWG و DXF فيما بينهما وإلى PDF و SVG وصورة؛ SVG إلى DXF و DWG
- الملفات المضغوطة: ZIP و 7Z و TAR و TAR.GZ و TAR.XZ و TAR.BZ2 (ويقرأ أيضًا RAR و ISO و CAB و DMG و WIM و ARJ و LZH و CPIO و RPM و DEB و ZST و LZ و LZMA و Z)

لا تعرض العجلة إلا ما يناسب ذلك الملف: الفيديو بلا صوت لا يعرض MP3، والصورة الثابتة لا تعرض APNG، والجدول الذي ليس قائمة جهات اتصال لا يعرض vCard.

**أدوات**
- ضغط الصور والفيديو والصوت و PDF حسب المستوى، أو بالنسبة المئوية، أو إلى حجم محدد
- محرر الصور: قص، وضبط الألوان، وأسهم، ومستطيلات، وقلم، ونص، وإخفاء (أسود أو ضبابي أو بكسلات)
- خلفية وهامش للصور، وتجميع عدة صور في صورة واحدة، وصور إلى PDF، وتدرج رمادي، وقراءة رمز QR
- البيانات الوصفية: عرض كل شيء، وتعديل العنوان والمؤلف وحقول أخرى، وحذف الموقع أو كل شيء
- الفيديو: قص مقطع، وقص الصورة، والسرعة، والتقسيم، وحفظ لقطة واحدة أو كل اللقطات، واستخراج الصوت والترجمات، والتدوير، وتغيير الدقة، وإزالة الصوت، والدمج
- الصوت: قص مقطع، وتسوية مستوى الصوت، وصافرة فوق مقاطع، وأحادي وستيريو، والسرعة، والتقسيم، والدمج
- PDF: ترتيب الصفحات (إعادة الترتيب، والتدوير، والحذف)، والتقسيم، والدمج، والإخفاء الحقيقي (يختفي النص المخفي تمامًا)
- الترجمات النصية: تأخير التوقيت أو تقديمه؛ الخطوط: ورقة نموذج؛ الملفات المضغوطة: الاستخراج وفحص السلامة

**الإجراءات**: المشاركة (المشاركة عن قرب في Windows)، وإبقاء الملف عائمًا على الشاشة، والنسخ أو النقل إلى مجلد، ونسخ المسار، وفتح الطرفية في المجلد.

## الميزات وأماكنها

تُفتح الإعدادات بنقرة مزدوجة على أيقونة Mexerica بجوار الساعة، أو بفتح Mexerica مرة أخرى من قائمة ابدأ. وفي قائمة الأيقونة (زر الفأرة الأيمن) كل ما تبقى.

| الميزة | الفائدة | المكان |
|---|---|---|
| **المجلدات المراقبة** | كل ما يصل إلى مجلد يُحوَّل تلقائيًا. مثال: كل صورة HEIC تصل إلى التنزيلات تصبح JPG | الإعدادات ← النتيجة ← المجلدات المراقبة ← **اختر…** ← **إضافة مجلد…**، اختر الصيغة واحفظ |
| **التحويلات الأخيرة** | قائمة بما تم، مع بحث وصور مصغرة وأزرار للفتح، أو الفتح ببرنامج آخر، أو الإظهار في المجلد | زر الفأرة الأيمن على الأيقونة ← **التحويلات الأخيرة…** |
| **تخصيص العجلة** | إخفاء ما لا تستخدمه وتغيير ترتيب الأجزاء | الإعدادات ← مظهر العجلة ← **تخصيص…** |
| **الإعدادات المسبقة** | جزء خاص بك في العجلة. مثال: "موقع" = JPG، جودة 80، حتى 1600 px، بلا موقع جغرافي؛ أو "MP3 خفيف" | في النافذة نفسها: **إعداد مسبق جديد…** |
| **السمة والشفافية** | عجلة فاتحة أو داكنة أو مثل Windows، وأكثر أو أقل شفافية | الإعدادات ← مظهر العجلة ← السمة و العتامة |
| **عجلة بعدة أوجه** | عندما تكثر الخيارات، يصبح للعجلة أكثر من وجه | **Tab** أو عجلة الفأرة والعجلة مفتوحة |
| **مباشرة من المتصفح** | تحويل صورة أو ملف من موقع ويب دون تنزيله أولًا | اسحب الصورة أو الرابط من الموقع، واضغط **مسافة**، وأفلته فوق الخيار. يُحفظ الملف في التنزيلات |
| **اللغة** | البرتغالية والإنجليزية والإسبانية والفرنسية والألمانية والإيطالية واليابانية والكورية والصينية والروسية والبولندية والهندية والعربية. يفتح Mexerica بالبرتغالية | **Configurações** (الإعدادات) ← **Idioma** (اللغة) |
| **التحديثات** | تنبيه بجوار الساعة عند صدور إصدار جديد؛ ونقرة واحدة تحدّث البرنامج. يُفحص الملف المنزّل (SHA-256) قبل التثبيت | الإعدادات ← التحديثات (تحقق الآن أو أوقف التنبيه) |
| **سطر الأوامر** | التحويل من موجه الأوامر أو في البرامج النصية | انظر [سطر الأوامر](#سطر-الأوامر) |

تُتبَع الاختصارات (.lnk) حتى الملف الحقيقي، ويُنبَّه إلى الملف الفارغ قبل المحاولة.

## التثبيت

1. نزّل <code>Mexerica-&lt;versão&gt;-instalador.exe</code> من صفحة **Releases** (على يمين هذه الصفحة).
2. افتح الملف المنزّل وانقر **Avançar** (التالي) حتى النهاية. برنامج التثبيت باللغة البرتغالية. لا حاجة إلى كلمة مرور المسؤول.
3. إذا عرض Windows الرسالة "قام Windows بحماية الكمبيوتر"، فانقر **مزيد من المعلومات** ثم **تشغيل على أي حال**. يظهر هذا التنبيه لأن برنامج التثبيت لا يحمل توقيعًا رقميًا مدفوعًا، وليس لأن هناك مشكلة.

عند الانتهاء، تشرح نافذة طريقة الاستخدام. يفتح Mexerica تلقائيًا عند تشغيل الكمبيوتر (يمكن إيقاف ذلك من قائمة منطقة الإعلامات). لإزالته: **الإعدادات ← التطبيقات ← التطبيقات المثبتة ← Mexerica ← إلغاء التثبيت**. يزيل هذا أيضًا قائمة زر الفأرة الأيمن والإعدادات.

المتطلبات: Windows 10 (الإصدار 2004 أو أحدث) أو Windows 11، بنظام 64 بت. لا حاجة إلى تثبيت أي شيء آخر. [LibreOffice](https://pt-br.libreoffice.org) اختياري: معه تخرج المستندات والجداول والعروض أقرب إلى الأصل. إن لم يكن موجودًا، يعرضه برنامج التثبيت؛ وإن رفضت، يحوّل Mexerica بنفسه.

## سطر الأوامر

أثناء التثبيت، حدّد الخيار **Usar o comando "mexerica" no Prompt de Comando e no PowerShell** (استخدام الأمر "mexerica" في موجه الأوامر و PowerShell) لتتمكن من استدعاء `mexerica` من أي مجلد:

<div dir="ltr">

```powershell
mexerica converter *.heic --para jpg                      # converts everything to JPG, next to the originals
mexerica converter video.mov --para mp4 -o D:\Saida      # chooses the output folder
mexerica opcoes planilha.xlsx                             # lists what can be done with the file
mexerica ferramenta Comprimir foto.jpg --p modo=pct --p pct=50
mexerica vigiar D:\Fotos --para webp --subpastas          # converts whatever arrives until Ctrl+C
```

</div>

السطر الأول يحوّل كل شيء إلى JPG بجوار الأصول؛ والثاني يختار مجلد النتائج؛ والثالث يعرض ما يمكن فعله بالملف؛ والأخير يحوّل كل ما يصل إلى المجلد حتى تضغط Ctrl+C.

رمز الخروج 0 عندما ينجح كل شيء، و 1 عندما يفشل ملف ما، و 2 عندما يوجد خطأ في المعاملات.

## البناء

تحتاج إلى [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

<div dir="ltr">

```powershell
powershell -ExecutionPolicy Bypass -File scripts\baixar-ferramentas.ps1   # ffmpeg, Ghostscript, ExifTool, qpdf, 7-Zip, lzip, rhwp -> tools\
powershell -ExecutionPolicy Bypass -File scripts\compilar-menu.ps1        # right click extension (C++; downloads llvm-mingw)
dotnet build src -c Release                                             # for testing
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1           # ready version in dist\ and the Release zip
powershell -ExecutionPolicy Bypass -File testes\rodar.ps1               # test suite without a screen
powershell -ExecutionPolicy Bypass -File testes\amostras-externas.ps1   # rare formats with real files from public collections
```

</div>

تحتاج النصوص الجديدة في الواجهة إلى ترجمة: <code>python scripts\extrair-textos.py --check</code> يعرض ما ينقص، و <code>python scripts\gerar-traducoes.py</code> ينشئ مسودة في <code>src\Idiomas</code> يجب مراجعتها.

للبرنامج نفسه أيضًا وضع اختبار بلا شاشة:

<div dir="ltr">

```powershell
Mexerica.exe --executar <Convert|Tools> <opção|*> relatorio.txt [--p chave=valor ...] arquivos...
```

</div>

## الترخيص

الشيفرة بموجب ترخيص MIT. المكتبات والبرامج الخارجية المستخدمة مذكورة مع تراخيصها في [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

</div>
