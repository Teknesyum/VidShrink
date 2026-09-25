using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using VidShrink.App.Localization;

namespace VidShrink.Shot;

/// <summary>
/// Kaydedicinin bölge düzenleyicisindeki araç panelini kendi ölçüsünde çizer. Pencere gösterilmez;
/// panel bassiz olarak ölçülüp yerleştirilir. Düzenleyicide <c>SetPhase</c> varsa her evre ayrı
/// kareye çizilir, yoksa yalnız boşta hali.
/// </summary>
internal static class BolgePaneli
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static int Run(string outDir, string label)
    {
        Directory.CreateDirectory(outDir);
        var written = new List<string>();
        Host.Run(() =>
        {
            Strings.Use("tr");
            var type = typeof(VidShrink.App.App).Assembly.GetType("VidShrink.App.Recorder.RecorderRegionEditor")
                ?? throw new TypeLoadException("RecorderRegionEditor");
            var setPhase = type.GetMethod("SetPhase", Any);
            var phases = setPhase is null
                ? new object?[] { null }
                : Enum.GetValues(setPhase.GetParameters()[0].ParameterType).Cast<object?>().ToArray();

            foreach (var phase in phases)
            {
                var editor = (Window)Activator.CreateInstance(type, nonPublic: true)!;
                try
                {
                    if (type.GetField("TxtSize", Any)?.GetValue(editor) is TextBlock size) size.Text = "1280×720";
                    if (phase is not null) setPhase!.Invoke(editor, new[] { phase });
                    var bar = (Control)(type.GetField("Toolbar", Any)!.GetValue(editor)!);
                    bar.Measure(Size.Infinity);
                    bar.Arrange(new Rect(bar.DesiredSize));
                    bar.UpdateLayout();
                    var w = (int)Math.Ceiling(bar.Bounds.Width);
                    var h = (int)Math.Ceiling(bar.Bounds.Height);
                    var name = $"bolge-paneli-{label}-{phase?.ToString()?.ToLowerInvariant() ?? "idle"}.png";
                    var path = Path.Combine(outDir, name);
                    using var bitmap = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
                    bitmap.Render(bar);
                    bitmap.Save(path, PngBitmapEncoderOptions.Default);
                    Console.WriteLine($"{name}	{w}x{h}");
                    written.Add(path);
                }
                finally
                {
                    editor.Close();
                }
            }
        });

        return written.Count == 0 ? 1 : 0;
    }
}
