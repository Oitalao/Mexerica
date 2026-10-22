// Adaptado do Filee (MIT, © 2026 Myeonggu Kim e colaboradores). Origem: Filee.Engines/Data/DataConverter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Filee.Engines.Data;
using Filee.Engines.Subtitles;

namespace Mexerica;

sealed class DadosModule : Module
{
    static readonly string[] Trees = ["json", "jsonl", "ndjson", "yaml", "yml", "toml", "xml"];
    public override string Name => "Dados";
    public override IEnumerable<string> Inputs => [.. Trees, "csv", "tsv", "vcf"];
    // VCF só para planilhas de contatos (com coluna de nome, telefone ou e-mail).
    public override IEnumerable<Target> TargetsFor(string inExt, string path)
    {
        var all = TargetsFor(inExt);
        if (inExt is not ("csv" or "tsv")) return Tabular(inExt, path) ? all : all.Where(t => t.Ext is not ("csv" or "tsv"));
        string head = "";
        try { using var r = new StreamReader(path); head = (r.ReadLine() ?? "").ToLowerInvariant(); } catch { }
        bool contacts = VCard.LooksLikeContacts(head.Split(inExt == "tsv" ? '\t' : head.Contains(';') && !head.Contains(',') ? ';' : ','));
        return contacts ? all : all.Where(t => t.Ext != "vcf");
    }

    // JSON, YAML, TOML e XML só viram CSV/TSV quando os dados formam uma tabela. Arquivos grandes: deixa tentar.
    static bool Tabular(string from, string path)
    {
        try
        {
            if (new FileInfo(path).Length > 4 * 1024 * 1024) return true;
            var tree = from == "xml" ? XmlData.Read(path) : ReadText(SubtitleEncoding.Decode(File.ReadAllBytes(path)), from);
            return CsvData.Table(tree) != null;
        }
        catch { return false; }
    }

    public override IEnumerable<Target> TargetsFor(string inExt)
    {
        if (inExt == "vcf") return [new Target("csv", "dados", "CSV")];
        if (inExt is "csv" or "tsv")
            return [.. Trees.Where(e => e is not ("ndjson" or "yml")).Select(e => new Target(e, "dados", e.ToUpperInvariant())), new Target("vcf", "contatos", "VCF")];
        return [.. Trees.Where(e => e is not ("ndjson" or "yml") && e != inExt).Select(e => new Target(e, "dados", e.ToUpperInvariant())),
            new Target("csv", "tabela", "CSV"), new Target("tsv", "tabela", "TSV")];
    }

    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(() =>
    {
        try
        {
        Job.Check();
        var from = Modules.Ext(src);
        if (from == "vcf")
        {
            var contacts = VCard.Read(File.ReadAllBytes(src));
            if (contacts.Count == 0) throw new Exception("Nenhum contato foi encontrado no vCard.");
            var csv = Paths.Out(src, "csv");
            CsvFormat.WriteRows(VCard.ToRows(contacts), csv);
            Job.Progress(1);
            return csv;
        }
        if (outExt == "vcf")
        {
            var contacts = VCard.FromRows(CsvData.ReadRows(src));
            Job.Check();
            var vcf = Paths.Out(src, "vcf");
            File.WriteAllText(vcf, VCard.Write(contacts), new UTF8Encoding(false));
            Job.Progress(1);
            return vcf;
        }
        var tree = from switch
        {
            "csv" or "tsv" => CsvData.Read(src),
            "xml" => XmlData.Read(src),
            _ => ReadText(SubtitleEncoding.Decode(File.ReadAllBytes(src)), from),
        };
        Job.Check();
        Job.Progress(0.6);
        var rows = outExt is "csv" or "tsv" ? CsvData.Table(tree) : null;
        var output = rows == null ? outExt switch
        {
            "json" => JsonData.Write(tree),
            "jsonl" => JsonData.WriteLines(tree),
            "yaml" => YamlData.Write(tree),
            "toml" => TomlData.Write(tree),
            "xml" => XmlData.Write(tree),
            _ => throw new Exception("Destino de dados não suportado."),
        } : null;
        var dst = Paths.Out(src, outExt);
        if (rows != null) CsvFormat.WriteRows(rows, dst, outExt == "tsv" ? '\t' : ',');
        else File.WriteAllText(dst, output, new UTF8Encoding(false));
        Job.Progress(1);
        return dst;
        }
        catch (InvalidDataException ex) when (ex.Message.Contains("CSV holds a table"))
        { throw new Exception("Esses dados não formam uma tabela para CSV ou TSV."); }
        catch (InvalidDataException)
        { throw new Exception("Arquivo de dados inválido ou incompatível com o destino."); }
    }, Job.Token);

    static object? ReadText(string text, string format) => format switch
    {
        "json" => JsonData.Read(text),
        "jsonl" or "ndjson" => JsonData.ReadLines(text),
        "yaml" or "yml" => YamlData.Read(text),
        "toml" => TomlData.Read(text),
        _ => throw new Exception("Formato de dados não suportado."),
    };
}
