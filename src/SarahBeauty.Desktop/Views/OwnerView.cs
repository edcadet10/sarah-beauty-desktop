using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal static class OwnerView
{
    internal static UIElement Build(OwnerAccess access, Action<OwnerSession> signedIn) =>
        access.NeedsSetup ? Setup(access, signedIn) : SignIn(access, signedIn);

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

    private static UIElement SignIn(OwnerAccess access, Action<OwnerSession> signedIn)
    {
        var panel = Form.Panel();
        panel.MaxWidth = 440;
        panel.HorizontalAlignment = HorizontalAlignment.Left;
        var owners = access.Owners();
        var select = new ComboBox { Header = "Owner", ItemsSource = owners.Select(o => o.Name).ToList(), SelectedIndex = 0 };
        var phrase = Form.Secret("Passphrase");
        var status = Form.Text();
        panel.Add(Form.Title("Welcome to Sarah Beauty"), select, phrase, status,
            Form.PrimaryAction("Sign in", status, async () =>
            {
                if (select.SelectedIndex < 0) throw new ArgumentException("Select an owner.");
                var (id, password) = (owners[select.SelectedIndex].Id, phrase.Password);
                phrase.Password = "";
                signedIn(await ((App)Application.Current).Operations.RunAsync(token=>Task.Run(() => access.SignIn(id, password),token)));
            }));
        panel.Unloaded+=(_,_)=>phrase.Password="";return panel;
    }
}
