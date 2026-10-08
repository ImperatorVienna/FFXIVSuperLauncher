using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
namespace XIVLauncher.Linux;

// Measure the displayed translation, not a fixed width chosen for an earlier label.
// Non-string content (such as official news cards) keeps its own presentation.
internal static class ButtonPresentation
{
    internal static Style CreateStyle() => new(x => x.OfType<Button>())
    {
        Setters =
        {
            new Setter(Layoutable.MinWidthProperty, 0d),
            new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Left),
            new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 6)),
            new Setter(ContentControl.ContentTemplateProperty, new FuncDataTemplate<object>((content, _) => content is Control control ? control :
                new TextBlock { Text = Localization.Display(content?.ToString() ?? ""), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }))
        }
    };
}
