using System.Windows;
using System.Windows.Controls;

namespace NezamMonitorPro.App.Converters;

/// <summary>
/// Picks a DataTemplate only for string button content (labels), and returns
/// null for everything else so the ContentPresenter shows the content
/// itself. This matters because a ContentTemplate set on a Button wins over
/// the content even when the content is a UIElement: with a plain
/// ContentTemplate, an icon TextBlock or a StackPanel (icon + label) was
/// rendered as the literal text "System.Windows.Controls.TextBlock" /
/// "System.Windows.Controls.StackPanel" instead of the icon.
/// </summary>
public sealed class StringContentTemplateSelector : DataTemplateSelector
{
    public DataTemplate? StringTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
        => item is string ? StringTemplate : null;
}
