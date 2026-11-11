using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Bookings;

public sealed class ServiceCatalog(BusinessStore store)
{
    private readonly RecordStore<ServiceDefinition> _records=new(store,"Service");
    private static readonly ServiceDefinition[] Defaults=[
        new(){Code="BRIDAL",Name="Bridal makeup including trial",Unit="Package",UnitCents=30000},
        new(){Code="TRIAL",Name="Included bridal trial portion",Kind="Trial portion",UnitCents=15000},
        new(){Code="GUEST",Name="Non-bride makeup",Unit="Person",UnitCents=12000},
        new(){Code="TRAVEL",Name="Wedding travel (round-trip miles)",Unit="Mile",UnitCents=100}];

    public List<ServiceDefinition> List(OwnerSession owner)=>_records.List(owner);

    public void Initialize(OwnerSession owner)=>store.Transaction(db=>
    {
        store.RequireSession(db,owner);
        var codes=_records.List(db,owner).Select(s=>s.Code).ToHashSet();
        var missing=Defaults.Where(s=>!codes.Contains(s.Code)).ToArray();
        foreach(var service in missing)Save(db,owner,service with{Id=Guid.NewGuid().ToString()});
        if(missing.Length>0)BusinessStore.Audit(db,owner,"Initialize service catalog",owner.BusinessId);
        return true;
    });

    public ServiceDefinition Save(OwnerSession owner,ServiceDefinition service)
    {
        service=service with{Id=service.Id.Length==0?Guid.NewGuid().ToString():service.Id,Name=service.Name.Trim(),Code=service.Code.Trim().ToUpperInvariant(),TaxReviewEvidence=service.TaxReviewEvidence.Trim()};
        return store.Write(owner,"Save service definition",service.Id,db=>Save(db,owner,service));
    }

    private ServiceDefinition Save(DbWork db,OwnerSession owner,ServiceDefinition service)
    {
        var previous=service.Revision==0?null:_records.Get(db,owner,service.Id);
        if(previous is not null && previous.Revision!=service.Revision)throw new InvalidOperationException("This service changed. Reload it before saving a new price or duration.");
        Validate(service,previous,_records.List(db,owner));
        var revision=new ServiceRevision(service.Revision+1,service.Name,service.Unit,service.Kind,service.UnitCents!.Value,service.DurationMinutes,service.Archived,owner.OwnerId,DateTimeOffset.UtcNow.ToString("O"))
            {TaxReviewStatus=service.TaxReviewStatus,TaxReviewEvidence=service.TaxReviewEvidence};
        return _records.Save(db,owner,service with{History=[..service.History,revision]});
    }

    public Booking AddService(OwnerSession owner,Booking booking,string serviceId,decimal quantity,int? reviewedRevision=null)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        var service=_records.Get(db,owner,serviceId);
        if(reviewedRevision.HasValue && service.Revision!=reviewedRevision.Value)throw new InvalidOperationException("This service changed. Reload the catalog choices and review its current price before adding it.");
        return ServiceBooking.Add(booking,service,quantity);
    });

    public Booking NewWedding(OwnerSession owner,string name,int guests=0,decimal miles=0)
    {
        if(guests is <0 or >1000 || miles is <0 or >100000)throw new ArgumentException("Use 0 to 1,000 guests and a nonnegative supported travel quantity.");
        Initialize(owner);
        return store.ReadSnapshot(db=>
        {
            store.RequireSession(db,owner);
            var rates=_records.List(db,owner).ToDictionary(s=>s.Code);
            var used=new[]{"BRIDAL","TRIAL"}.Concat(guests>0?["GUEST"]:[]).Concat(miles>0?["TRAVEL"]:[]);
            foreach(string code in used)
                if(rates[code].Archived || rates[code].UnitCents is null)throw new ArgumentException("Restore or review the starting service and its price before preparing a new wedding.");
            if(rates["TRIAL"].UnitCents>rates["BRIDAL"].UnitCents)throw new ArgumentException("The included trial portion cannot exceed its bridal package price.");
            var booking=ServiceBooking.Add(new(){Id=Guid.NewGuid().ToString(),Name=name.Trim(),TrialCents=rates["TRIAL"].UnitCents!.Value,TrialDurationMinutes=rates["TRIAL"].DurationMinutes},rates["BRIDAL"],1);
            if(guests>0)booking=ServiceBooking.Add(booking,rates["GUEST"],guests);
            if(miles>0)booking=ServiceBooking.Add(booking,rates["TRAVEL"],miles);
            return booking;
        });
    }

    private static void Validate(ServiceDefinition service,ServiceDefinition? previous,List<ServiceDefinition> others)
    {
        if(service.Name.Length is <1 or >200 || service.Code.Length is <1 or >40 || service.Code.Any(c=>!char.IsAsciiLetterOrDigit(c) && c!='-')
            || service.UnitCents is null or <0 || service.DurationMinutes is <=0 or >1440 || !new[]{"Person","Package","Mile","Custom"}.Contains(service.Unit)
            || !new[]{"Service","Trial portion"}.Contains(service.Kind))throw new ArgumentException("Review the service name, code, unit, nonnegative price, and optional duration of 1 to 1,440 minutes.");
        if(others.Any(s=>s.Id!=service.Id && s.Code==service.Code))throw new ArgumentException("Use a separate permanent service code, including for archived definitions.");
        if(!new[]{"Unreviewed","Review required","Reviewed"}.Contains(service.TaxReviewStatus) || service.TaxReviewStatus=="Reviewed" && string.IsNullOrWhiteSpace(service.TaxReviewEvidence))
            throw new ArgumentException("Choose the service's tax review status and retain evidence for a reviewed decision. Resolve actual rates separately on each booking.");
        if(previous is not null && previous.Code!=service.Code)throw new ArgumentException("A service keeps its permanent code. Archive it and create another definition for a new code.");
        if(!service.History.SequenceEqual(previous?.History??[]))throw new ArgumentException("Service price and duration history cannot be overwritten.");
        var standard=Defaults.SingleOrDefault(s=>s.Code==service.Code);
        if(standard is not null && (service.Unit!=standard.Unit || service.Kind!=standard.Kind))throw new ArgumentException("A starting service keeps its package, person, mile, or trial-portion meaning.");
    }
}
