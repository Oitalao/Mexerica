using System.IO;

namespace Filee.Engines.Geo;

internal sealed class ConversionRefusedException(string code, string? detail = null) : Exception(Describe(code, detail))
{
    static string Describe(string code, string? detail) => code switch
    {
        "error.geo_empty" => "O mapa não contém feições.",
        "error.geo_csv_no_coordinates" => "O CSV precisa de colunas longitude/latitude ou WKT.",
        "error.geo_csv_no_locations" => "Nenhuma linha do CSV contém coordenadas válidas.",
        "error.geo_unsupported_crs" => $"Sistema de coordenadas não suportado: {detail}.",
        "error.geo_unsupported_shape" => $"Geometria Shapefile não suportada: {detail}.",
        "error.geo_too_large" => "O Shapefile excede o limite de tamanho do formato.",
        _ => $"Não foi possível converter o mapa: {detail ?? code}.",
    };
}
