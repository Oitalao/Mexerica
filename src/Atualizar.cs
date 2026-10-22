using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mexerica;

/// Procura versão nova no GitHub, baixa o instalador, confere o SHA-256 e instala sem perguntar nada,
/// mantendo as configurações. Depois de atualizar, mostra o que há de novo.
static class Updater
{
    const string Repo = "Oitalao/Mexerica";
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };
    static DispatcherTimer? timer;
    static Release? offered;

    sealed record Release(Version Version, string Tag, string Notes, string Url, long Size, string? Sha256);

    static Updater()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mexerica-atualizador");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static Version Current => typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0);
    public static string CurrentText => Current.ToString(3);

    /// Liga a verificação automática: um minuto depois de abrir e depois a cada 6 horas.
    public static void Start()
    {
        _ = ShowWhatsNewIfUpdated();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        timer.Tick += async (_, _) =>
        {
            timer.Interval = TimeSpan.FromHours(6);
            if (Config.Now.AutoUpdate) await Check(manual: false);
        };
        timer.Start();
    }

    static async Task<Release?> Latest()
    {
        var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        return Parse(JsonDocument.Parse(json).RootElement);
    }

    static Release? Parse(JsonElement r)
    {
        var tag = r.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return null;
        var asset = r.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("name").GetString()?.EndsWith("-instalador.exe", StringComparison.OrdinalIgnoreCase) == true);
        if (asset.ValueKind == JsonValueKind.Undefined) return null;
        string? sha = asset.TryGetProperty("digest", out var dg) && dg.GetString() is string d && d.StartsWith("sha256:") ? d[7..] : null;
        return new Release(v, tag, r.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "",
            asset.GetProperty("browser_download_url").GetString()!, asset.GetProperty("size").GetInt64(), sha);
    }

    /// Procura agora. Manual: avisa também quando já está atualizado.
    public static async Task Check(bool manual)
    {
        try
        {
            var r = await Latest();
            if (r == null || r.Version <= Current)
            {
                if (manual) App.Toast.Show("Mexerica está atualizado", $"Versão {CurrentText}.");
                return;
            }
            if (!manual && Config.Now.SkippedVersion == r.Tag) return;
            offered = r;
            App.Toast.Show($"Mexerica {r.Version.ToString(3)} disponível", "Clique aqui para atualizar agora (leva um minuto).", () => _ = Install(r), seconds: 20);
            App.OfferUpdate(r.Version.ToString(3));
        }
        catch (Exception ex)
        {
            Log.Write("atualização: " + ex.Message);
            if (manual) App.Toast.Show("Não deu para procurar atualização", "Confira a internet e tente de novo.", error: true);
        }
    }

    public static Task InstallOffered() => offered != null ? Install(offered) : Check(manual: true);

    static async Task Install(Release r)
    {
        var job = new Job();
        App.Toast.Track(job, "Baixando atualização…", $"Mexerica {r.Version.ToString(3)}");
        try
        {
            var dir = Paths.Temp();
            var exe = Path.Combine(dir, $"Mexerica-{r.Version.ToString(3)}-instalador.exe");
            using (var resp = await http.GetAsync(r.Url, HttpCompletionOption.ResponseHeadersRead, job.Cts.Token))
            {
                resp.EnsureSuccessStatusCode();
                await using var src = await resp.Content.ReadAsStreamAsync(job.Cts.Token);
                await using var dst = File.Create(exe);
                var buf = new byte[1 << 16];
                long done = 0; int n;
                while ((n = await src.ReadAsync(buf, job.Cts.Token)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), job.Cts.Token);
                    done += n;
                    job.OnProgress?.Invoke(r.Size > 0 ? (double)done / r.Size : 0);
                }
            }
            if (r.Sha256 != null)
            {
                await using var f = File.OpenRead(exe);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(f));
                if (!hash.Equals(r.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("O instalador baixado não confere com o publicado (SHA-256). Nada foi instalado.");
            }
            // Guarda as novidades para mostrar quando a versão nova abrir.
            var c = Config.Now.Clone(); c.UpdateNotes = r.Notes; c.UpdateNotesVersion = r.Version.ToString(3); c.Save();
            App.Toast.Show("Instalando a atualização…", "O Mexerica volta sozinho em instantes.", sticky: true);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-") { UseShellExecute = true });
        }
        catch (OperationCanceledException) { App.Toast.Show("Atualização cancelada", ""); }
        catch (Exception ex) { Log.Write("atualização: " + ex); App.Toast.Show("Não deu para atualizar", App.Friendly(ex), error: true); }
    }

    /// Primeira vez numa versão nova: mostra o que mudou (guardado antes de atualizar, ou buscado no GitHub).
    static async Task ShowWhatsNewIfUpdated()
    {
        var c = Config.Now;
        var now = CurrentText;
        if (c.LastSeenVersion == now) return;
        bool firstEver = string.IsNullOrEmpty(c.LastSeenVersion);
        var save = c.Clone(); save.LastSeenVersion = now; save.Save();
        if (firstEver) return; // instalação nova: quem cuida é a janela de boas-vindas
        string notes = c.UpdateNotesVersion == now ? c.UpdateNotes : "";
        if (notes.Length == 0)
        {
            try
            {
                var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/tags/v{now}");
                notes = JsonDocument.Parse(json).RootElement.GetProperty("body").GetString() ?? "";
            }
            catch { }
        }
        if (notes.Trim().Length > 0) new WhatsNewWindow(now, notes).Show();
    }
}

/// "O que há de novo": as notas da versão, com títulos, listas e negrito simples.
sealed class WhatsNewWindow : Window
{
    public WhatsNewWindow(string version, string notes)
    {
        Title = $"Novidades do Mexerica {version}";
        Width = 560; Height = 520; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Ui.Dark(this);
        var st = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        st.Children.Add(new TextBlock { Text = $"Mexerica {version}", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Ui.Orange, Margin = new Thickness(0, 0, 0, 10) });
        foreach (var raw in notes.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;
            bool bullet = line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* ");
            bool head = line.StartsWith("#");
            var text = bullet ? line.TrimStart()[2..] : head ? line.TrimStart('#', ' ') : line;
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White, Margin = new Thickness(bullet ? 14 : 0, head ? 10 : 2, 0, 4), FontSize = head ? 15 : 13 };
            if (head) tb.FontWeight = FontWeights.SemiBold;
            if (bullet) tb.Inlines.Add(new Run("•  ") { Foreground = Ui.Orange });
            // **negrito**
            var parts = Regex.Split(text, @"\*\*(.+?)\*\*");
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0) tb.Inlines.Add(new Run(parts[i].Replace("`", "")) { FontWeight = i % 2 == 1 ? FontWeights.SemiBold : FontWeights.Normal });
            st.Children.Add(tb);
        }
        var ok = Ui.Button("Fechar", true);
        ok.HorizontalAlignment = HorizontalAlignment.Right; ok.Margin = new Thickness(0, 14, 0, 0);
        ok.Click += (_, _) => Close();
        var root = new DockPanel();
        DockPanel.SetDock(ok, Dock.Bottom);
        root.Children.Add(ok);
        root.Children.Add(new ScrollViewer { Content = st, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }
}
