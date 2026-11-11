namespace SarahBeauty.Business.Bookings;

internal static class CalendarConflicts
{
    internal static Dictionary<(string Booking,string Appointment),List<CalendarIssue>> Build(IEnumerable<CalendarWork> source)
    {
        var result=new Dictionary<(string Booking,string Appointment),List<CalendarIssue>>();var active=new List<CalendarWork>();
        foreach(var work in source.Where(w=>!w.Cancelled && w.HasTimes).OrderBy(w=>w.Appointment.Start))
        {
            active.RemoveAll(w=>w.Appointment.End<=work.Appointment.Start);
            foreach(var prior in active)
            {
                bool artist=prior.Appointment.ArtistId==work.Appointment.ArtistId,recipient=prior.People.Overlaps(work.People);
                if(!artist && !recipient)continue;
                Add(result,work,prior,artist,recipient);Add(result,prior,work,artist,recipient);
            }
            active.Add(work);
        }
        return result;
    }

    private static void Add(Dictionary<(string Booking,string Appointment),List<CalendarIssue>> result,CalendarWork work,CalendarWork other,bool artist,bool recipient)
    {
        if(!result.TryGetValue(work.Key,out var list)){list=[];result[work.Key]=list;}
        bool reviewed=!string.IsNullOrWhiteSpace(work.Appointment.OverrideReason) || work.Booking.Id==other.Booking.Id && !string.IsNullOrWhiteSpace(other.Appointment.OverrideReason);
        string context=other.Booking.Name+" • "+other.Appointment.Kind;
        if(artist)list.Add(new("ArtistOverlap","Artist overlaps "+context,reviewed,other.Booking.Id,other.Appointment.Id));
        if(recipient)list.Add(new("RecipientOverlap","Recipient overlaps "+context,reviewed,other.Booking.Id,other.Appointment.Id));
    }
}
