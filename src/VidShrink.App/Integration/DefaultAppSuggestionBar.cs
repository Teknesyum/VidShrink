using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using VidShrink.App.Localization;

namespace VidShrink.App.Integration;

/// <summary>
/// "VidShrink varsayılan değil" önerisi. Bir cümle ve iki düğme taşır: biri Windows'un
/// varsayılan uygulamalar sayfasını açar, diğeri öneriyi kalıcı olarak kapatır.
///
/// <para>Renk ve ölçü <c>Themes/Theme.axaml</c> belirteçlerinden dinamik kaynak olarak
/// alınır; şeridin kendi sayısı yoktur. Metin dil dosyasından gelir ve dil değişince
/// kendiliğinden yenilenir.</para>
/// </summary>
internal sealed class DefaultAppSuggestionBar : UserControl
{
    private const string MessageKey = "settings.default-app.suggestion";
    private const string OpenKey = "settings.default-app.open";
    private const string DismissKey = "settings.default-app.dismiss";

    private readonly string? _settingsPath;

    internal DefaultAppSuggestionBar(string? settingsPath = null)
    {
        _settingsPath = settingsPath;

        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        message.Bind(TextBlock.TextProperty, Text(MessageKey));
        message.Bind(TextBlock.ForegroundProperty, Token("TextBody"));
        message.Bind(TextBlock.FontSizeProperty, Token("FontSizeSm"));

        var open = Action(OpenKey);
        open.Click += (_, _) =>
        {
            if (OperatingSystem.IsWindows()) DefaultApp.OpenSettings();
        };

        var dismiss = Action(DismissKey);
        dismiss.Click += (_, _) =>
        {
            DefaultAppSuggestion.Dismiss(_settingsPath);
            IsVisible = false;
        };

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Bind(StackPanel.SpacingProperty, Token("SpaceSm"));
        actions.Children.Add(open);
        actions.Children.Add(dismiss);

        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(actions, Dock.Right);
        row.Children.Add(actions);
        row.Children.Add(message);

        var frame = new Border { Child = row };
        frame.Bind(Border.BackgroundProperty, Token("PanelSurface"));
        frame.Bind(Border.BorderBrushProperty, Token("NeonBlueBorder"));
        frame.Bind(Border.BorderThicknessProperty, Token("BorderThin"));
        frame.Bind(Border.CornerRadiusProperty, Token("RadiusPanel"));
        frame.Bind(Border.PaddingProperty, Token("PanelPadding"));

        Content = frame;
        Bind(MarginProperty, Token("NoticeMargin"));
    }

    /// <summary>
    /// Şerit görünür mü. Kararı <see cref="DefaultAppSuggestion.ShouldShow"/> verir;
    /// buradaki iş yalnız üç girdiyi toplamaktır.
    /// </summary>
    internal static bool Wanted(string executablePath, IReadOnlyList<string> extensions, string? settingsPath = null)
        => DefaultAppSuggestion.ShouldShow(
            OperatingSystem.IsWindows(),
            OperatingSystem.IsWindows() && DefaultApp.IsDefault(executablePath, extensions),
            DefaultAppSuggestion.Dismissed(settingsPath));

    private static Button Action(string key)
    {
        var button = new Button { VerticalAlignment = VerticalAlignment.Center };
        button.Bind(ContentControl.ContentProperty, Text(key));
        button.Bind(TemplatedControl.PaddingProperty, Token("ButtonPaddingSm"));
        button.Bind(TemplatedControl.FontSizeProperty, Token("FontSizeSm"));
        return button;
    }

    private static IBinding Text(string key)
        => new Binding(nameof(LocalizedText.Value))
        {
            Source = LocalizedText.For(key),
            Mode = BindingMode.OneWay
        };

    private static DynamicResourceExtension Token(string key) => new(key);
}
