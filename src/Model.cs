using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mexerica;

enum Mode { Convert, Tools, Actions }

enum Kind { Image, Svg, Audio, Video, Pdf, Doc, Markdown, Text, Html, Epub, Slides, Sheet, Csv, Subtitle, Archive, Folder, Other }

/// Parâmetros escolhidos numa janela de ajuste (ou passados pelo modo de teste).
sealed class P : Dictionary<string, string>
{
    public P() : base(StringComparer.OrdinalIgnoreCase) { }
    public static readonly P None = new();
    public string S(string k, string def = "") => TryGetValue(k, out var v) && v != null ? v : def;
    public double D(string k, double def = 0) =>
        TryGetValue(k, out var v) && double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : def;
    public int I(string k, int def = 0) => (int)Math.Round(D(k, def));
    public bool B(string k) => S(k) is "1" or "true" or "sim";
}

sealed class Op
{
    public required string Label { get; init; }
    public string Sub { get; init; } = "";
    /// Abre uma janela de ajuste antes de rodar (na thread da tela). Devolve null se a pessoa cancelar.
    public Func<string[], P?>? Ask { get; init; }
    /// Processa um arquivo por vez; devolve o caminho gerado.
    public Func<string, P, Task<string?>>? One { get; init; }
    /// Processa a seleção inteira de uma vez.
    public Func<string[], P, Task<string?>>? All { get; init; }
    /// Roda na hora, na thread da tela (área de transferência, diálogos).
    public Action<string[]>? Ui { get; init; }
    /// Em vez do aviso "Pronto", mostra o resultado (texto) numa janela própria.
    public Action<string>? Show { get; init; }
}

/// Trabalho em andamento: progresso e cancelamento chegam aos motores sem passar parâmetro.
sealed class Job
{
    static readonly AsyncLocal<Job?> current = new();
    public static Job? Current { get => current.Value; set => current.Value = value; }
    public static CancellationToken Token => Current?.Cts.Token ?? CancellationToken.None;

    public readonly CancellationTokenSource Cts = new();
    public Action<double>? OnProgress;

    // Cada arquivo que roda em paralelo tem sua vaga; a barra mostra a média.
    // A vaga e o trecho do passo atual seguem o fluxo assíncrono de cada arquivo.
    sealed record Scope(int Slot, double P0, double Ps);
    static readonly AsyncLocal<Scope?> scope = new();
    double[] slots = new double[1];
    long lastReport;

    public void Slots(int n) { lock (this) slots = new double[Math.Max(1, n)]; }

    /// Daqui em diante, neste fluxo, o progresso conta para o arquivo i.
    public static void Slot(int i) => scope.Value = new Scope(i, 0, 1);

    /// Avisa o progresso do passo atual (0 a 1).
    public static void Progress(double f)
    {
        var j = Current;
        if (j == null) return;
        var s = scope.Value ?? new Scope(0, 0, 1);
        double avg;
        lock (j)
        {
            if (s.Slot >= j.slots.Length) return;
            j.slots[s.Slot] = Math.Clamp(s.P0 + s.Ps * Math.Clamp(f, 0, 1), 0, 1);
            avg = j.slots.Average();
            // Muitos arquivos juntos geram avisos demais para a tela: no máximo ~15 por segundo.
            var now = Environment.TickCount64;
            if (f > 0 && f < 1 && now - j.lastReport < 66) return;
            j.lastReport = now;
        }
        j.OnProgress?.Invoke(avg);
    }

    /// Divide a parte da barra deste arquivo entre vários passos.
    public static void Part(int i, int n)
    {
        if (Current == null || n <= 0) return;
        scope.Value = new Scope(scope.Value?.Slot ?? 0, (double)i / n, 1.0 / n);
        Progress(0);
    }

    public static void Check() => Token.ThrowIfCancellationRequested();
}
