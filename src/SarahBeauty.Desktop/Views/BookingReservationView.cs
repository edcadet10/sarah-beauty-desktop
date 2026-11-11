using Microsoft.UI.Xaml;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class BookingView
{
    private UIElement Reservation(Booking booking)
    {
        var panel=Form.Panel();
        var date=Form.Input("Reservation date (YYYY-MM-DD)",DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"));
        var reason=Form.Input("Reservation decision and evidence");
        var phrase=Form.Secret($"Confirm {_owner.OwnerName}'s passphrase");
        var status=Form.Text();
        string requestId=Guid.NewGuid().ToString();
        panel.Add(Form.Text("Confirmation checks the accepted event details and approved trial/deposit allocations. Saving an inquiry or a trial payment alone does not reserve the date."),
            date,reason,phrase,status,Form.Action("Confirm and reserve this booking",status,async()=>
            {
                var at=Input.Date(date.Text)??throw new ArgumentException("Enter a reservation date.");
                string explanation=reason.Text,password=phrase.Password;
                phrase.Password="";
                var saved=await Task.Run(()=>_reservations.Confirm(_owner,booking.Id,booking.Revision,requestId,at,explanation,password));
                _list.ItemsSource=_book.List(_owner);
                Edit(saved);
            }));
        return panel;
    }
}
