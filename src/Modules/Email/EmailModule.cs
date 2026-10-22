// Adaptado de Filee.Engines/Email/EmlConverter.cs. Adaptado do Filee (MIT, © 2026 Myeonggu Kim e colaboradores)
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using MimeKit;

namespace Mexerica;

sealed class EmailModule : Module
{
    public override string Name => "E-mail";
    public override IEnumerable<string> Inputs => new[] { "eml", "msg" };
    public override IEnumerable<Target> TargetsFor(string inExt)
    {
        foreach (var ext in new[] { "html", "txt", "zip" }) yield return new(ext, ext == "zip" ? "anexos" : "mensagem");
        if (inExt == "msg") yield return new("eml", "mensagem");
        if (Office.Ok) yield return new("pdf", "mensagem");
    }

    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(async () =>
    {
        Job.Check();
        MimeMessage mail;
        try { mail = Modules.Ext(src) == "msg" ? OutlookMessage.Load(src) : MimeMessage.Load(src); }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new Exception("Não foi possível ler a mensagem: " + ex.Message, ex); }
        Job.Progress(.3);
        if (outExt == "pdf")
        {
            if (!Office.Ok) throw new Exception("Instale o LibreOffice para gerar PDF do e-mail");
            var temp = Paths.Temp();
            try
            {
                var html = Path.Combine(temp, "mensagem.html");
                File.WriteAllText(html, Html(mail), new UTF8Encoding(false));
                var pdf = await TextDocs.FromHtmlFile(html, "pdf");
                var dst = Paths.Out(src, "pdf");
                File.Move(pdf, dst);
                Job.Progress(1);
                return dst;
            }
            finally { Paths.TryDelete(temp); }
        }
        var output = Paths.Out(src, outExt);
        if (outExt == "eml") mail.WriteTo(output);
        else if (outExt == "html") File.WriteAllText(output, Html(mail), new UTF8Encoding(false));
        else if (outExt == "txt") File.WriteAllText(output, Plain(mail), new UTF8Encoding(true));
        else if (outExt == "zip") Attachments(mail, output);
        else throw new Exception("Destino de e-mail não suportado");
        Job.Progress(1);
        return output;
    });

    static IEnumerable<(string Name, string Value)> Headers(MimeMessage m)
    {
        if (m.From.Count > 0) yield return ("De", m.From.ToString());
        if (m.To.Count > 0) yield return ("Para", m.To.ToString());
        if (m.Cc.Count > 0) yield return ("Cc", m.Cc.ToString());
        if (m.Headers.Contains(HeaderId.Date)) yield return ("Data", m.Date.ToString("dd/MM/yyyy HH:mm zzz"));
        if (!string.IsNullOrWhiteSpace(m.Subject)) yield return ("Assunto", m.Subject);
        var attachments = m.BodyParts.OfType<MimePart>().Where(p => p.IsAttachment || p.FileName != null && p.ContentId == null)
            .Select(p => p.FileName ?? "anexo").ToList();
        if (attachments.Count > 0) yield return ("Anexos", string.Join(", ", attachments));
    }

    static string Plain(MimeMessage m) => string.Join("\r\n", Headers(m).Select(h => h.Name + ": " + h.Value)) +
        "\r\n\r\n" + (m.TextBody ?? TextDocs.HtmlToText(m.HtmlBody ?? ""));

    static string Html(MimeMessage m)
    {
        var body = m.HtmlBody ?? "<pre>" + WebUtility.HtmlEncode(m.TextBody ?? "") + "</pre>";
        foreach (var part in m.BodyParts.OfType<MimePart>().Where(x => x.ContentId != null && x.Content != null))
        {
            using var data = new MemoryStream();
            part.Content!.DecodeTo(data);
            if (part.ContentType.MediaType.Equals("image", StringComparison.OrdinalIgnoreCase) && part.ContentType.MediaSubtype is not "svg+xml")
                body = Regex.Replace(body, "cid:" + Regex.Escape(part.ContentId!.Trim('<', '>')), "data:" + part.ContentType.MimeType + ";base64," + System.Convert.ToBase64String(data.ToArray()), RegexOptions.IgnoreCase);
        }
        var doc = new HtmlParser().ParseDocument(body);
        foreach (var element in doc.QuerySelectorAll("script,noscript,iframe,object,embed,form,input,button,meta,link,base,svg,math,video,audio,source,style").ToArray()) element.Remove();
        foreach (var element in doc.QuerySelectorAll("*").ToArray())
            foreach (var attr in element.Attributes.ToArray())
                if (!(attr.Name is "class" or "title" or "alt" or "width" or "height" or "colspan" or "rowspan") &&
                    !(attr.Name == "src" && element.LocalName == "img" && Regex.IsMatch(attr.Value, "^data:image/(png|jpeg|gif|webp|bmp);base64,", RegexOptions.IgnoreCase)) &&
                    !(attr.Name == "href" && element.LocalName == "a" && (attr.Value.StartsWith('#') || Uri.TryCreate(attr.Value, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" or "mailto"))) element.RemoveAttribute(attr.Name);
        var rows = string.Join("", Headers(m).Select(h => "<tr><th>" + h.Name + "</th><td>" + WebUtility.HtmlEncode(h.Value) + "</td></tr>"));
        return "<!doctype html><html><head><meta charset=\"utf-8\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'\"><title>" + WebUtility.HtmlEncode(m.Subject) + "</title></head><body><table>" + rows + "</table><hr>" + doc.Body?.InnerHtml + "</body></html>";
    }

    static void Attachments(MimeMessage m, string output)
    {
        var parts = m.BodyParts.Where(x => x is MimePart p && (p.IsAttachment || p.FileName != null && p.ContentId == null) || x is MessagePart).ToList();
        if (parts.Count == 0) throw new Exception("Este e-mail não tem anexos");
        using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            Job.Check();
            var raw = part.ContentDisposition?.FileName ?? (part as MimePart)?.FileName ?? "anexo";
            var name = Path.GetFileName(raw.Replace('\\', '/'));
            name = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim(' ', '.');
            if (name.Length == 0) name = "anexo";
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            for (var n = 2; !used.Add(name); n++) name = stem + " (" + n + ")" + ext;
            using var stream = zip.CreateEntry(name).Open();
            if (part is MimePart file) file.Content?.DecodeTo(stream);
            else if (part is MessagePart message) message.Message?.WriteTo(stream);
        }
    }
}
