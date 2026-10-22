using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Mexerica;

/// Arrastos que vêm do navegador (Chrome, Edge, Firefox): uma imagem, um link de arquivo ou um download.
/// O arquivo é guardado na pasta Downloads e daí segue como qualquer outro.
static class WebDrop
{
    static readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromMinutes(3) };

    static WebDrop() => http.DefaultRequestHeaders.UserAgent.ParseAdd("Mexerica/1.1 (Windows)");

    /// O arrasto traz algo que dá para virar arquivo (mas ainda não é um arquivo no disco)?
    public static bool Has(IDataObject d) =>
        d.GetDataPresent("FileGroupDescriptorW") || Url(d) != null;

    /// Conteúdo embutido no arrasto (imagem arrastada de uma página): grava na hora em Downloads.
    public static string? FromContents(IDataObject d)
    {
        try
        {
            if (!d.GetDataPresent("FileGroupDescriptorW") || d.GetData("FileGroupDescriptorW") is not MemoryStream desc) return null;
            var name = FirstName(desc);
            if (string.IsNullOrWhiteSpace(name) || d.GetData("FileContents") is not MemoryStream content || content.Length == 0) return null;
            var dest = Unique(Downloads(), Clean(name));
            File.WriteAllBytes(dest, content.ToArray());
            return dest;
        }
        catch (Exception ex) { Log.Write("arrasto do navegador: " + ex.Message); return null; }
    }

    /// Endereço do arquivo arrastado (link, imagem sem conteúdo embutido, download do Chrome).
    public static string? Url(IDataObject d)
    {
        try
        {
            if (d.GetDataPresent("DownloadURL") && Read(d, "DownloadURL") is string dl)
            {
                // Chrome: "mime:nome:https://..."
                var i = dl.IndexOf(":http", StringComparison.OrdinalIgnoreCase);
                if (i > 0) return dl[(i + 1)..].Trim('\0', ' ');
            }
            foreach (var f in new[] { "UniformResourceLocatorW", "UniformResourceLocator", "text/x-moz-url" })
                if (d.GetDataPresent(f) && Read(d, f) is string u && Web(u.Split('\n')[0])) return u.Split('\n')[0].Trim('\0', ' ', '\r');
            if (d.GetDataPresent(DataFormats.UnicodeText) && d.GetData(DataFormats.UnicodeText) is string t && Web(t.Trim()) && !t.Trim().Contains(' '))
                return t.Trim();
        }
        catch { }
        return null;
    }

    static bool Web(string s) => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    static string? Read(IDataObject d, string fmt) => d.GetData(fmt) switch
    {
        string s => s,
        MemoryStream m => (fmt.EndsWith("W") || fmt == "text/x-moz-url" || fmt == "DownloadURL" ? Encoding.Unicode : Encoding.Default).GetString(m.ToArray()).TrimEnd('\0'),
        _ => null,
    };

    /// Baixa o endereço para Downloads (sem o login do navegador) e devolve o caminho.
    public static async Task<string> Download(string url)
    {
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!resp.IsSuccessStatusCode) throw new Exception($"O site recusou o download ({(int)resp.StatusCode})");
        var name = resp.Content.Headers.ContentDisposition?.FileNameStar ?? resp.Content.Headers.ContentDisposition?.FileName;
        name = name?.Trim('"');
        if (string.IsNullOrWhiteSpace(name)) name = Uri.UnescapeDataString(Path.GetFileName(new Uri(url).AbsolutePath));
        if (string.IsNullOrWhiteSpace(name)) name = "download";
        name = Clean(name);
        if (Path.GetExtension(name).Length == 0) name += ExtFor(resp.Content.Headers.ContentType?.MediaType);
        var dest = Unique(Downloads(), name);
        await using (var fs = File.Create(dest))
            await resp.Content.CopyToAsync(fs);
        return dest;
    }

    static string ExtFor(string? mime) => mime switch
    {
        "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", "image/gif" => ".gif", "image/avif" => ".avif",
        "image/svg+xml" => ".svg", "image/heic" => ".heic", "application/pdf" => ".pdf", "video/mp4" => ".mp4", "video/webm" => ".webm",
        "audio/mpeg" => ".mp3", "audio/ogg" => ".ogg", "text/plain" => ".txt", "text/html" => ".html", "application/zip" => ".zip",
        _ => "",
    };

    // FILEGROUPDESCRIPTORW: contagem (4 bytes) + FILEDESCRIPTORW (592 bytes cada; o nome começa no byte 72).
    static string FirstName(MemoryStream desc)
    {
        var b = desc.ToArray();
        if (b.Length < 4 + 592 || BitConverter.ToInt32(b, 0) < 1) return "";
        return Encoding.Unicode.GetString(b, 4 + 72, 520).Split('\0')[0];
    }

    static string Clean(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Length > 150 ? name[..120] + Path.GetExtension(name) : name;
    }

    static string Downloads()
    {
        var d = Paths.Downloads();
        Directory.CreateDirectory(d);
        return d;
    }

    static string Unique(string dir, string name)
    {
        var p = Path.Combine(dir, name);
        for (int i = 2; File.Exists(p); i++) p = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(name)} {i}{Path.GetExtension(name)}");
        return p;
    }
}
