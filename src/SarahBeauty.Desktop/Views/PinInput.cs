using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace SarahBeauty_Desktop.Views;

internal static class PinInput
{
    internal static PasswordBox Create(string label)
    {
        var input=Form.Secret(label);var scope=new InputScope();
        scope.Names.Add(new InputScopeName{NameValue=InputScopeNameValue.NumericPin});input.InputScope=scope;
        return input;
    }
}
