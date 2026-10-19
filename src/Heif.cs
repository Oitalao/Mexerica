using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mexerica;

/// Monta um .heic (HEIF com uma imagem HEVC) a partir de um MP4 de um quadro só gerado pelo ffmpeg.
/// Assim o Mexerica grava HEIC sem as extensões HEIF/HEVC da Microsoft Store.
/// Estrutura: ftyp + meta (hdlr, pitm, iloc, iinf, iprp com hvcC, ispe e colr) + mdat.
static class Heif
{
    public static void Wrap(string mp4, string dst, int width, int height)
    {
        var src = File.ReadAllBytes(mp4);
        var moov = TopLevel(src, "moov") ?? throw new Exception("MP4 sem cabeçalho");
        var hvcC = FindBox(moov, "hvcC") ?? throw new Exception("o codificador não gerou a configuração HEVC");
        var sample = FirstSample(src, moov);

        var ftyp = Box("ftyp", Ascii("heic"), U32(0), Ascii("mif1"), Ascii("heic"));
        var hdlr = Full("hdlr", 0, 0, U32(0), Ascii("pict"), U32(0), U32(0), U32(0), new byte[] { 0 });
        var pitm = Full("pitm", 0, 0, U16(1));
        var infe = Full("infe", 2, 0, U16(1), U16(0), Ascii("hvc1"), new byte[] { 0 });
        var iinf = Full("iinf", 0, 0, U16(1), infe);
        var ispe = Full("ispe", 0, 0, U32((uint)width), U32((uint)height));
        // sRGB (primárias BT.709, transferência sRGB), matriz BT.601 em faixa limitada: o que o ffmpeg gerou
        var colr = Box("colr", Ascii("nclx"), U16(1), U16(13), U16(6), new byte[] { 0x00 });
        var ipco = Box("ipco", hvcC, ispe, colr);
        // item 1 → propriedades 1 (hvcC, essencial), 2 (ispe), 3 (colr)
        var ipma = Full("ipma", 0, 0, U32(1), U16(1), new byte[] { 3, 0x81, 0x02, 0x03 });
        var iprp = Box("iprp", ipco, ipma);

        // O iloc aponta para os dados dentro do mdat: o tamanho do meta não depende do valor do deslocamento.
        byte[] Iloc(uint offset) => Full("iloc", 0, 0, new byte[] { 0x44, 0x00 }, U16(1), U16(1), U16(0), U16(1), U32(offset), U32((uint)sample.Length));
        var meta = Full("meta", 0, 0, hdlr, pitm, Iloc(0), iinf, iprp);
        uint dataOffset = (uint)(ftyp.Length + meta.Length + 8);
        meta = Full("meta", 0, 0, hdlr, pitm, Iloc(dataOffset), iinf, iprp);

        using var f = File.Create(dst);
        f.Write(ftyp);
        f.Write(meta);
        f.Write(U32((uint)(8 + sample.Length)));
        f.Write(Ascii("mdat"));
        f.Write(sample);
    }

    // ---------- leitura do MP4 ----------

    /// Caixa inteira (com cabeçalho) do primeiro tipo encontrado, em qualquer profundidade.
    static byte[]? FindBox(byte[] d, string type)
    {
        var t = Encoding.ASCII.GetBytes(type);
        for (int i = 4; i + 4 <= d.Length; i++)
        {
            if (d[i] != t[0] || d[i + 1] != t[1] || d[i + 2] != t[2] || d[i + 3] != t[3]) continue;
            uint size = BE32(d, i - 4);
            if (size >= 8 && i - 4 + size <= d.Length) return d.AsSpan(i - 4, (int)size).ToArray();
        }
        return null;
    }

    /// Caixa de primeiro nível (moov, mdat...), andando de caixa em caixa.
    static byte[]? TopLevel(byte[] d, string type)
    {
        long i = 0;
        while (i + 8 <= d.Length)
        {
            long size = BE32(d, (int)i);
            string t = Encoding.ASCII.GetString(d, (int)i + 4, 4);
            if (size == 1 && i + 16 <= d.Length) size = (long)BE32(d, (int)i + 8) << 32 | BE32(d, (int)i + 12);
            else if (size == 0) size = d.Length - i;
            if (size < 8 || i + size > d.Length) return null;
            if (t == type) return d.AsSpan((int)i, (int)size).ToArray();
            i += size;
        }
        return null;
    }

    /// O único quadro do vídeo, pelo tamanho (stsz) e posição (stco/co64).
    static byte[] FirstSample(byte[] d, byte[] moov)
    {
        var stsz = FindBox(moov, "stsz") ?? throw new Exception("MP4 sem tabela de tamanhos");
        uint size = BE32(stsz, 12);
        if (size == 0) size = BE32(stsz, 20);
        long offset;
        var stco = FindBox(moov, "stco");
        if (stco != null) offset = BE32(stco, 16);
        else
        {
            var co64 = FindBox(moov, "co64") ?? throw new Exception("MP4 sem tabela de posições");
            offset = (long)BE32(co64, 16) << 32 | BE32(co64, 20);
        }
        if (offset + size > d.Length || size == 0) throw new Exception("MP4 inconsistente");
        return d.AsSpan((int)offset, (int)size).ToArray();
    }

    static uint BE32(byte[] d, int i) => (uint)(d[i] << 24 | d[i + 1] << 16 | d[i + 2] << 8 | d[i + 3]);

    // ---------- escrita ----------

    static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);
    static byte[] U16(ushort v) => new[] { (byte)(v >> 8), (byte)v };
    static byte[] U16(int v) => U16((ushort)v);
    static byte[] U32(uint v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

    static byte[] Box(string type, params byte[][] parts)
    {
        int len = 8 + parts.Sum(p => p.Length);
        var b = new List<byte>(len);
        b.AddRange(U32((uint)len)); b.AddRange(Ascii(type));
        foreach (var p in parts) b.AddRange(p);
        return b.ToArray();
    }

    static byte[] Full(string type, byte version, uint flags, params byte[][] parts) =>
        Box(type, new[] { new byte[] { version, (byte)(flags >> 16), (byte)(flags >> 8), (byte)flags } }.Concat(parts).ToArray());
}
