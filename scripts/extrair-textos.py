"""Extrai textos portugueses exibidos no Mexerica. Uso: python scripts/extrair-textos.py."""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
STRING = re.compile(r'(?P<prefix>\$?)(?<!@)"(?P<body>(?:\\.|[^"\\])*)"')
VISIBLE = re.compile(
    r'\b(?:Title|Text|Content|Header|ToolTip|Sub|Label)\s*=|'
    r'\b(?:Ui\.(?:H|Row|Stack|Check|Button|Combo)|Toast\.(?:Show|Track|Status)|MessageBox\.Show|Tr\.Box|'
    r'new (?:Target|Op|\w*Exception|ToolStripMenuItem|TextBlock|Button|CheckBox)|Line\(|Hint\(|Friendly\(|Tr\.T\()'
)
PORTUGUESE = re.compile(r'[A-Za-zÀ-ÿ]{2}')
SKIP = re.compile(r'^(?:https?://|[A-Za-z]:\\|--|[0-9 .,%:+*/()\\-]+$)')


def template(body):
    body = body.replace(r'\n', '\n').replace(r'\t', '\t').replace(r'\"', '"')
    # C# interpolations: braces can contain calls and format specifiers.
    result, pos, number = [], 0, 0
    while pos < len(body):
        if body[pos:pos + 2] in ('{{', '}}'):
            result.append(body[pos]); pos += 2
        elif body[pos] == '{':
            depth, end = 1, pos + 1
            while end < len(body) and depth:
                if body[end] == '{': depth += 1
                elif body[end] == '}': depth -= 1
                end += 1
            if depth:
                result.append(body[pos]); pos += 1
            else:
                result.append('{' + str(number) + '}'); number += 1; pos = end
        else:
            result.append(body[pos]); pos += 1
    return ''.join(result)


def extract():
    found = set()
    broad = {'Catalog.cs', 'Settings.cs', 'WheelWindow.cs', 'ShellMenu.cs', 'App.cs', 'Toast.cs',
             'Personalizar.cs', 'Dialogs.cs', 'Form.cs', 'WelcomeWindow.cs', 'Recentes.cs',
             'Vigia.cs', 'Atualizar.cs', 'MediaWindow.cs', 'PdfWindows.cs', 'ImageEditor.cs',
             'InfoWindows.cs', 'FloatWindow.cs', 'LibreOfficeSetup.cs', 'Teste.cs'}
    for path in (ROOT / 'src').rglob('*.cs'):
        if 'obj' in path.parts or 'bin' in path.parts: continue
        text = path.read_text(encoding='utf-8-sig')
        # Motores adaptados do Filee: as mensagens internas são em inglês e viram um aviso genérico em App.Friendly.
        if 'namespace Filee' in text: continue
        for line in text.splitlines():
            if path.name not in broad and not VISIBLE.search(line): continue
            for match in STRING.finditer(line):
                source = template(match['body'])
                if PORTUGUESE.search(source) and not SKIP.search(source) and '\\' not in source and not source.startswith(('.', '-', '/', '<')): found.add(source)
    return sorted(found, key=str.casefold)


if __name__ == '__main__':
    entries = extract()
    if '--check' in sys.argv:
        missing = {}
        formats = ('PDF', 'JPG', 'PNG', 'MP4', 'ZIP', 'WebP', 'AVIF', 'HEIC', 'DPI')
        for code in ('en', 'es', 'fr', 'de', 'it', 'ja', 'ko', 'zh-CN', 'ru', 'pl', 'hi', 'ar'):
            file = ROOT / 'src' / 'Idiomas' / f'{code}.json'
            data = json.loads(file.read_text(encoding='utf-8')) if file.exists() else {}
            absent = [text for text in entries if not data.get(text) or
                      sorted(re.findall(r'\{\d+\}', text)) != sorted(re.findall(r'\{\d+\}', data.get(text, ''))) or
                      any(re.search(r'(?<!\w)' + fmt + r'(?!\w)', text) and fmt not in data.get(text, '') for fmt in formats)]
            if absent: missing[code] = absent
        if missing:
            for code, absent in missing.items(): print(f'{code}: {len(absent)} sem tradução; exemplos: {absent[:5]}')
            sys.exit(1)
        print(f'{len(entries)} textos com tradução nos 12 idiomas')
    else:
        print(json.dumps(entries, ensure_ascii=False, indent=2))
