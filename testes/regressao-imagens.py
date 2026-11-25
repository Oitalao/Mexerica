"""Regressões reais de imagens; requer o programa compilado e a predefinição Site.

Uso: python testes/regressao-imagens.py [--exe Mexerica.exe] [--ferramentas pasta]
Não altera configurações. As amostras ficam em teste/frente e são apagadas no fim.
"""

import argparse
import gzip
import json
import os
from pathlib import Path
import subprocess
import tempfile


def main():
    repo = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path)
    parser.add_argument("--ferramentas", type=Path, default=repo.parent / "tools")
    args = parser.parse_args()
    exe = args.exe or repo / "src/bin/Release/net8.0-windows10.0.19041.0/win-x64/Mexerica.exe"
    ffmpeg = args.ferramentas / "ffmpeg.exe"
    ffprobe = args.ferramentas / "ffprobe.exe"
    qpdf = args.ferramentas / "qpdf/qpdf.exe"
    for file in (exe, ffmpeg, ffprobe, qpdf):
        if not file.is_file():
            raise SystemExit(f"Arquivo necessário não encontrado: {file}")
    config_file = Path(os.environ["LOCALAPPDATA"]) / "Mexerica/config.json"
    config = json.loads(config_file.read_text(encoding="utf-8-sig"))
    if config.get("OutputDir"):
        raise SystemExit("Esvazie OutputDir nas configurações antes deste teste.")
    if not any(p.get("Name") == "Site" and p.get("Group") == "Image" for p in config.get("Presets", [])):
        raise SystemExit("Este teste requer uma predefinição de imagem chamada Site.")

    def run(*command):
        result = subprocess.run([str(x) for x in command], capture_output=True,
                                text=True, encoding="utf-8", errors="replace", timeout=90)
        if result.returncode:
            raise AssertionError(f"Comando falhou ({result.returncode}): {command}\n{result.stdout}\n{result.stderr}")
        return result.stdout

    def convert(source, target, extension=None):
        before = set(source.parent.iterdir())
        output = run(exe, "converter", source, "--para", target)
        created = [p for p in set(source.parent.iterdir()) - before
                   if p.suffix.lower() == "." + (extension or target).lower()]
        assert len(created) == 1, f"Conversão {target} não gerou exatamente um arquivo: {output}"
        assert created[0].stat().st_size > 0
        return created[0]

    def check_animation(file):
        data = json.loads(run(ffprobe, "-v", "error", "-select_streams", "v:0",
                              "-show_frames", "-show_entries", "frame=pts_time,duration_time",
                              "-of", "json", file))
        frames = data["frames"]
        assert len(frames) == 3, f"{file.name}: {len(frames)} quadros, esperados 3"
        for i, frame in enumerate(frames):
            assert abs(float(frame["pts_time"]) - i * .17) < .002, (file.name, frame)
            assert abs(float(frame["duration_time"]) - .17) < .002, (file.name, frame)

    front = repo.parent / "teste/frente"
    front.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="regressao-imagens-", dir=front) as temporary:
        folder = Path(temporary)
        svgz = folder / "vetor.svgz"
        svgz.write_bytes(gzip.compress(b'<svg xmlns="http://www.w3.org/2000/svg" width="120" height="80"><rect width="120" height="80" fill="red"/></svg>'))
        png = convert(svgz, "png")
        assert png.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"), "SVGZ não virou PNG"
        assert convert(svgz, "pdf").read_bytes().startswith(b"%PDF-"), "SVGZ não virou PDF"

        gif = folder / "animacao.gif"
        run(ffmpeg, "-hide_banner", "-nostdin", "-loglevel", "error", "-y", "-f", "lavfi",
            "-i", "testsrc2=size=160x100:rate=100/17", "-frames:v", "3", gif)
        check_animation(gif)
        webp = convert(gif, "webp")
        apng = convert(gif, "apng")
        for animation in (webp, apng, convert(webp, "gif"), convert(apng, "gif")):
            check_animation(animation)

        tiff = convert(gif, "tiff")
        pdf = convert(tiff, "pdf")
        assert run(qpdf, "--show-npages", pdf).strip() == "3", "TIFF não preservou as 3 páginas"
        icon = convert(png, "icns")
        preset = next(p for p in config["Presets"] if p.get("Name") == "Site" and p.get("Group") == "Image")
        convert(icon, "Site", preset["Format"])
        print("OK: SVGZ PNG/PDF; GIF/WebP/APNG com 3 quadros de 170 ms; TIFF/PDF com 3 páginas; ICNS/Site.")


if __name__ == "__main__":
    main()
