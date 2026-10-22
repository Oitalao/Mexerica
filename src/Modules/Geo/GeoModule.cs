using System.IO;
using System.Threading.Tasks;
using Filee.Engines.Geo;

namespace Mexerica;

sealed class GeoModule : Module
{
    static readonly string[] Formats = ["geojson", "kml", "kmz", "gpx", "shp", "csv", "json"];
    public override string Name => "Mapas";
    public override IEnumerable<string> Inputs => Formats;
    public override IEnumerable<Target> TargetsFor(string inExt) => Formats
        .Where(e => e != inExt)
        .Select(e => new Target(e, e == "csv" ? "coordenadas" : "mapa", e.ToUpperInvariant()));
    public override IEnumerable<Target> TargetsFor(string inExt, string path)
    {
        if (inExt == "json")
        {
            try
            {
                using var reader = new StreamReader(path);
                var head = new char[2048];
                var count = reader.Read(head, 0, head.Length);
                if (!new string(head, 0, count).Contains("\"type\"", StringComparison.OrdinalIgnoreCase)) return [];
            }
            catch { return []; }
        }
        if (inExt == "csv")
        {
            try
            {
                using var reader = new StreamReader(path);
                var header = reader.ReadLine() ?? "";
                if (!new[] { "wkt", "geom", "shape", "longitude", "lon", "lng", "long" }.Any(h => header.Contains(h, StringComparison.OrdinalIgnoreCase))) return [];
            }
            catch { return []; }
        }
        return TargetsFor(inExt);
    }
    public override Task<string?> Convert(string src, string outExt, P p) => Task.Run<string?>(() =>
    {
        try
        {
        Job.Check();
        var dataset = Modules.Ext(src) switch
        {
            "geojson" or "json" => GeoJsonFormat.Read(src, Job.Token),
            "kml" => KmlFormat.Read(src, false, Job.Token),
            "kmz" => KmlFormat.Read(src, true, Job.Token),
            "gpx" => GpxFormat.Read(src, Job.Token),
            "shp" => ShapefileFormat.Read(src, Job.Token),
            "csv" => GeoCsvFormat.Read(src, Job.Token),
            _ => throw new Exception("Formato de mapa não reconhecido."),
        };
        if (dataset.Features.Count == 0) throw new Exception("O mapa não contém feições.");
        for (var i = 0; i < dataset.Features.Count; i++)
        {
            Job.Check();
            if (dataset.Features[i].Geometry is not { } geometry) continue;
            var converted = dataset.Crs.IsIdentity ? geometry : geometry.Map(dataset.Crs.ToLonLat);
            if (converted.Coordinates().Any(c => !c.IsFinite || c.Y is < -90.000001 or > 90.000001 || c.X is < -180.000001 or > 180.000001))
                throw new Exception("As coordenadas não cabem em longitude/latitude; confira o sistema de referência (.prj).");
            dataset.Features[i] = new GeoFeature(converted, dataset.Features[i].Properties);
        }
        dataset.Crs = GeoCrs.Wgs84;
        Job.Progress(0.6);
        Job.Check();
        string dst;
        var written = new List<string>();
        try
        {
        if (outExt == "shp")
        {
            var groups = ShapefileFormat.Plan(dataset);
            if (groups.Count == 0) throw new Exception("O mapa não contém geometrias para Shapefile.");
            string? first = null;
            foreach (var group in groups)
            {
                Job.Check();
                var suffix = groups.Count == 1 ? null : group.Kind.ToString().ToLowerInvariant();
                var shp = Paths.Out(src, "shp", suffix);
                while (new[] { "shx", "dbf", "prj", "cpg" }.Any(e => File.Exists(Path.ChangeExtension(shp, e)))) shp = Paths.Out(src, "shp", suffix);
                written.Add(shp);
                written.AddRange(new[] { "shx", "dbf", "prj", "cpg" }.Select(e => Path.ChangeExtension(shp, e)));
                ShapefileFormat.Write(group, shp, Job.Token);
                first ??= shp;
            }
            dst = first!;
        }
        else
        {
            dst = Paths.Out(src, outExt);
            written.Add(dst);
            switch (outExt)
            {
                case "geojson" or "json": GeoJsonFormat.Write(dataset, dst, Job.Token); break;
                case "kml" or "kmz": KmlFormat.Write(dataset, dst, outExt == "kmz", Job.Token); break;
                case "gpx": GpxFormat.Write(dataset, dst, Job.Token); break;
                case "csv": GeoCsvFormat.Write(dataset, dst, Job.Token); break;
                default: throw new Exception("Destino de mapa não reconhecido.");
            }
        }
        }
        catch
        {
            foreach (var file in written) Paths.TryDelete(file);
            throw;
        }
        Job.Progress(1);
        return dst;
        }
        catch (InvalidDataException) { throw new Exception("O arquivo de mapa está inválido ou incompleto."); }
    }, Job.Token);
}
