using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SarahBeauty_Desktop.Views;

internal static class Form
{
    internal static StackPanel Panel() => new() { Spacing = 12, MaxWidth = 1100, HorizontalAlignment = HorizontalAlignment.Stretch };
    internal static TextBlock Title(string text) => new() { Text = text, FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    internal static TextBlock Text(string text = "") => new() { Text = text, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    internal static TextBox Input(string label, string value = "") => new() { Header = label, Text = value, MinWidth = 200 };
    internal static PasswordBox Secret(string label) => new() { Header = label, PasswordRevealMode = PasswordRevealMode.Hidden };

    internal static ComboBox Select(string label, string[] choices, string selected) =>
        new() { Header = label, ItemsSource = choices, SelectedItem = selected, MinWidth = 200 };

    internal static Button Action(string label, TextBlock status, Func<Task> action)
    {
        var button = new Button { Content = label, MinHeight = 44 };
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { status.Text = ""; await action(); }
            catch (Exception error) { status.Text = error.Message; }
            finally { button.IsEnabled = true; }
        };
        return button;
    }

    internal static void Add(this Panel panel, params UIElement[] controls)
    {
        foreach (var control in controls) panel.Children.Add(control);
    }
}
