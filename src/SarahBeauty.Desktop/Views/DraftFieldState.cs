using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;

namespace SarahBeauty_Desktop.Views;

internal sealed class DraftFieldState(UIElement root)
{
    private readonly string _initial=Fingerprint(root);
    internal bool HasChanges=>Fingerprint(root)!=_initial;
    private static string Fingerprint(UIElement element)=>JsonSerializer.Serialize(Values(element).ToArray());

    private static IEnumerable<string[]> Values(UIElement element)
    {
        switch(element)
        {
            case TextBox text when !text.IsReadOnly:
                if(text.Header is not string label||!label.StartsWith("Search ",StringComparison.Ordinal))yield return ["Text",text.Text];
                yield break;
            case ComboBox choice:
                yield return ["Choice",choice.SelectedId(),choice.SelectedItem as string??""];yield break;
            case CheckBox check:
                yield return ["Check",check.IsChecked?.ToString()??""];yield break;
        }
        foreach(var child in Children(element))foreach(var value in Values(child))yield return value;
    }

    private static IEnumerable<UIElement> Children(UIElement element)=>element switch
    {
        Panel panel=>panel.Children,
        ContentControl {Content:UIElement child}=>[child],
        Border {Child:{} child}=>[child],
        _=>[]
    };
}
