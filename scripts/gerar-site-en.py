"""Gera docs/en/index.html (página em inglês para o Google) a partir de docs/index.html.

O português fica no HTML e o inglês em data-en / data-*-en. Aqui o inglês vai para o HTML e o português para
data-pt / data-*-pt, assim o seletor PT/EN continua funcionando nas duas páginas.
Rodar depois de qualquer mudança no site:  python scripts/gerar-site-en.py
"""
import html
import re
import sys
from html.parser import HTMLParser
from pathlib import Path

DOCS = Path(__file__).resolve().parents[1] / 'docs'
SITE = 'https://oitalao.github.io/Mexerica/'
VOID = {'area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'source', 'track', 'wbr',
        'path', 'circle', 'rect', 'stop', 'use', 'polygon', 'line', 'ellipse', 'polyline'}


class Finder(HTMLParser):
    """Acha o começo e o fim do conteúdo de cada elemento com data-en (sem mexer no resto do arquivo)."""

    def __init__(self, text):
        super().__init__(convert_charrefs=False)
        self.text = text
        self.lines = [0]
        for line in text.splitlines(keepends=True):
            self.lines.append(self.lines[-1] + len(line))
        self.stack, self.found, self.raw = [], [], 0

    def where(self):
        line, col = self.getpos()
        return self.lines[line - 1] + col

    def handle_starttag(self, tag, attrs):
        start = self.where()
        end = start + len(self.get_starttag_text())
        if self.raw: return
        if tag in ('script', 'style'): self.raw = 1
        if tag in VOID or self.get_starttag_text().endswith('/>'): return
        self.stack.append((tag, start, end, dict(attrs)))

    def handle_endtag(self, tag):
        if self.raw:
            if tag in ('script', 'style'): self.raw = 0
            return
        if tag in VOID: return
        while self.stack:
            t, start, end, attrs = self.stack.pop()
            if t == tag:
                if 'data-en' in attrs:
                    self.found.append((start, end, self.where(), attrs['data-en']))
                return


def main():
    src = (DOCS / 'index.html').read_text(encoding='utf-8')
    finder = Finder(src)
    finder.feed(src)
    items = sorted(finder.found)
    for a, b in zip(items, items[1:]):
        if b[0] < a[2]: sys.exit(f'data-en aninhado em {a[0]} e {b[0]}: não sei trocar')
    out = src
    for start, tag_end, close, en in reversed(items):
        pt = out[tag_end:close]
        tag = out[start:tag_end]
        tag = tag[:-1] + f' data-pt="{html.escape(pt, quote=True)}">'
        out = out[:start] + tag + en + out[close:]

    # Atributos: content/alt/aria-label com versão em inglês
    def swap(m):
        tag = m.group(0)
        for kind, attr in (('content', 'content'), ('alt', 'alt'), ('label', 'aria-label')):
            en = re.search(rf'\sdata-{kind}-en="([^"]*)"', tag)
            cur = re.search(rf'\s{attr}="([^"]*)"', tag)
            if en and cur:
                tag = tag.replace(cur.group(0), f' {attr}="{en.group(1)}" data-{kind}-pt="{cur.group(1)}"', 1)
        return tag
    out = re.sub(r'<[a-zA-Z][^<>]*\sdata-(?:content|alt|label)-en="[^"]*"[^<>]*>', swap, out)

    # Cabeçalho da página em inglês
    out = out.replace('<html lang="pt-BR">', '<html lang="en">', 1)
    out = re.sub(r'<title>.*?</title>', '<title>Mexerica: free file converter for Windows</title>', out, count=1)
    out = out.replace(f'<link rel="canonical" href="{SITE}">', f'<link rel="canonical" href="{SITE}en/">', 1)
    out = out.replace(f'<meta property="og:url" content="{SITE}">', f'<meta property="og:url" content="{SITE}en/">', 1)
    out = out.replace('<meta property="og:locale" content="pt_BR">\n<meta property="og:locale:alternate" content="en_US">',
                      '<meta property="og:locale" content="en_US">\n<meta property="og:locale:alternate" content="pt_BR">', 1)
    # verificação do Search Console só na página principal
    out = re.sub(r'<meta name="google-site-verification"[^>]*>\n', '', out, count=1)
    ld_pt = re.search(r'<script type="application/ld\+json">.*?</script>', out, re.S).group(0)
    ld_en = ld_pt
    ld_en = re.sub(r'"description": "[^"]*"', '"description": "Free and open source file converter and editor for Windows: images (HEIC, WebP, PNG, JPG), video, audio, PDF (compress, merge, split, OCR, redact, PDF/A), Office documents, spreadsheets, e-books, subtitles and archives. Drag the file, press Space and pick an option."', ld_en, count=1)
    ld_en = ld_en.replace('"alternateName": "Mexerica conversor de arquivos"', '"alternateName": "Mexerica file converter"')
    ld_en = ld_en.replace(f'"url": "{SITE}"', f'"url": "{SITE}en/"')
    ld_en = re.sub(r'"featureList": \[[^\]]*\]', '"featureList": ["Convert HEIC to JPG", "Convert Word to PDF", "Convert PDF to Word", "Compress PDF", "Merge PDF", "Split PDF", "OCR for scanned PDFs", "Redact PDF", "PDF/A", "Compress video", "Timeline video editor", "Convert video to MP4", "Convert audio to MP3", "AI background removal", "Batch rename files", "Watched folders"]', ld_en, count=1)
    ld_en = ld_en.replace('"priceCurrency": "BRL"', '"priceCurrency": "USD"')
    out = out.replace(ld_pt, ld_en, 1)
    # Botões de idioma: começa em EN
    out = out.replace('data-lang="pt" aria-pressed="true"', 'data-lang="pt" aria-pressed="false"', 1)
    out = out.replace('data-lang="en" aria-pressed="false"', 'data-lang="en" aria-pressed="true"', 1)
    # Caminhos relativos sobem uma pasta
    out = re.sub(r'(\s(?:href|src)=")(?!https?:|#|/|data:|mailto:)([^"]+)"', r'\1../\2"', out)
    out = out.replace('<!-- gerado -->', '')
    (DOCS / 'en').mkdir(exist_ok=True)
    # o aviso vai depois do doctype (comentário antes dele joga o navegador no modo de compatibilidade)
    out = out.replace('<!doctype html>\n', '<!doctype html>\n<!-- Gerado por scripts/gerar-site-en.py a partir de docs/index.html; não editar à mão. -->\n', 1)
    (DOCS / 'en' / 'index.html').write_text(out, encoding='utf-8', newline='')
    print(f'ok: {len(items)} textos e a página docs/en/index.html')


if __name__ == '__main__':
    main()
