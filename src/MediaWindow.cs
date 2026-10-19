using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mexerica;

enum MediaMode { Trim, Split, Bleep, Frame }

/// Tocador com linha do tempo para escolher trechos: cortar, dividir, bipe e quadro.
sealed class MediaWindow : Window
{
    readonly string src;
    readonly MediaMode mode;
    readonly MediaElement me = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, ScrubbingEnabled = true, Stretch = Stretch.Uniform };
    readonly Slider timeline = new() { Minimum = 0, Margin = new Thickness(0, 8, 0, 0), IsMoveToPointEnabled = true };
    readonly TextBlock clock = new() { Foreground = Ui.Dim, Margin = new Thickness(0, 4, 0, 0) };
    readonly TextBlock status = new() { Foreground = Ui.Dim, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
    readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromMilliseconds(80) };
    readonly P values = new();
    readonly bool video;
    double duration;
    bool playing, syncing;
    double stopAt = -1;
    string? proxyDir;
    P? result;

    public static P? Ask(string src, MediaMode mode)
    {
        var w = new MediaWindow(src, mode);
        return w.ShowDialog() == true ? w.result : null;
    }

    MediaWindow(string src, MediaMode mode)
    {
        this.src = src; this.mode = mode;
        video = Ff.HasVideo(src);
        duration = Ff.Duration(src);
        var head = mode switch { MediaMode.Trim => "Cortar trecho", MediaMode.Split => "Dividir em partes", MediaMode.Bleep => "Bipe por cima", _ => "Quadro do vídeo" };
        Title = "Mexerica: " + head.ToLowerInvariant();
        Width = 760; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);

        var st = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        st.Children.Add(new TextBlock { Text = head, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Ui.Orange });
        st.Children.Add(new TextBlock { Text = Path.GetFileName(src), Foreground = Ui.Dim, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 2, 0, 10) });

        var screen = new Grid { Height = video ? 380 : 70, Background = Brushes.Black };
        screen.Children.Add(me);
        screen.Children.Add(status);
        if (!video) status.Text = "♪  " + Path.GetFileName(src);
        st.Children.Add(screen);

        timeline.Maximum = Math.Max(0.1, duration);
        timeline.SmallChange = 0.1; timeline.LargeChange = 5;
        timeline.ValueChanged += (_, e) => { if (!syncing) Seek(e.NewValue); UpdateClock(); };
        st.Children.Add(timeline);
        st.Children.Add(clock);

        var transport = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        Button TB(string text, string tip, Action a) { var b = Ui.Button(text, false); b.Margin = new Thickness(0, 0, 6, 0); b.MinWidth = 44; b.ToolTip = tip; b.Click += (_, _) => a(); transport.Children.Add(b); return b; }
        Button play = null!;
        play = TB("▶", "Tocar ou pausar (Espaço)", Toggle);
        TB("−5 s", "Voltar 5 segundos", () => Nudge(-5));
        TB("−1 s", "Voltar 1 segundo", () => Nudge(-1));
        if (video) { TB("‹ quadro", "Quadro anterior", () => Nudge(-1 / 30.0)); TB("quadro ›", "Próximo quadro", () => Nudge(1 / 30.0)); }
        TB("+1 s", "Avançar 1 segundo", () => Nudge(1));
        TB("+5 s", "Avançar 5 segundos", () => Nudge(5));
        st.Children.Add(transport);

        st.Children.Add(new Border { Height = 1, Background = Ui.Line, Margin = new Thickness(0, 14, 0, 8) });
        st.Children.Add(ModePanel());

        var ok = Ui.Button(mode == MediaMode.Frame ? "Salvar quadro" : "Fazer", true);
        var cancel = Ui.Button("Cancelar", false);
        ok.Click += (_, _) => Finish();
        cancel.Click += (_, _) => DialogResult = false;
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        bar.Children.Add(cancel); bar.Children.Add(ok);
        st.Children.Add(bar);
        Content = st;

        tick.Tick += (_, _) =>
        {
            if (!playing) return;
            var pos = me.Position.TotalSeconds;
            if (stopAt >= 0 && pos >= stopAt) { Pause(); stopAt = -1; }
            syncing = true; timeline.Value = Math.Min(timeline.Maximum, pos); syncing = false;
            UpdateClock();
        };
        me.MediaOpened += (_, _) =>
        {
            status.Text = video ? "" : status.Text;
            if (duration <= 0 && me.NaturalDuration.HasTimeSpan) { duration = me.NaturalDuration.TimeSpan.TotalSeconds; timeline.Maximum = duration; }
            me.Play(); me.Pause(); Seek(timeline.Value);
        };
        me.MediaEnded += (_, _) => Pause();
        me.MediaFailed += async (_, e) => await UseProxy(e.ErrorException?.Message);
        PreviewKeyDown += (_, e) =>
        {
            if (e.OriginalSource is TextBox) return;
            if (e.Key == Key.Space) { Toggle(); e.Handled = true; }
            else if (e.Key == Key.Left) { Nudge(Keyboard.Modifiers == ModifierKeys.Shift ? -5 : -1); e.Handled = true; }
            else if (e.Key == Key.Right) { Nudge(Keyboard.Modifiers == ModifierKeys.Shift ? 5 : 1); e.Handled = true; }
        };
        Closed += (_, _) => { tick.Stop(); me.Stop(); me.Close(); if (proxyDir != null) Paths.TryDelete(proxyDir); };
        Loaded += (_, _) => { me.Source = new Uri(src); tick.Start(); UpdateClock(); };
        void Toggle() { if (playing) Pause(); else Play(); play.Content = playing ? "⏸" : "▶"; }
        tick.Tick += (_, _) => play.Content = playing ? "⏸" : "▶";
    }

    /// O Windows não toca alguns formatos (mkv sem codec, opus...): gera uma prévia leve com o ffmpeg.
    async Task UseProxy(string? why)
    {
        if (proxyDir != null) { status.Text = "Sem prévia para esse arquivo; use os campos de tempo."; return; }
        Log.Write($"prévia {src}: {why}");
        proxyDir = Paths.Temp();
        status.Text = "Preparando a prévia…";
        var file = Path.Combine(proxyDir, video ? "previa.mp4" : "previa.m4a");
        try
        {
            var a = video
                ? new[] { "-i", src, "-vf", "scale=-2:'min(480,ih)',format=yuv420p", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "30", "-c:a", "aac", "-b:a", "96k", file }
                : new[] { "-i", src, "-vn", "-c:a", "aac", "-b:a", "96k", file };
            await Task.Run(() => Ff.RunOut(a));
            me.Source = new Uri(file);
        }
        catch (Exception ex) { status.Text = "Sem prévia para esse arquivo; use os campos de tempo."; Log.Write("prévia: " + ex.Message); }
    }

    void Play() { me.Play(); playing = true; }
    void Pause() { me.Pause(); playing = false; }
    void Seek(double s) { me.Position = TimeSpan.FromSeconds(Math.Clamp(s, 0, Math.Max(0, duration))); }
    void Nudge(double d) { var v = Math.Clamp(timeline.Value + d, 0, timeline.Maximum); timeline.Value = v; Seek(v); }
    double Now => timeline.Value;
    void UpdateClock() => clock.Text = $"{Ff.Clock(Now)} de {Ff.Clock(duration)}";

    static string T(double s) => Ff.Clock(s);

    FrameworkElement ModePanel()
    {
        var st = new StackPanel();
        switch (mode)
        {
            case MediaMode.Trim:
            {
                var a = Ui.Text("00:00", 110); var b = Ui.Text(T(duration), 110);
                values["modo"] = "preciso";
                st.Children.Add(Ui.Row("Começo do trecho", Pair(a, "Usar posição atual", () => a.Text = T(Now))));
                st.Children.Add(Ui.Row("Fim do trecho", Pair(b, "Usar posição atual", () => b.Text = T(Now))));
                st.Children.Add(Ui.Row("Corte", Ui.Combo(new[] { ("preciso", "Preciso (recodifica)"), ("rapido", "Rápido (sem perda, corta no quadro-chave)") }, "preciso", v => values["modo"] = v)));
                var listen = Ui.Button("Ver o trecho", false); listen.HorizontalAlignment = HorizontalAlignment.Left; listen.Margin = new Thickness(0, 6, 0, 0);
                listen.Click += (_, _) => { var s = Ff.ParseTime(a.Text); var e = Ff.ParseTime(b.Text); if (double.IsNaN(s) || double.IsNaN(e)) return; timeline.Value = s; Seek(s); stopAt = e; Play(); };
                st.Children.Add(listen);
                finish = () =>
                {
                    double s = Ff.ParseTime(a.Text), e = Ff.ParseTime(b.Text);
                    if (double.IsNaN(s) || double.IsNaN(e)) return "Escreva os tempos assim: 1:30 ou 90.";
                    if (e <= s) return "O fim tem que vir depois do começo.";
                    if (duration > 0 && s >= duration) return "O começo passa do fim do arquivo.";
                    values["inicio"] = Inv(s); values["fim"] = Inv(Math.Min(e, duration > 0 ? duration : e));
                    return null;
                };
                break;
            }
            case MediaMode.Split:
            {
                values["modo"] = "pontos";
                var pts = Ui.Text("", 420); pts.TextWrapping = TextWrapping.Wrap; pts.ToolTip = "Tempos separados por ponto e vírgula. Exemplo: 1:30; 3:00";
                var parts = Ui.Text("2", 70); var each = Ui.Text("60", 70);
                var rowPts = Ui.Row("Cortar em", Pair(pts, "Cortar aqui", () => pts.Text = (pts.Text.Trim().TrimEnd(';') + (pts.Text.Trim().Length > 0 ? "; " : "") + T(Now))));
                var rowParts = Ui.Row("Número de partes iguais", parts);
                var rowEach = Ui.Row("Uma parte a cada (segundos)", each);
                void Show() { rowPts.Visibility = values["modo"] == "pontos" ? Visibility.Visible : Visibility.Collapsed; rowParts.Visibility = values["modo"] == "partes" ? Visibility.Visible : Visibility.Collapsed; rowEach.Visibility = values["modo"] == "cada" ? Visibility.Visible : Visibility.Collapsed; }
                st.Children.Add(Ui.Row("Como", Ui.Combo(new[] { ("pontos", "Nos pontos que eu marcar"), ("partes", "Em partes iguais"), ("cada", "A cada tantos segundos") }, "pontos", v => { values["modo"] = v; Show(); })));
                st.Children.Add(rowPts); st.Children.Add(rowParts); st.Children.Add(rowEach);
                Show();
                finish = () =>
                {
                    switch (values["modo"])
                    {
                        case "pontos":
                            var list = Ff.ParseTimes(pts.Text).Where(t => t > 0 && (duration <= 0 || t < duration)).ToList();
                            if (list.Count == 0) return "Marque pelo menos um ponto de corte (botão Cortar aqui).";
                            values["pontos"] = string.Join(";", list.Select(Inv)); break;
                        case "partes":
                            if (!int.TryParse(parts.Text, out var n) || n < 2 || n > 500) return "Número de partes: de 2 a 500.";
                            values["partes"] = n.ToString(); break;
                        default:
                            var sec = Ff.ParseTime(each.Text);
                            if (double.IsNaN(sec) || sec < 1) return "Escreva quantos segundos cada parte deve ter.";
                            values["cada"] = Inv(sec); break;
                    }
                    return null;
                };
                break;
            }
            case MediaMode.Bleep:
            {
                var list = Ui.Text("", 420); list.AcceptsReturn = true; list.Height = 90; list.TextWrapping = TextWrapping.Wrap; list.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                list.ToolTip = "Um trecho por linha, assim: 0:12-0:14";
                double? pending = null;
                var mark = Ui.Button("Marcar começo", false); mark.Margin = new Thickness(0, 6, 6, 0);
                mark.Click += (_, _) =>
                {
                    if (pending == null) { pending = Now; mark.Content = "Marcar fim"; }
                    else
                    {
                        var (s, e) = (Math.Min(pending.Value, Now), Math.Max(pending.Value, Now));
                        if (e - s < 0.1) e = s + 0.5;
                        list.Text = (list.Text.TrimEnd() + (list.Text.Trim().Length > 0 ? "\r\n" : "") + $"{T(s)}-{T(e)}");
                        pending = null; mark.Content = "Marcar começo";
                    }
                };
                values["freq"] = "1000";
                st.Children.Add(new TextBlock { Text = "Trechos para cobrir (toque, pare no ponto e marque começo e fim):", Margin = new Thickness(0, 0, 0, 4) });
                st.Children.Add(list);
                var rowB = new StackPanel { Orientation = Orientation.Horizontal }; rowB.Children.Add(mark);
                st.Children.Add(rowB);
                st.Children.Add(Ui.Row("Som", Ui.Combo(new[] { ("1000", "Bipe clássico (1000 Hz)"), ("440", "Bipe grave (440 Hz)"), ("0", "Só silenciar") }, "1000", v => values["freq"] = v)));
                finish = () =>
                {
                    var r = Ff.ParseRanges(list.Text);
                    if (r.Count == 0) return "Marque pelo menos um trecho (exemplo: 0:12-0:14).";
                    values["trechos"] = string.Join(";", r.Select(x => $"{Inv(x.a)}-{Inv(x.b)}"));
                    return null;
                };
                break;
            }
            case MediaMode.Frame:
            {
                values["formato"] = "png";
                st.Children.Add(new TextBlock { Text = "Pare no quadro que você quer e clique em Salvar quadro.", Foreground = Ui.Dim, Margin = new Thickness(0, 0, 0, 6) });
                st.Children.Add(Ui.Row("Formato", Ui.Combo(new[] { ("png", "PNG (sem perda)"), ("jpg", "JPG") }, "png", v => values["formato"] = v)));
                finish = () => { values["tempo"] = Inv(Now); return null; };
                break;
            }
        }
        return st;
    }

    Func<string?> finish = () => null;

    static string Inv(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

    static FrameworkElement Pair(TextBox tb, string button, Action a)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var b = Ui.Button(button, false); b.Click += (_, _) => a();
        sp.Children.Add(tb); sp.Children.Add(b);
        return sp;
    }

    void Finish()
    {
        var err = finish();
        if (err != null) { MessageBox.Show(this, err, "Mexerica", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        result = values; DialogResult = true;
    }
}
