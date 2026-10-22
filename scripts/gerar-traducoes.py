"""Gera rascunhos de tradução para revisão; mantém os marcadores {0}, {1}."""
import json
import pathlib
import re
import time
import urllib.parse
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]
LANGUAGES = ('en', 'es', 'fr', 'de', 'it', 'ja', 'ko', 'zh-CN', 'ru', 'pl', 'hi', 'ar')
PROPER = re.compile(r'(?<![\w.])(?:Mexerica|LibreOffice|Word|Excel|PowerPoint|WebP|[A-Z][A-Z0-9.]{1,8})(?!\w|\.[A-Za-z])')


def translate(lines, language):
    names = []
    prepared = []
    for line in lines:
        tokens = []
        def mask(match):
            tokens.append(match.group())
            return f'ZXQ{len(tokens)-1}QXZ'
        names.append(tokens)
        prepared.append(PROPER.sub(mask, line.replace('\n', 'ZXQNEWLINEQXZ')))
    query = urllib.parse.urlencode({'client': 'gtx', 'sl': 'pt', 'tl': language, 'dt': 't', 'q': '\n'.join(prepared)})
    with urllib.request.urlopen('https://translate.googleapis.com/translate_a/single?' + query, timeout=30) as response:
        value = json.load(response)
    result = ''.join(part[0] for part in value[0]).split('\n')
    if len(result) != len(lines): raise ValueError(f'{language}: {len(lines)} entradas viraram {len(result)}')
    for i, tokens in enumerate(names):
        for n, original in enumerate(tokens): result[i] = result[i].replace(f'ZXQ{n}QXZ', original)
        result[i] = result[i].replace('ZXQNEWLINEQXZ', '\n')
    return result


def main():
    # O extrator tem hífen no nome para uso direto; importação por caminho.
    import importlib.util
    spec = importlib.util.spec_from_file_location('extract', ROOT / 'scripts' / 'extrair-textos.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    entries = module.extract()
    folder = ROOT / 'src' / 'Idiomas'
    for language in LANGUAGES:
        file = folder / f'{language}.json'
        existing = json.loads(file.read_text(encoding='utf-8')) if file.exists() else {}
        pending = [key for key in entries if key not in existing]
        print(language, len(pending), flush=True)
        while pending:
            batch = []
            while pending and sum(len(s) + 1 for s in batch) + len(pending[0]) < 1800 and len(batch) < 25:
                batch.append(pending.pop(0))
            if not batch: batch.append(pending.pop(0))
            for attempt in range(3):
                try:
                    values = translate(batch, language)
                    existing.update(zip(batch, values))
                    break
                except Exception as error:
                    if attempt == 2:
                        print(error, flush=True)
                        for key in batch:
                            try: existing[key] = translate([key], language)[0]
                            except Exception: existing[key] = key
                    time.sleep(attempt + 1)
            file.write_text(json.dumps(existing, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__': main()
