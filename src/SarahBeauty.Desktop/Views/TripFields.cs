using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using System.Text.Json;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class ResourceView
{
    private void Edit(MileageTrip trip)
    {
        _detail.Children.Clear();
        var owners=new OwnerAccess(store).Owners();var artists=new BookingBook(store).Artists(owner);
        var names=owners.ToDictionary(o=>o.Id,o=>o.Name);foreach(var person in artists)names.TryAdd(person.Id,person.Name);
        string Who(string id)=>names.GetValueOrDefault(id,"Former person")+" • "+id[..Math.Min(8,id.Length)];
        var purpose=Form.Input("Business purpose",trip.Name);
        var date=Form.Input("Actual trip date (YYYY-MM-DD)",trip.TripDate?.ToString("yyyy-MM-dd")??"");
        var driver=Form.Records("Owner driver (choose one driver only)",owners,trip.DriverOwnerId);
        var artist=Form.Records("Artist driver",artists.Select(a=>(a.Id,a.Name+(a.Archived?" • Archived":""))),trip.DriverArtistId);
        var booking=Form.Records("Related booking (optional)",new BookingBook(store).List(owner).Select(b=>(b.Id,b.Name)),trip.BookingId);
        var origin=Form.Input("Origin",trip.Origin);
        var destination=Form.Input("Destination",trip.Destination);
        var miles=Form.Input("Actual miles traveled",trip.ActualMiles.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var parking=Form.Input("Actual parking in USD",Money.Input(trip.ParkingCents));
        var tolls=Form.Input("Actual tolls in USD",Money.Input(trip.TollsCents));
        var reimbursement=Form.Select("Reimbursement review",["Unreviewed","Not requested","Requested"],trip.ReimbursementStatus);
        var requested=Form.Input("Reviewed reimbursement requested in USD (blank means unknown)",trip.RequestedReimbursementCents.HasValue?Money.Input(trip.RequestedReimbursementCents.Value):"");
        var evidence=Form.MultilineInput("Trip evidence reference",trip.EvidenceId);
        var archived=Archived(trip.Archived);
        var status=Form.Text();
        MileageTrip Read()=>trip with{Name=purpose.Text,TripDate=Input.Date(date.Text),DriverOwnerId=driver.SelectedId(),DriverArtistId=artist.SelectedId(),
            BookingId=booking.SelectedId(),Origin=origin.Text,Destination=destination.Text,ActualMiles=Input.Decimal(miles.Text),ParkingCents=Money.Parse(parking.Text),
            TollsCents=Money.Parse(tolls.Text),ReimbursementStatus=(string)reimbursement.SelectedItem,RequestedReimbursementCents=OptionalMoney(requested.Text),EvidenceId=Form.ReadInput(evidence,trip.EvidenceId).Trim(),Archived=archived.IsChecked==true};
        bool Changed(){try{return JsonSerializer.Serialize(Read())!=JsonSerializer.Serialize(trip);}catch{return true;}}
        TrackDraft(trip,Read);
        _detail.Add(Form.Title("Actual trip"),Form.Text("Actual mileage is separate from quoted travel. This log does not apply a tax rate, invent a deduction, or post reimbursement. Approve any actual business payment separately."),
            purpose,date,driver,artist,booking,origin,destination,miles,parking,tolls,reimbursement,requested,evidence,archived,status,Form.Action("Save actual trip",status,async()=>
            {
                var value=Read();
                await SaveEntry(()=>_operations.SaveTrip(owner,value),Edit);
            }),PagedReport.Build("Trip review history",["Revision / time","Driver / route","Actual miles / costs","Request review / evidence"],trip.History.Select(h=>new[]{
                $"{h.Revision} • {h.AtUtc}\n{Who(h.OwnerId)}",$"{Who(h.DriverOwnerId.Length>0?h.DriverOwnerId:h.DriverArtistId)}\n{h.Origin} → {h.Destination}",
                $"{h.ActualMiles} miles • Parking {Money.Display(h.ParkingCents)} • Tolls {Money.Display(h.TollsCents)}",
                $"{h.ReimbursementStatus} • {(h.RequestedReimbursementCents.HasValue?Money.Display(h.RequestedReimbursementCents.Value):"Unknown amount")}\n{h.EvidenceId}"})));
        if(trip.Revision>0)_detail.Add(new TripPaymentView(store,owner,trip.Id,Changed).Build());
    }
}
