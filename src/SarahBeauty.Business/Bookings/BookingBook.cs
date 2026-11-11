using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class BookingBook(BusinessStore store)
{
    internal readonly RecordStore<Booking> Records = new(store, "Booking");
    private readonly RecordStore<Artist> _artists = new(store, "Artist");
    public List<Booking> List(OwnerSession owner) => Records.List(owner);
    public List<Artist> Artists(OwnerSession owner) => _artists.List(owner);

    public Artist SaveArtist(OwnerSession owner, Artist artist)
    {
        if (string.IsNullOrWhiteSpace(artist.Name)) throw new ArgumentException("Enter an artist name.");
        artist = artist with { Id = artist.Id.Length == 0 ? Guid.NewGuid().ToString() : artist.Id, Name = artist.Name.Trim() };
        return store.Write(owner, "Save artist", artist.Id, db => _artists.Save(db, owner, artist));
    }

    public Booking Save(OwnerSession owner, Booking booking)
    {
        booking = booking with { Id = booking.Id.Length == 0 ? Guid.NewGuid().ToString() : booking.Id, Name = booking.Name.Trim() };
        return store.Write(owner, "Save booking", booking.Id, db =>
        {
            Booking? previous = booking.Revision == 0 ? null : Records.Get(db, owner, booking.Id);
            if (booking.Status != (previous?.Status ?? "Inquiry"))
                throw new ArgumentException("Use a recorded booking status action to change its status.");
            if ((previous is not null && !previous.History.SequenceEqual(booking.History))
                || (previous is null && booking.History.Length > 0))
                throw new ArgumentException("Booking history cannot be overwritten.");
            if (previous is not null) PreserveLinks(previous, booking);
            if(booking.AcceptedQuoteVersionId!=(previous?.AcceptedQuoteVersionId??""))
                throw new ArgumentException("Record quote acceptance through the document workflow.");
            BookingValidation.Validate(db, owner, booking, Records.List(db, owner).Where(b => b.Id != booking.Id).ToList(), _artists.List(db, owner));
            return Records.Save(db, owner, booking);
        });
    }

    public Booking LinkPlace(OwnerSession owner, Booking booking, string placeId, string customerId) => Save(owner,
        booking with { Places = booking.Places.Select(p => p.Id == placeId && p.Active ? p with { CustomerId = customerId } : p).ToArray() });

    public Booking ChangeStatus(OwnerSession owner, Booking booking, string next, string reason) =>
        store.Write(owner, "Change booking status", booking.Id, db =>
        {
            var current = Records.Get(db, owner, booking.Id);
            if (current.Revision != booking.Revision) throw new InvalidOperationException("Reload the changed booking.");
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Record a reason for the status change.");
            if (current.Status == next) throw new ArgumentException("The booking already has this status.");
            if (next != "Cancelled" && !(next == "Completed" && current.Status == "Confirmed"))
                throw new ArgumentException("This status action is not available. Confirmation requires an approved deposit and reservation.");
            return Transition(db, owner, current, next, reason);
        });

    internal Booking Transition(DbWork db, OwnerSession owner, Booking current, string next, string reason) =>
        Records.Save(db, owner, current with { Status = next, History = [.. current.History,
            new(current.Status, next, reason, owner.OwnerId, DateTimeOffset.UtcNow.ToString("O"))] });

    private static void PreserveLinks(Booking previous, Booking edited)
    {
        if (previous.Charges.Any(c => !edited.Charges.Any(n => n.Id == c.Id))
            || previous.Places.Any(p => !edited.Places.Any(n => n.Id == p.Id))
            || previous.Appointments.Any(a => !edited.Appointments.Any(n => n.Id == a.Id)))
            throw new ArgumentException("Retain existing charges, places, and appointments. Mark removed work inactive or cancelled.");
    }
}
