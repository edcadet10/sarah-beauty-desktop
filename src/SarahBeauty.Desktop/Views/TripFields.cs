using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ResourceView
{
    private void Edit(MileageTrip trip)
    {
        _detail.Children.Clear();
        var purpose=Form.Input("Business purpose",trip.Name);
        var date=Form.Input("Actual trip date (YYYY-MM-DD)",trip.TripDate?.ToString("yyyy-MM-dd")??"");
        var driver=Form.Records("Owner driver (choose one driver only)",new OwnerAccess(store).Owners(),trip.DriverOwnerId);
        var artist=Form.Records("Artist driver",new BookingBook(store).Artists(owner).Select(a=>(a.Id,a.Name)),trip.DriverArtistId);
        var booking=Form.Records("Related booking (optional)",new BookingBook(store).List(owner).Select(b=>(b.Id,b.Name)),trip.BookingId);
        var origin=Form.Input("Origin",trip.Origin);
        var destination=Form.Input("Destination",trip.Destination);
        var miles=Form.Input("Actual miles traveled",trip.ActualMiles.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var parking=Form.Input("Actual parking in USD",Money.Input(trip.ParkingCents));
        var tolls=Form.Input("Actual tolls in USD",Money.Input(trip.TollsCents));
        var reimbursement=Form.Select("Reimbursement review",["Unreviewed","Not requested","Requested"],trip.ReimbursementStatus);
        var evidence=Form.Input("Trip evidence reference",trip.EvidenceId);
        var archived=Archived(trip.Archived);
        var status=Form.Text();
        _detail.Add(Form.Title("Actual trip"),Form.Text("Actual mileage is separate from quoted travel. This log does not apply a tax rate, invent a deduction, or post reimbursement. Approve any actual business payment separately."),
            purpose,date,driver,artist,booking,origin,destination,miles,parking,tolls,reimbursement,evidence,archived,status,Form.Action("Save actual trip",status,async()=>
            {
                var value=trip with{Name=purpose.Text,TripDate=Input.Date(date.Text),DriverOwnerId=driver.SelectedId(),DriverArtistId=artist.SelectedId(),
                    BookingId=booking.SelectedId(),Origin=origin.Text,Destination=destination.Text,ActualMiles=Input.Decimal(miles.Text),ParkingCents=Money.Parse(parking.Text),
                    TollsCents=Money.Parse(tolls.Text),ReimbursementStatus=(string)reimbursement.SelectedItem,EvidenceId=evidence.Text.Trim(),Archived=archived.IsChecked==true};
                var saved=await Task.Run(()=>_operations.SaveTrip(owner,value));
                Refresh();
                Edit(saved);
            }));
    }
}
