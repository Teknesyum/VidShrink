using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace VidShrink.Tests;

internal static class VisualVisibility
{
    internal static bool IsShown(this Visual visual)
    {
        for (Visual? node = visual; node is not null and not TopLevel; node = node.GetVisualParent())
            if (!node.IsVisible) return false;
        return true;
    }
}
