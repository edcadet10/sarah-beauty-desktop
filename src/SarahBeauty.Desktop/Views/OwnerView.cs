using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal static class OwnerView
{
    internal static UIElement Build(OwnerAccess access,OwnerPins pins, Action<OwnerSession> signedIn) =>
        access.NeedsSetup ? Setup(access, signedIn) : new OwnerSignInView(access,pins,signedIn).Build();

    private static UIElement Setup(OwnerAccess access, Action<OwnerSession> signedIn)
    {
        var panel = Form.Panel();
        var name = Form.Input("Business display name");
        var first = Form.Input("First owner name");
        var second = Form.Input("Second owner name");
        var firstPhrase = Form.Secret("First owner's passphrase");
        var secondPhrase = Form.Secret("Second owner's passphrase");
        var status = Form.Text();
        panel.Add(Form.Title("Set up your TEST business"),
            Form.Text("Choose a separate passphrase of at least 15 characters for each owner. Use words you can type easily. These records stay on this Windows account."),
            name, first, firstPhrase, second, secondPhrase, status,
            Form.PrimaryAction("Create business", status, async () =>
            {
                var values = (name.Text, first.Text, firstPhrase.Password, second.Text, secondPhrase.Password);
                var session = await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(() => access.Setup(values.Item1, values.Item2, values.Item3, values.Item4, values.Item5),token));
                firstPhrase.Password = secondPhrase.Password = "";
                signedIn(session);
            }));
        panel.Unloaded+=(_,_)=>{firstPhrase.Password="";secondPhrase.Password="";};return panel;
    }

}
