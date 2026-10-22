using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mexerica;

static class Tr
{
    public static readonly (string Code, string Name)[] Languages =
    {
        ("pt-BR", "Português (Brasil)"), ("auto", "Automático (idioma do Windows)"),
        ("en", "English"), ("es", "Español"), ("fr", "Français"),
        ("de", "Deutsch"), ("it", "Italiano"), ("ja", "日本語"),
        ("ko", "한국어"), ("zh-CN", "简体中文"), ("ru", "Русский"),
        ("pl", "Polski"), ("hi", "हिन्दी"), ("ar", "العربية")
    };

    static readonly Dictionary<string, Dictionary<string, string>> cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, List<(Regex Pattern, string Translation)>> patterns = new(StringComparer.OrdinalIgnoreCase);
    static readonly DependencyProperty watched = DependencyProperty.RegisterAttached("Watched", typeof(bool), typeof(Tr));
    static readonly DependencyProperty raw = DependencyProperty.RegisterAttached("Raw", typeof(bool), typeof(Tr));

    public static void Raw(FrameworkElement element) => element.SetValue(raw, true);

    static Tr()
    {
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is not FrameworkElement element || (bool)element.GetValue(watched) || (bool)element.GetValue(raw)) return;
                element.SetValue(watched, true);
                Watch(element, FrameworkElement.ToolTipProperty);
                if (element is Window) Watch(element, System.Windows.Window.TitleProperty);
                if (element is TextBlock) Watch(element, TextBlock.TextProperty);
                if (element is ContentControl) Watch(element, ContentControl.ContentProperty);
                if (element is HeaderedContentControl) Watch(element, HeaderedContentControl.HeaderProperty);
            }));
    }

    public static string Code
    {
        get
        {
            var requested = Config.Now.Language;
            if (requested == "auto")
            {
                var windows = CultureInfo.CurrentUICulture.Name;
                requested = windows.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR"
                    : windows.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN"
                    : windows.Split('-')[0];
                if (!Languages.Any(l => l.Code.Equals(requested, StringComparison.OrdinalIgnoreCase))) requested = "pt-BR";
            }
            return Languages.FirstOrDefault(l => l.Code.Equals(requested, StringComparison.OrdinalIgnoreCase)).Code ?? "pt-BR";
        }
    }

    public static bool Rtl => Code == "ar";

    public static MessageBoxResult Box(Window owner, string message, string caption,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None) =>
        MessageBox.Show(owner, T(message), T(caption), buttons, image, defaultResult,
            Rtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);

    public static MessageBoxResult Box(string message, string caption,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None,
        MessageBoxResult defaultResult = MessageBoxResult.None) =>
        MessageBox.Show(T(message), T(caption), buttons, image, defaultResult,
            Rtl ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None);

    public static void Window(Window window)
    {
        window.FlowDirection = Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        window.FontFamily = new System.Windows.Media.FontFamily(Code switch
        {
            "ja" => "Yu Gothic UI", "ko" => "Malgun Gothic", "zh-CN" => "Microsoft YaHei UI",
            "hi" => "Nirmala UI", "ar" => "Segoe UI", _ => "Segoe UI"
        });
    }

    static void Watch(FrameworkElement element, DependencyProperty property)
    {
        void Update(object? _, EventArgs __)
        {
            if (element.GetValue(property) is string source)
            {
                var translated = T(source);
                if (translated != source) element.SetValue(property, translated);
            }
        }
        DependencyPropertyDescriptor.FromProperty(property, element.GetType())?.AddValueChanged(element, Update);
        Update(null, EventArgs.Empty);
    }

    public static void TranslateTree(DependencyObject node)
    {
        if (node is FrameworkElement element && !(bool)element.GetValue(raw))
        {
            foreach (var property in new[] { FrameworkElement.ToolTipProperty, TextBlock.TextProperty,
                ContentControl.ContentProperty, HeaderedContentControl.HeaderProperty, System.Windows.Window.TitleProperty })
                if (element.GetValue(property) is string source && T(source) is string result && result != source)
                    element.SetValue(property, result);
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            TranslateTree(VisualTreeHelper.GetChild(node, i));
    }

    public static string T(string? source)
    {
        if (string.IsNullOrEmpty(source) || Code == "pt-BR" ||
            source is "Shift" or "Ctrl" or "Alt" or "Tab" or "Esc" or "Caps Lock" ||
            Regex.IsMatch(source, @"^(?:[A-Z0-9][A-Z0-9.+-]*|\d+ (?:DPI|px))$")) return source ?? "";
        var language = Code;
        Dictionary<string, string> words;
        List<(Regex Pattern, string Translation)> templates;
        lock (cache)
        {
            if (!cache.TryGetValue(language, out words!))
            {
                var file = Path.Combine(AppContext.BaseDirectory, "Idiomas", language + ".json");
                try { words = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? new(); }
                catch (Exception ex) { Log.Write("idioma " + language + ": " + ex.Message); words = new(); }
                cache[language] = words;
            }
            if (!patterns.TryGetValue(language, out templates!))
            {
                templates = new();
                foreach (var pair in words.Where(p => p.Key.Contains("{0}")))
                {
                    var parts = Regex.Split(pair.Key, @"\{\d+\}");
                    var pattern = "^" + string.Join("(.*?)", parts.Select(Regex.Escape)) + "$";
                    templates.Add((new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline), pair.Value));
                }
                patterns[language] = templates;
            }
        }
        if (words.TryGetValue(source, out var direct)) return direct;
        foreach (var (pattern, translation) in templates)
        {
            var match = pattern.Match(source);
            if (!match.Success) continue;
            try { return string.Format(CultureInfo.CurrentCulture, translation,
                match.Groups.Cast<Group>().Skip(1).Select(g => (object)g.Value).ToArray()); }
            catch (FormatException) { return source; }
        }
        return source;
    }
}
