using System;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Mexerica;

/// Sessão do modelo de IA que remove fundo: isnet-general-use, do projeto DIS (Xuebin Qin, Apache 2.0) —
/// o mesmo modelo "geral" que o rembg usa. Fica em tools\onnx\isnet-general-use.onnx; roda local, sem internet.
static class Bg
{
    public const int Size = 1024;

    public static readonly string? ModelPath = Paths.FindTool(Path.Combine("onnx", "isnet-general-use.onnx"));
    public static bool Ok => ModelPath != null;

    static InferenceSession? session;
    static readonly object gate = new();

    /// Tensor de entrada 1x3x1024x1024 (já normalizado). Devolve a máscara 1024x1024 com valores 0..1.
    public static float[] Mask(DenseTensor<float> input)
    {
        InferenceSession s;
        lock (gate) s = session ??= new InferenceSession(ModelPath!);
        using var results = s.Run(new[] { NamedOnnxValue.CreateFromTensor(s.InputMetadata.Keys.First(), input) });
        var mask = results.First().AsEnumerable<float>().Take(Size * Size).ToArray();
        float mi = mask.Min(), ma = mask.Max();
        if (ma <= mi) return mask;
        for (int i = 0; i < mask.Length; i++) mask[i] = (mask[i] - mi) / (ma - mi);
        return mask;
    }
}
