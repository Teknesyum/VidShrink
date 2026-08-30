using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;

namespace VidShrink.Tests;

public sealed class VisibleTextTests
{
    private static readonly HashSet<string> EmptyTextExemptions =
    [
        "TxtDurationRange",
        "TxtEstimateRange",
        "TxtEstimateNote",
        "TxtResult",
        "TxtShareStatus",
        "TxtConvertValidation",
        "TxtConvertResult",
        "TxtShareCeiling",
        "TxtShareDeleteNote",
        "TxtSystemStatus"
    ];

    private static readonly HashSet<string> EmptyButtonExemptions =
    [
        "BtnMinimize",
        "PART_DecreaseButton",
        "PART_IncreaseButton",
        "PART_PageUpButton",
        "PART_PageDownButton"
    ];

    [Fact]
    public void EveryVisibleTextNodeHasContent()
    {
        var empty = AppHost.Run(() =>
        {
            var window = new MainWindow
            {
                Width = double.NaN,
                Height = double.NaN
            };
            var size = new Size(1560, 1060);
            var result = new List<string>();
            window.Measure(size);
            window.Arrange(new Rect(size));

            for (var index = 0; index < window.Tabs.ItemCount; index++)
            {
                window.Tabs.SelectedIndex = index;
                foreach (var node in window.GetVisualDescendants().OfType<Layoutable>())
                    node.InvalidateMeasure();
                var root = (Layoutable)window.GetVisualChildren().Single();
                root.InvalidateMeasure();
                root.Measure(size);
                root.Arrange(new Rect(size));

                result.AddRange(window.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(block => block.IsEffectivelyVisible)
                    .Where(block => block.TemplatedParent is null)
                    .Where(block => string.IsNullOrWhiteSpace(block.Text))
                    .Where(block => block.Inlines is null || !block.Inlines.OfType<Run>()
                        .Any(run => !string.IsNullOrWhiteSpace(run.Text)))
                    .Where(block => !EmptyTextExemptions.Contains(block.Name ?? string.Empty))
                    .Select(block => $"tab {index}: {block.Name ?? $"<unnamed in {block.GetVisualParent()?.GetType().Name}>"}"));

                result.AddRange(window.GetVisualDescendants()
                    .OfType<Button>()
                    .Where(button => button.IsEffectivelyVisible)
                    .Where(button => button.Content is null || button.Content is string text && string.IsNullOrWhiteSpace(text))
                    .Where(button => !EmptyButtonExemptions.Contains(button.Name ?? string.Empty))
                    .Select(button => $"tab {index}: {button.Name ?? "<unnamed button>"}"));
            }

            result.AddRange(window.Tabs.Items
                .OfType<TabItem>()
                .Where(tab => tab.Header is null || tab.Header is string text && string.IsNullOrWhiteSpace(text))
                .Select((_, index) => $"tab {index}: <empty header>"));

            return result.Distinct().ToList();
        });

        Assert.True(empty.Count == 0, "Empty visible text nodes: " + string.Join(", ", empty));
    }
}
