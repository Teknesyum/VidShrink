using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public static class Probe
{
    public static void Main()
    {
        var t = typeof(MeasureFilterGraph);
        System.Console.WriteLine("tur      : " + t.FullName);
        System.Console.WriteLine("derleme  : " + t.Assembly.GetName().Name);
        System.Console.WriteLine("grafik   : " + MeasureFilterGraph.Build(1920, 1080, "libvmaf"));
        var u = typeof(VidShrink.Ffmpeg.MeasureFilterGraph);
        System.Console.WriteLine("urun tur : " + u.FullName + " @ " + u.Assembly.GetName().Name);
    }
}
