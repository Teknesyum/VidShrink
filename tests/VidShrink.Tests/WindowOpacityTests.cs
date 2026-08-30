using Avalonia;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using System.Runtime.InteropServices;
using VidShrink.App;

namespace VidShrink.Tests;

public sealed class WindowOpacityTests
{
    [Fact]
    public void TheWorkspaceKeepsTheDesktopOutOfEveryPixel()
    {
        var transparent = AppHost.Run(() =>
        {
            const int width = 1600;
            const int height = 1000;
            var window = new MainWindow
            {
                Width = double.NaN,
                Height = double.NaN
            };
            var size = new Size(width, height);
            window.Measure(size);
            window.Arrange(new Rect(size));
            var root = (Layoutable)window.GetVisualChildren().Single();
            root.Measure(size);
            root.Arrange(new Rect(size));

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(root);
            var stride = width * 4;
            var pixels = new byte[stride * height];
            var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), pinned.AddrOfPinnedObject(), pixels.Length, stride);
            }
            finally
            {
                pinned.Free();
            }
            var transparent = Enumerable.Range(0, width * height).Count(index => pixels[(index * 4) + 3] < byte.MaxValue);
            var clear = Enumerable.Range(0, width * height).Count(index => pixels[(index * 4) + 3] == 0);
            return (transparent, clear, root.Bounds);
        });

        Assert.True(transparent.transparent == 0,
            $"Transparent: {transparent.transparent}, clear: {transparent.clear}, bounds: {transparent.Bounds}");
    }
}
