using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mexerica;

/// Aviso no canto da tela, com barra de progresso e cancelar durante o trabalho.
/// Clicar no aviso pronto abre a pasta com o resultado selecionado.
sealed class Toast : Window
{
    readonly TextBlock head, body, cancel;
    readonly ProgressBar bar;
    readonly Border card;
    readonly DispatcherTimer timer = new();
    string? target;
    Job? job;

    public Toast()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Width = 360; SizeToContent = SizeToContent.Height;
        head = new TextBlock { Foreground = Brushes.White, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        body = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255)), FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 64, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0) };
        bar = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 9, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0xA8, 0xFF)), Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed };
        cancel = new TextBlock { Text = "Cancelar", Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0xA8, 0xFF)), FontSize = 12, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Cursor = Cursors.Hand, Visibility = Visibility.Collapsed };
        cancel.MouseLeftButtonUp += (_, e) => { e.Handled = true; job?.Cts.Cancel(); head.Text = "Cancelando…"; };
        var st = new StackPanel(); st.Children.Add(head); st.Children.Add(body); st.Children.Add(bar); st.Children.Add(cancel);
        card = new Border
        {
            CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 12, 16, 13), Margin = new Thickness(8),
            Background = new SolidColorBrush(Color.FromArgb(242, 28, 28, 32)), BorderThickness = new Thickness(0, 0, 0, 3), Child = st,
            Cursor = Cursors.Hand,
        };
        Content = card;
        card.MouseLeftButtonUp += (_, _) => { if (job != null) return; OpenTarget(); Hide(); };
        timer.Tick += (_, _) => { timer.Stop(); Hide(); };
        SourceInitialized += (_, _) => Native.MakeToolWindow(new WindowInteropHelper(this).Handle, true);
        SizeChanged += (_, _) => Place();
    }

    public void Show(string title, string text, string? path = null, bool sticky = false, bool error = false)
    {
        head.Text = title; body.Text = text; target = path;
        card.BorderBrush = new SolidColorBrush(error ? Color.FromRgb(0xF0, 0x4A, 0x4A) : sticky ? Color.FromRgb(0x3A, 0xA8, 0xFF) : Color.FromRgb(0xFF, 0x8A, 0x1F));
        if (!sticky) { job = null; bar.Visibility = cancel.Visibility = Visibility.Collapsed; }
        timer.Stop();
        if (!sticky) { timer.Interval = TimeSpan.FromSeconds(error ? 9 : 5); timer.Start(); }
        if (!IsVisible) Show();
        Place();
    }

    /// Mostra um trabalho em andamento com barra e cancelar.
    public void Track(Job j, string title, string text)
    {
        job = j;
        Show(title, text, sticky: true);
        bar.Value = 0; bar.IsIndeterminate = true; bar.Visibility = Visibility.Visible; cancel.Visibility = Visibility.Visible;
        j.OnProgress = f => Dispatcher.BeginInvoke(() => { if (job != j) return; bar.IsIndeterminate = false; bar.Value = f; });
    }

    public void Status(Job j, string title, string text) { if (job == j) { head.Text = title; body.Text = text; } }

    void Place()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth - 8;
        Top = wa.Bottom - ActualHeight - 8;
    }

    void OpenTarget()
    {
        if (target == null) return;
        Reveal(target);
    }

    public static void Reveal(string target)
    {
        try
        {
            if (File.Exists(target) || Directory.Exists(target))
                Process.Start("explorer.exe", $"/select,\"{target}\"");
        }
        catch { }
    }
}
