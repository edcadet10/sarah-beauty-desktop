using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class WorkspaceView(BusinessStore store, OwnerSession session, Action locked)
{
    private readonly StackPanel _content = Form.Panel();

    internal UIElement Build()
    {
        var panel = Form.Panel();
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var status = Form.Text();
        nav.Add(Link("Dashboard", Dashboard, status), Link("Customers", () => new CustomerView(store, session).Build(), status),
            Link("Bookings", () => new BookingView(store, session).Build(), status),
            Link("Money", () => new MoneyView(store, session).Build(), status),
            Form.Action("Lock / switch owner", status, () => { locked(); return Task.CompletedTask; }));
        panel.Add(Form.Title(store.BusinessName(session)), Form.Text($"TEST • Signed in as {session.OwnerName}"),
            nav, status, _content);
        _content.Add(Dashboard());
        return panel;
    }

    private Button Link(string title, Func<UIElement> page, TextBlock status) => Form.Action(title, status, () =>
    {
        UIElement view = page();
        _content.Children.Clear();
        _content.Add(view);
        return Task.CompletedTask;
    });

    private UIElement Dashboard()
    {
        var panel = Form.Panel();
        panel.Add(Form.Title("Dashboard"), Form.Text($"Active customers: {new CustomerBook(store).Search(session).Count}"),
            Form.Text("Cloud export: Never completed. Recovery upload: Never completed."));
        return panel;
    }
}
