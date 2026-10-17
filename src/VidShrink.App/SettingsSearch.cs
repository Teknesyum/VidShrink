using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace VidShrink.App;

internal static class SettingsSearch
{
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    internal static bool Matches(string? text, string query)
        => query.Length == 0
           || !string.IsNullOrEmpty(text) && CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, query, Options) >= 0;

    internal static IEnumerable<string> Texts(Control root)
    {
        foreach (var node in root.GetSelfAndLogicalDescendants())
        {
            switch (node)
            {
                case TextBlock { Text: { Length: > 0 } text }:
                    yield return text;
                    break;
                case ContentControl { Content: string { Length: > 0 } content }:
                    yield return content;
                    break;
            }

            if (node is Control control && ToolTip.GetTip(control) is string { Length: > 0 } tip) yield return tip;
        }
    }

    internal static bool CardMatches(Control card, string query)
        => query.Length == 0 || Texts(card).Any(text => Matches(text, query));
}
