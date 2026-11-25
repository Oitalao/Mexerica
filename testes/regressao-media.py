"""Regressões de mídia: python testes/regressao-media.py [--exe EXE] [--tools PASTA]."""
import argparse
import json
import math
import os
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile


def main():
    repo = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path, default=repo / "src/bin/Release/net8.0-windows10.0.19041.0/win-x64/Mexerica.exe")
    parser.add_argument("--tools", type=Path, default=repo.parent / "tools")
    args = parser.parse_args()
    ffmpeg = args.tools / "ffmpeg.exe"
    ffprobe = args.tools / "ffprobe.exe"
    for program in (args.exe, ffmpeg, ffprobe):
        if not program.is_file():
            raise RuntimeError(f"Programa não encontrado: {program}")
    config = Path(os.environ["LOCALAPPDATA"]) / "Mexerica/config.json"
    if config.exists() and json.loads(config.read_text(encoding="utf-8-sig")).get("OutputDir"):
        raise RuntimeError("Esvazie OutputDir no config.json antes do teste e restaure-o depois; o teste não altera configurações.")

    frente = (repo.parent / "teste/frente").resolve()
    frente.mkdir(parents=True, exist_ok=True)
    work = Path(tempfile.mkdtemp(prefix="regressao-media-", dir=frente)).resolve()
    sequence = 0

    def ff(*values):
        return subprocess.check_output([str(ffmpeg), "-hide_banner", "-loglevel", "error", "-nostdin", "-y", *map(str, values)], timeout=180)

    def probe(path):
        return json.loads(subprocess.check_output([str(ffprobe), "-v", "error", "-show_streams", "-show_format", "-of", "json", str(path)], timeout=30))

    def cli(mode, label, files, **params):
        nonlocal sequence
        sequence += 1
        report = work / f"rel-{sequence}.txt"
        command = [str(args.exe.resolve()), "--executar", mode, label, str(report)]
        for key, value in params.items():
            command += ["--p", f"{key}={value}"]
        command += list(map(str, files))
        result = subprocess.run(command, timeout=180)
        lines = report.read_text(encoding="utf-8-sig").splitlines()
        successes = [line for line in lines if line.startswith("OK   ")]
        if result.returncode or len(successes) != 1:
            raise AssertionError("\n".join(lines))
        return Path(successes[0].split(": ", 1)[1])

    def samples(path, *options):
        raw = ff(*options, "-i", path, "-t", "1", "-f", "f32le", "-")
        return struct.unpack("<" + "f" * (len(raw) // 4), raw)

    def packets(path, kind):
        bitstream = ("-bsf:v", "h264_mp4toannexb") if kind == "v" else ()
        return ff("-i", path, "-map", f"0:{kind}:0", "-c", "copy", *bitstream, "-f", "data", "-")

    try:
        long_audio = work / "hora.flac"
        ff("-f", "lavfi", "-i", "sine=frequency=440:sample_rate=8000:duration=3602", "-c:a", "flac", long_audio)
        censored = cli("Tools", "Bipe", [long_audio], trechos="01:00:00.123-01:00:00.456", freq=0)
        values = samples(censored, "-ss", "3600")
        assert max(map(abs, values[1040:3600])) == 0, "Bipe não silenciou o intervalo com precisão de milissegundos."
        assert max(map(abs, values[:800])) > 0.01, "Bipe apagou áudio fora do intervalo."
        print("Bipe: 3600,130–3600,450 s sem sinal; áudio anterior preservado.")

        surround = work / "central.wav"
        ff("-f", "lavfi", "-i", "aevalsrc=0|0|sin(2*PI*440*t)|0|0|0:s=48000:d=2:c=5.1", "-c:a", "pcm_s24le", surround)
        mono = cli("Tools", "Canais", [surround], canais="mono")
        values = samples(mono)
        rms = math.sqrt(sum(value * value for value in values) / len(values))
        assert probe(mono)["streams"][0]["channels"] == 1 and rms > 0.05, "Downmix perdeu o canal central."
        print(f"Mono de 5.1: canal central preservado; RMS={rms:.6f}.")

        aiff = cli("Convert", "AIFF", [surround])
        assert probe(aiff)["streams"][0]["codec_name"] == "pcm_s24be", "Conversão perdeu os 24 bits."
        for label, parameters in (("Cortar", {"inicio": "0.123", "fim": "1.789"}), ("Normalizar", {"alvo": "-14"}), ("Velocidade", {"fator": "1.5"})):
            result = cli("Tools", label, [surround], **parameters)
            assert probe(result)["streams"][0]["codec_name"] == "pcm_s24le", f"{label} perdeu os 24 bits."
        print("PCM24: AIFF, corte, normalização e velocidade mantêm 24 bits.")

        video = work / "video.mp4"
        ff("-f", "lavfi", "-i", "testsrc2=s=160x90:r=24:d=2", "-f", "lavfi", "-i", "sine=frequency=500:sample_rate=44100:duration=2", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-ac", "1", "-shortest", video)
        joined = cli("Tools", "Juntar", [video, video], nome="juntos")
        assert packets(joined, "v") == packets(video, "v") * 2, "Concatenação recodificou os pacotes H264."
        assert packets(joined, "a") == packets(video, "a") * 2, "Concatenação recodificou os pacotes AAC."
        print("Juntar: H264 e AAC idênticos, byte a byte, aos dois originais.")
    finally:
        if not work.is_relative_to(frente) or work == frente:
            raise RuntimeError(f"Pasta de limpeza inesperada: {work}")
        shutil.rmtree(work)


if __name__ == "__main__":
    main()
