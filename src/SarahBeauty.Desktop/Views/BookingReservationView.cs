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
        var fields=_fields;
        string requestId=Guid.NewGuid().ToString();
        panel.Add(Form.Text("Confirmation checks the accepted event details and approved trial/deposit allocations. Saving an inquiry or a trial payment alone does not reserve the date."),
            date,reason,phrase,status,Form.Action(booking.Status=="Confirmed"?"Review changed reservation":"Confirm and reserve this booking",status,async()=>
            {
                if(!ReferenceEquals(_fields,fields) || fields is null || fields.Read(booking)!=booking)
                    throw new ArgumentException("Save the current booking details before reviewing its reservation.");
                var at=Input.Date(date.Text)??throw new ArgumentException("Enter a reservation date.");
                string explanation=reason.Text,password=phrase.Password;
                phrase.Password="";
                var saved=await Task.Run(()=>_reservations.Confirm(_owner,booking.Id,booking.Revision,requestId,at,explanation,password));
                bool replace=ReservationEditorUnchanged(booking,fields);
                _list.ItemsSource=_book.List(_owner);
                if(replace)Edit(saved);
                else _status.Text="Reservation review recorded. Current unsaved fields are preserved; reload the booking before saving further changes.";
            }));
        return panel;
    }

    private bool ReservationEditorUnchanged(Booking booking,BookingFields fields)
    {
        if(!ReferenceEquals(_fields,fields))return false;
        try{return fields.Read(booking)==booking;}
        catch(Exception error) when(error is ArgumentException or FormatException or OverflowException){return false;}
    }
}
