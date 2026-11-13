using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal static class OwnerView
{
    internal static UIElement Build(OwnerAccess access,OwnerPins pins, Action<OwnerSession> signedIn) =>
        new OwnerSignInView(access,pins,signedIn).Build();

}
