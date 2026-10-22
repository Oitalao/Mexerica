using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mexerica;

/// Miniatura que fica por cima de tudo, para arrastar o arquivo depois para onde quiser.
sealed class FloatWindow : Window
{
    readonly string[] files;
    Point downAt;
    bool pressed;

    public FloatWindow(string[] files)
    {
        Tr.Window(this);
        this.files = files;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight;
        ShowActivated = false;

        var img = new Image { Width = 96, Height = 96, Stretch = Stretch.Uniform, Source = Thumb(files[0]) };
        var name = new TextBlock
        {
            Text = files.Length == 1 ? Path.GetFileName(files[0].TrimEnd('\\')) : Tr.T($"{files.Length} itens"),
            Foreground = Brushes.White, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 130, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0),
        };
        Tr.Raw(name); // Nome de arquivo não é texto de interface.
        var close = new Button
        {
            Content = "✕", Width = 22, Height = 22, Padding = new Thickness(0), FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromArgb(200, 60, 60, 66)), Foreground = Brushes.White, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = "Fechar",
        };
        close.Click += (_, _) => Close();
        var stack = new StackPanel { Margin = new Thickness(14, 14, 14, 10) };
        stack.Children.Add(img); stack.Children.Add(name);
        var grid = new Grid();
        grid.Children.Add(stack); grid.Children.Add(close);
        Content = new Border
        {
            CornerRadius = new CornerRadius(14), Background = new SolidColorBrush(Color.FromArgb(235, 28, 28, 32)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 138, 31)), BorderThickness = new Thickness(1.5),
            Child = grid, ToolTip = "Arraste para levar o arquivo · botão direito move a janela",
        };

        img.MouseLeftButtonDown += (_, e) => { pressed = true; downAt = e.GetPosition(this); };
        img.MouseMove += (_, e) =>
        {
            if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - downAt.X) < 4 && Math.Abs(p.Y - downAt.Y) < 4) return;
            pressed = false;
            var data = new DataObject(DataFormats.FileDrop, files.Where(f => File.Exists(f) || Directory.Exists(f)).ToArray());
            var r = DragDrop.DoDragDrop(img, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
            if (r.HasFlag(DragDropEffects.Move)) Close();
        };
        img.MouseLeftButtonUp += (_, _) => pressed = false;
        MouseRightButtonDown += (_, _) => DragMove();

        SourceInitialized += (_, _) => Native.MakeToolWindow(new WindowInteropHelper(this).Handle, false);
        Loaded += (_, _) =>
        {
            var wa = SystemParameters.WorkArea;
            int n = Application.Current.Windows.OfType<FloatWindow>().Count() - 1;
            Left = wa.Right - ActualWidth - 24 - n * 18;
            Top = wa.Top + 80 + n * 18;
        };
    }

    static ImageSource? Thumb(string path)
    {
        try
        {
            if (Catalog.KindOf(path) == Kind.Image)
            {
                var bi = new BitmapImage();
                bi.BeginInit(); bi.UriSource = new Uri(path); bi.DecodePixelWidth = 192; bi.CacheOption = BitmapCacheOption.OnLoad; bi.EndInit();
                return bi;
            }
        }
        catch { }
        return Shell.Icon(path);
    }
}
