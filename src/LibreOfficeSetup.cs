using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Mexerica;

/// Baixa e instala o LibreOffice quando falta (documentos do Word, Excel e PowerPoint dependem dele).
/// A pessoa só confirma e aceita o pedido de permissão do Windows; o resto é automático.
static class LibreOfficeSetup
{
    const string Index = "https://download.documentfoundation.org/libreoffice/stable/";
    static bool busy;

    public static async void Offer()
    {
        if (Office.Ok) { App.Toast.Show("O LibreOffice já está instalado", "Pode converter seus documentos."); return; }
        if (busy) { App.Toast.Show("Já estou instalando o LibreOffice", "Aguarde terminar."); return; }
        var r = MessageBox.Show(
            "Para converter documentos do Word, Excel e PowerPoint, o Mexerica usa o LibreOffice, um programa gratuito e confiável.\n\n" +
            "Quer que eu baixe e instale agora? São cerca de 360 MB. O Windows vai pedir sua permissão para instalar.",
            "Mexerica: falta o LibreOffice", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
        if (r != MessageBoxResult.Yes) return;
        busy = true;
        var job = new Job();
        App.Toast.Track(job, "Baixando o LibreOffice…", "Pode continuar usando o computador.");
        var dir = Path.Combine(Path.GetTempPath(), "mexerica-libreoffice");
        try
        {
            Directory.CreateDirectory(dir);
            var msi = await Task.Run(() => Download(dir, job));
            App.Toast.Track(job, "Instalando o LibreOffice…", "Aceite o pedido de permissão do Windows. Leva alguns minutos.");
            int code = await Task.Run(() => RunMsi(msi));
            if (code is 1602 or 1223) { App.Toast.Show("Instalação cancelada", "O LibreOffice não foi instalado. Dá para tentar de novo quando quiser."); return; }
            if (code is not (0 or 3010 or 1641)) throw new Exception($"O instalador do LibreOffice terminou com o código {code}.");
            Office.Refresh();
            if (!Office.Ok) throw new Exception("O LibreOffice foi instalado, mas não o encontrei. Reinicie o computador e tente de novo.");
            ShellMenu.Apply();
            App.Toast.Show("LibreOffice instalado", "Agora o Mexerica converte documentos do Word, Excel e PowerPoint.");
        }
        catch (OperationCanceledException) { App.Toast.Show("Cancelado", "O LibreOffice não foi instalado."); }
        catch (HttpRequestException) { App.Toast.Show("Sem conexão com a internet", "Não deu para baixar o LibreOffice. Tente de novo quando estiver conectado.", error: true); }
        catch (Exception ex) { Log.Write("libreoffice: " + ex); App.Toast.Show("Não deu para instalar o LibreOffice", ex.Message, error: true); }
        finally { busy = false; Paths.TryDelete(dir); }
    }

    /// Versão estável mais nova, direto do site oficial (o endereço redireciona para o espelho mais próximo).
    public static async Task<string> LatestUrl(HttpClient http)
    {
        var html = await http.GetStringAsync(Index);
        var v = Regex.Matches(html, @"href=""(\d+\.\d+\.\d+)/""").Select(m => Version.Parse(m.Groups[1].Value)).Max()
                ?? throw new Exception("Não achei a versão do LibreOffice no site oficial.");
        var s = v.ToString(3);
        return $"{Index}{s}/win/x86_64/LibreOffice_{s}_Win_x86-64.msi";
    }

    static async Task<string> Download(string dir, Job job)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mexerica");
        var url = await LatestUrl(http);
        var dst = Path.Combine(dir, Path.GetFileName(url));
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, job.Cts.Token);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? 0, got = 0;
        await using (var src = await resp.Content.ReadAsStreamAsync(job.Cts.Token))
        await using (var f = File.Create(dst))
        {
            var buf = new byte[1 << 20];
            int n;
            while ((n = await src.ReadAsync(buf, job.Cts.Token)) > 0)
            {
                await f.WriteAsync(buf.AsMemory(0, n), job.Cts.Token);
                got += n;
                if (total > 0) job.OnProgress?.Invoke((double)got / total);
            }
        }
        // Só instala se for mesmo da The Document Foundation (assinatura digital do arquivo).
        try
        {
            var cert = X509Certificate.CreateFromSignedFile(dst);
            if (!cert.Subject.Contains("Document Foundation", StringComparison.OrdinalIgnoreCase))
                throw new Exception("O arquivo baixado não é do LibreOffice oficial. Nada foi instalado.");
        }
        catch (System.Security.Cryptography.CryptographicException) { throw new Exception("O arquivo baixado não tem a assinatura do LibreOffice. Nada foi instalado."); }
        return dst;
    }

    /// Instala sem perguntas além da permissão do Windows, sem trocar o programa que abre .docx/.xlsx
    /// (quem tem o Microsoft Office continua abrindo pelo Office) e sem atalho na área de trabalho.
    static int RunMsi(string msi)
    {
        var psi = new ProcessStartInfo("msiexec.exe")
        {
            UseShellExecute = true, Verb = "runas",
            Arguments = $"/i \"{msi}\" /passive /norestart ALLUSERS=1 REGISTER_NO_MSO_TYPES=1 CREATEDESKTOPLINK=0 QUICKSTART=0 ISCHECKFORPRODUCTUPDATES=0 RebootYesNo=No",
        };
        try
        {
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return 1223; } // não aceitou a permissão
    }
}
