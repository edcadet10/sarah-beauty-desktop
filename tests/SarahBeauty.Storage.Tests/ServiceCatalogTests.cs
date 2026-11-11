using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;

namespace SarahBeauty.Storage.Tests;

public sealed class ServiceCatalogTests
{
    [Fact]
    public void BookingServiceSourcesRequireAnOwnedRetainedRevisionAndValidOptionalDurations()
    {
        using var f=new BusinessFixture();var catalog=new ServiceCatalog(f.Store);catalog.Initialize(f.Owner);
        var guest=catalog.List(f.Owner).Single(s=>s.Code=="GUEST");
        var bookings=new BookingBook(f.Store);var booking=catalog.AddService(f.Owner,new(){Name="Fictional sourced booking",HasTrial=false,TrialCents=0},guest.Id,2);
        booking=bookings.Save(f.Owner,booking);
        Assert.Throws<ArgumentException>(()=>bookings.Save(f.Owner,booking with{Charges=[booking.Charges[0] with{ServiceRevision=999}]}));
        Assert.Throws<ArgumentException>(()=>bookings.Save(f.Owner,booking with{Charges=[booking.Charges[0] with{DurationMinutes=0}]}));
        Assert.Throws<ArgumentException>(()=>bookings.Save(f.Owner,booking with{TrialDurationMinutes=-1}));
        using var foreign=new BusinessFixture();var other=new ServiceCatalog(foreign.Store);other.Initialize(foreign.Owner);
        Assert.Throws<ArgumentException>(()=>bookings.Save(f.Owner,booking with{Charges=[booking.Charges[0] with{ServiceId=other.List(foreign.Owner).First().Id}]}));
    }

    [Fact]
    public void CatalogEditsDoNotChangeAnIssuedQuotesOriginalPricesOrHash()
    {
        using var f=new BusinessFixture();var catalog=new ServiceCatalog(f.Store);
        var customer=new CustomerBook(f.Store).Save(f.Owner,new(){FullName="Fictional catalog payer"});
        var booking=catalog.NewWedding(f.Owner,"Fictional catalog quote",2) with{PrimaryCustomerId=customer.Id,PayerMode="Single",SinglePayerCustomerId=customer.Id,UnpaidResponsibility="Fictional agreed payer"};
        booking=new BookingBook(f.Store).Save(f.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});
        var quotes=new QuoteBook(f.Store);var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking));
        var issued=quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        var guest=catalog.List(f.Owner).Single(s=>s.Code=="GUEST");catalog.Save(f.Owner,guest with{UnitCents=13500,DurationMinutes=45});
        var retained=quotes.Versions(f.Owner).Single();
        Assert.Equal(issued.Hash,retained.Hash);
        Assert.Equal(12000,retained.Snapshot.Lines.Single(l=>l.Unit=="Person").UnitCents);
        Assert.Equal(13500,catalog.NewWedding(f.Owner,"Fictional next quote",2).Charges.Single(c=>c.Unit=="Person").UnitCents);
    }

    [Fact]
    public void StartingCatalogPreservesTheAgreedPackageAndTrialWithoutInventingDurationsOrTax()
    {
        using var f=new BusinessFixture();var book=new ServiceCatalog(f.Store);
        var wedding=book.NewWedding(f.Owner,"Fictional catalog wedding",9,76.6m);
        Assert.Equal(4,book.List(f.Owner).Count);
        Assert.Equal(145660,BookingRules.Totals(wedding).NetCents);
        Assert.Equal(15000,wedding.TrialCents);Assert.Null(wedding.TrialDurationMinutes);
        Assert.Equal(3,wedding.Charges.Length);Assert.Equal(10,wedding.Places.Length);
        Assert.All(wedding.Charges,c=>{Assert.Null(c.TaxRate);Assert.Null(c.DurationMinutes);Assert.Equal(1,c.ServiceRevision);Assert.NotEmpty(c.ServiceId);});
        Assert.Null(BookingRules.Totals(wedding).Plan);
        var resolved=wedding with{Charges=wedding.Charges.Select(c=>c with{TaxRate=0m}).ToArray()};
        Assert.Equal(65330,BookingRules.Totals(resolved).Plan!.DepositCents);
        book.Initialize(f.Owner);Assert.Equal(4,book.List(f.Owner).Count);
    }

    [Fact]
    public void NewDraftsCopyReviewedPricesAndDurationsWhileExistingDraftsKeepTheirSnapshots()
    {
        using var f=new BusinessFixture();var book=new ServiceCatalog(f.Store);var before=book.NewWedding(f.Owner,"Fictional earlier wedding",2);
        var guest=book.List(f.Owner).Single(s=>s.Code=="GUEST");
        var revised=book.Save(f.Owner,guest with{UnitCents=13500,DurationMinutes=45});
        book.Initialize(f.Owner);var after=book.NewWedding(f.Owner,"Fictional newer wedding",2);
        Assert.Equal(12000,before.Charges.Single(c=>c.Unit=="Person").UnitCents);
        Assert.Null(before.Charges.Single(c=>c.Unit=="Person").DurationMinutes);
        var line=after.Charges.Single(c=>c.Unit=="Person");Assert.Equal(13500,line.UnitCents);Assert.Equal(45,line.DurationMinutes);Assert.Equal(2,line.ServiceRevision);
        Assert.Equal(2,revised.History.Length);Assert.Equal(12000,revised.History[0].UnitCents);Assert.Equal(f.Owner.OwnerId,revised.History[1].OwnerId);
        Assert.Throws<InvalidOperationException>(()=>book.Save(f.Owner,guest with{UnitCents=14000}));
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,revised with{History=[]}));
    }

    [Fact]
    public void CustomAndNoTrialBookingsUseTheSelectedServiceWithoutAnotherTrialCharge()
    {
        using var f=new BusinessFixture();var book=new ServiceCatalog(f.Store);book.Initialize(f.Owner);
        var custom=book.Save(f.Owner,new(){Name="Fictional event makeup",Code="EVENT",Unit="Person",UnitCents=8000,DurationMinutes=30});
        var booking=book.AddService(f.Owner,new Booking{HasTrial=false,TrialCents=0,Name="Fictional no-trial booking"},custom.Id,3);
        Assert.Equal(24000,BookingRules.Totals(booking).NetCents);Assert.Equal(3,booking.Places.Length);Assert.False(booking.HasTrial);
        Assert.Null(BookingRules.Totals(booking).Plan);
        var trial=book.List(f.Owner).Single(s=>s.Code=="TRIAL");
        Assert.Throws<ArgumentException>(()=>book.AddService(f.Owner,booking,trial.Id,1));
        Assert.Throws<ArgumentException>(()=>book.AddService(f.Owner,booking,custom.Id,1.5m));
        custom=book.Save(f.Owner,custom with{Archived=true});
        Assert.Throws<ArgumentException>(()=>book.AddService(f.Owner,booking,custom.Id,1));
        Assert.Equal(24000,BookingRules.Totals(booking).NetCents);
    }

    [Fact]
    public void InvalidCodesDurationsPricesAndChangedStartingMeaningsAreRejected()
    {
        using var f=new BusinessFixture();var book=new ServiceCatalog(f.Store);book.Initialize(f.Owner);
        var guest=book.List(f.Owner).Single(s=>s.Code=="GUEST");
        foreach(var invalid in new[]{guest with{Name=""},guest with{Code="OTHER"},guest with{UnitCents=null},guest with{UnitCents=-1},guest with{DurationMinutes=0},guest with{DurationMinutes=1441},guest with{Unit="Mile"}})
            Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,invalid));
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,new(){Name="Fictional duplicate",Code=" guest ",Unit="Person",UnitCents=1}));
        Assert.Throws<ArgumentException>(()=>book.Save(f.Owner,guest with{TaxReviewStatus="Reviewed"}));
        var reviewed=book.Save(f.Owner,guest with{TaxReviewStatus="Reviewed",TaxReviewEvidence="Fictional service classification review; actual rate remains per booking"});
        Assert.Equal("Reviewed",reviewed.History[^1].TaxReviewStatus);
        Assert.Null(book.NewWedding(f.Owner,"Fictional tax review stays separate",1).Charges.Single(c=>c.Unit=="Person").TaxRate);
        Assert.Throws<ArgumentException>(()=>book.NewWedding(f.Owner,"Fictional invalid quantity",-1));
        var bridal=book.List(f.Owner).Single(s=>s.Code=="BRIDAL");book.Save(f.Owner,bridal with{UnitCents=10000});
        Assert.Throws<ArgumentException>(()=>book.NewWedding(f.Owner,"Fictional unsupported trial share"));
    }

    [Fact]
    public void ArchivedStartingServicesStayArchivedAndDefinitionsCannotCrossBusinesses()
    {
        using var f=new BusinessFixture();var book=new ServiceCatalog(f.Store);book.Initialize(f.Owner);
        var guest=book.List(f.Owner).Single(s=>s.Code=="GUEST");book.Save(f.Owner,guest with{Archived=true});book.Initialize(f.Owner);
        Assert.True(book.List(f.Owner).Single(s=>s.Code=="GUEST").Archived);
        Assert.Throws<ArgumentException>(()=>book.NewWedding(f.Owner,"Fictional archived starting service",1));
        Assert.Single(book.NewWedding(f.Owner,"Fictional bridal-only service").Charges);
        using var foreign=new BusinessFixture();
        Assert.Throws<UnauthorizedAccessException>(()=>book.List(foreign.Owner));
        Assert.Throws<ArgumentException>(()=>new ServiceCatalog(foreign.Store).AddService(foreign.Owner,new(),guest.Id,1));
    }
}
