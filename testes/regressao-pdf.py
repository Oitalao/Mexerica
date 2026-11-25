"""Regressões PDF/arquivos; usa somente Python padrão e os binários locais do Mexerica."""
import argparse
import json
import os
from pathlib import Path
import random
import re
import subprocess
import tarfile
import tempfile
from urllib.parse import unquote
import zipfile


def main():
    repo = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--exe', type=Path, default=repo/'src/bin/Release/net8.0-windows10.0.19041.0/win-x64/Mexerica.exe')
    parser.add_argument('--ferramentas', type=Path, default=repo/'tools')
    args = parser.parse_args()
    config = Path(os.environ['LOCALAPPDATA'])/'Mexerica/config.json'
    assert not json.loads(config.read_text('utf-8-sig')).get('OutputDir'), 'OutputDir deve estar vazio'
    frente = repo.parent/'teste/frente'
    frente.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='regressao-pdf-', dir=frente) as folder:
        b = Path(folder)
        counter = 0

        def cli(mode, label, files, parameters=(), failure=False):
            nonlocal counter
            counter += 1
            report = b/f'rel-{counter}.txt'
            command = [str(args.exe), '--executar', mode, label, str(report)]
            for parameter in parameters:
                command += ['--p', parameter]
            subprocess.run(command + [str(f) for f in files], timeout=120)
            text = report.read_text('utf-8')
            if failure:
                assert 'ERRO ' in text, text
                return text
            assert 'OK   '+label in text and 'ERRO ' not in text, text
            return Path(text.split('): ', 1)[1].strip().split(' | ')[0])

        def gs(*options):
            subprocess.run([str(args.ferramentas/'gswin64c.exe'), '-q', '-dNOPAUSE', '-dBATCH', *map(str, options)], check=True, timeout=120)

        def extract(pdf):
            return cli('Convert', 'TXT', [pdf]).read_text('utf-8-sig')

        ps = b/'digital.ps'
        ps.write_text('''%!PS
<< /PageSize [612 792] >> setpagedevice
/Helvetica findfont 24 scalefont setfont
72 600 moveto (SEGREDO CONFIDENCIAL OCULTO) show
72 480 moveto (Este documento tem texto publico.) show
72 440 moveto (O processo de teste permite a leitura.) show
72 400 moveto (As palavras de fora devem permanecer.) show
showpage
/Helvetica findfont 24 scalefont setfont
72 700 moveto (SEGUNDA PAGINA DIGITAL INTACTA) show
showpage''', encoding='ascii')
        digital = b/'digital.pdf'
        gs('-sDEVICE=pdfwrite', '-o', digital, ps)
        red = cli('Tools', 'Tarjar', [digital], ['tarjas=1:0.09,0.20,0.85,0.10'])
        text = extract(red)
        assert all(word not in text.upper() for word in ['SEGREDO', 'CONFIDENCIAL', 'OCULTO']), text
        assert 'SEGUNDA PAGINA DIGITAL INTACTA' in text and 'documento' in text.lower(), text
        print('8: tarja sem texto coberto extraível; página 2 e OCR público preservados', flush=True)

        scan = b/'scan.png'
        gs('-sDEVICE=png16m', '-r180', '-dFirstPage=1', '-dLastPage=1', '-o', scan, digital)
        raster = cli('Convert', 'PDF', [scan])
        header = 'CABECALHO DIGITAL DO PROCESSO COM MAIS DE VINTE LETRAS'
        mixed = cli('Tools', 'Carimbar', [raster], ['numerar=0', 'carimbo='+header, 'posicao=se', 'tamanho=10'])
        ocr = cli('Tools', 'OCR', [mixed])
        text = extract(ocr)
        assert text.count(header) == 1 and 'SEGREDO' in text, text
        for src, name in [(mixed, 'before.png'), (ocr, 'after.png')]:
            gs('-sDEVICE=png16m', '-r120', '-o', b/name, src)
        assert (b/'before.png').read_bytes() == (b/'after.png').read_bytes(), 'OCR alterou os pixels visíveis'
        for rotation in [90, 180, 270]:
            rotated = cli('Tools', 'Organizar', [raster], ['ordem=1r'+str(rotation)])
            text = extract(cli('Tools', 'OCR', [rotated]))
            assert 'SEGREDO' in re.sub(r'\s+', '', text), (rotation, text)
        print('9: corpo OCR, cabeçalho único, pixels idênticos e giros 90/180/270', flush=True)

        a, c = b/'a', b/'b'
        a.mkdir(); c.mkdir(); (a/'nome.txt').write_text('um'); (c/'nome.txt').write_text('dois'); (a/'vazia').mkdir()
        items = [a/'nome.txt', c/'nome.txt', a/'vazia']
        with zipfile.ZipFile(cli('Tools', 'ZIP', items)) as archive:
            assert sorted(archive.namelist()) == ['nome (1).txt', 'nome.txt', 'vazia/']
            assert archive.read('nome.txt') == b'um' and archive.read('nome (1).txt') == b'dois'
        with tarfile.open(cli('Tools', 'TAR.GZ', items)) as archive:
            assert archive.getmember('vazia').isdir()
            assert archive.extractfile('nome.txt').read() == b'um' and archive.extractfile('nome (1).txt').read() == b'dois'
        print('3/4: nomes únicos, conteúdos corretos e pasta vazia em ZIP e TAR.GZ', flush=True)

        raw = b/'noise.rgb'; raw.write_bytes(random.Random(5).randbytes(900*1200*3))
        subprocess.run([str(args.ferramentas/'ffmpeg.exe'), '-hide_banner', '-loglevel', 'error', '-y', '-f', 'rawvideo', '-pixel_format', 'rgb24', '-video_size', '900x1200', '-i', str(raw), '-frames:v', '1', str(b/'noise.png')], check=True)
        noise_raw = cli('Convert', 'PDF', [b/'noise.png'])
        noise = b/'noise150.pdf'
        gs('-sDEVICE=pdfwrite', '-dPDFFitPage', '-dFIXEDMEDIA', '-dDEVICEWIDTHPOINTS=432', '-dDEVICEHEIGHTPOINTS=576', '-o', noise, noise_raw)
        small = cli('Tools', 'Comprimir', [noise], ['modo=mb', 'mb=0.01'])
        assert small.stat().st_size <= noise.stat().st_size
        failed = cli('Tools', 'Limite', [noise], ['mb=0.01'], failure=True)
        assert '120 DPI' in failed and '1' in failed
        fit = cli('Tools', 'Limite', [noise], ['mb=0.25'])
        assert fit.stat().st_size <= .25*1024*1024
        print(f'1/10: {noise.stat().st_size} -> {small.stat().st_size} bytes; limite 0,25MB={fit.stat().st_size}; 0,01MB recusado', flush=True)

        ps.write_text('''%!PS
<< /PageSize [612 792] >> setpagedevice
/Helvetica findfont 12 scalefont setfont
72 720 moveto (COLUNA_A) show 250 720 moveto (COLUNA_B) show 430 720 moveto (COLUNA_C) show
250 695 moveto (B2) show 430 695 moveto (C2) show
72 670 moveto (A3) show 430 670 moveto (C3) show
72 520 moveto (X) show 300 520 moveto (Y) show
300 495 moveto (Y2) show
showpage''', encoding='ascii')
        gs('-sDEVICE=pdfwrite', '-o', b/'table.pdf', ps)
        with zipfile.ZipFile(cli('Convert', 'XLSX', [b/'table.pdf'])) as z:
            xml = z.read('xl/worksheets/sheet1.xml').decode()
            assert all(f'r="{cell}"' in xml for cell in ['B2', 'C2', 'C3']) and '>B2<' in xml and '>C3<' in xml, xml
        print('17: células vazias conservadas em tabelas de 3 e 2 colunas', flush=True)

        ansi = b/'brasileiro.csv'
        ansi.write_bytes('Descrição,Valor\r\nAção,25\r\nCafé,7'.encode('cp1252'))
        with zipfile.ZipFile(cli('Convert', 'XLSX', [ansi])) as z:
            xml = ''.join(z.read(name).decode('utf-8') for name in z.namelist() if name in ['xl/sharedStrings.xml', 'xl/worksheets/sheet1.xml'])
            assert all(word in xml for word in ['Descrição', 'Ação', 'Café']), xml
        print('18: CSV CP1252 também preservado no caminho do LibreOffice', flush=True)

        html = b/'image.html'
        html.write_text('<html><body><h1>Imagem</h1><img src="scan.png" alt="scan"><p>Fim.</p></body></html>', encoding='utf-8')
        md = cli('Convert', 'MD', [html])
        links = re.findall(r'!\[[^]]*\]\(([^)]+)\)', md.read_text())
        assert links and all((md.parent/unquote(link)).exists() for link in links)
        docx = cli('Convert', 'DOCX', [md])
        with zipfile.ZipFile(docx) as z:
            assert b'/relationships/image' in z.read('word/_rels/document.xml.rels')
        office_md = cli('Convert', 'MD', [docx])
        assert '![' in office_md.read_text()
        with zipfile.ZipFile(b/'image.epub', 'w') as z:
            z.writestr('mimetype', 'application/epub+zip')
            z.writestr('META-INF/container.xml', '<container><rootfiles><rootfile full-path="OEBPS/book.opf"/></rootfiles></container>')
            z.writestr('OEBPS/book.opf', '<package><metadata><title>Livro</title></metadata><manifest><item id="c" href="chapter.xhtml" media-type="application/xhtml+xml"/></manifest><spine><itemref idref="c"/></spine></package>')
            z.writestr('OEBPS/chapter.xhtml', '<html><body><img src="picture.png"/></body></html>')
            z.write(scan, 'OEBPS/picture.png')
        epub_md = cli('Convert', 'MD', [b/'image.epub'])
        assert '![' in epub_md.read_text() and 'data:image' not in epub_md.read_text()
        print('20: HTML/Office/EPUB com imagens locais; Markdown com imagens resolvidas no DOCX', flush=True)
    print('Regressões PDF aprovadas; amostras apagadas.')


if __name__ == '__main__':
    main()
