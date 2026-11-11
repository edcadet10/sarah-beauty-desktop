using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReservationTests
{
    private static (Booking Booking,DocumentVersion Invoice) Setup(LedgerFixture f,bool resolved=true,bool hasTrial=true,bool late=false)
    {
        var book=new BookingBook(f.Business.Store);
        var artist=book.SaveArtist(f.Business.Owner,new Artist{Name="Fictional reservation artist"});
        var booking=BookingRules.Wedding("Fictional reservation",0,0) with{PrimaryCustomerId=f.CustomerId,
            PayerMode="Single",SinglePayerCustomerId=f.CustomerId,UnpaidResponsibility="Fictional payer covers all charges",
            EventDate=f.Date.AddDays(60),Venue=resolved?"Fictional event venue":"",ReadyBy=late?"11:00":"13:00",
            HasTrial=hasTrial,TrialCents=hasTrial?15000:0,CustomDepositCents=hasTrial?null:10000,
            Appointments=[new(Guid.NewGuid().ToString(),"Wedding",artist.Id,
                new DateTimeOffset(2027,3,11,10,0,0,TimeSpan.FromHours(-5)),new DateTimeOffset(2027,3,11,12,0,0,TimeSpan.FromHours(-5)),"Fictional venue")]};
        booking=book.Save(f.Business.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});
        var quotes=new QuoteBook(f.Business.Store);
        var draft=quotes.SaveDraft(f.Business.Owner,quotes.FromBooking(f.Business.Owner,booking) with{IssueDate=f.Date});
        var quote=quotes.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        quotes.Accept(f.Business.Owner,quote.Id,f.Date,"Fictional reservation agreement");
        var invoices=new InvoiceBook(f.Business.Store);
        var invoice=invoices.SaveDraft(f.Business.Owner,new InvoiceDraft{Name="Fictional reservation invoice",QuoteVersionId=quote.Id,
            PayerCustomerId=f.CustomerId,IssueDate=f.Date,Portions=quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});
        return(book.List(f.Business.Owner).Single(),invoices.Issue(f.Business.Owner,invoice.Id,invoice.Revision,BusinessFixture.Phrase));
    }

    private static void Pay(LedgerFixture f,DocumentVersion invoice,long cents,DateOnly? date=null,bool credit=false)
    {
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,cents) with{Date=date??f.Date});
        string creditId="";
        if(credit)
        {
            var credits=new CreditBook(f.Business.Store);
            var funding=credits.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional reservation credit",Date=date??f.Date,PaymentId=receipt.Id,Cents=cents});
            creditId=credits.Approve(f.Business.Owner,funding.Id,funding.Revision,BusinessFixture.Phrase).Id;
        }
        var apps=new PaymentApplications(f.Business.Store);
        var draft=apps.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional reservation payment",Date=date??f.Date,
            PaymentId=credit?"":receipt.Id,CreditId=creditId,InvoiceVersionId=invoice.Id,Cents=cents});
        apps.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    private static Booking Confirm(LedgerFixture f,Booking booking,string? request=null,DateOnly? date=null)=>
        new BookingReservations(f.Business.Store).Confirm(f.Business.Owner,booking.Id,booking.Revision,request??Guid.NewGuid().ToString(),
            date??f.Date,"Fictional recorded reservation",BusinessFixture.Phrase);

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ConfirmedBookingCanReviewAnAmendedAgreementAfterItsAdditionalDepositIsPaid(int originalDay)
    {
        using var f=new LedgerFixture();var (booking,invoice)=Setup(f);Pay(f,invoice,22500);var original=Confirm(f,booking,date:f.Date.AddDays(originalDay));
        var reviewDate=f.Date.AddDays(Math.Max(1,originalDay));
        var books=new BookingBook(f.Business.Store);booking=BookingRules.AddCharge(original,"Fictional agreed extra service","Custom",1,6000);
        booking=books.Save(f.Business.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});
        var quotes=new QuoteBook(f.Business.Store);var prior=quotes.Versions(f.Business.Owner).Single(v=>v.Snapshot.Kind==DocumentKind.Quote);
        var draft=quotes.SaveDraft(f.Business.Owner,quotes.FromBooking(f.Business.Owner,booking,prior.DocumentId) with{IssueDate=f.Date.AddDays(1)});
        var quote=quotes.Issue(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);quotes.Accept(f.Business.Owner,quote.Id,f.Date.AddDays(1),"Fictional amended service agreement");
        booking=books.List(f.Business.Owner).Single();Assert.Throws<ArgumentException>(()=>Confirm(f,booking,date:f.Date.AddDays(1)));
        var invoices=new InvoiceBook(f.Business.Store);var extra=quote.Snapshot.Lines.Single(l=>l.Description=="Fictional agreed extra service");
        var added=invoices.SaveDraft(f.Business.Owner,new InvoiceDraft{Name="Fictional additional service invoice",QuoteVersionId=quote.Id,
            PayerCustomerId=f.CustomerId,IssueDate=f.Date.AddDays(1),Portions=[new(extra.ChargeId,1)]});
        var issued=invoices.Issue(f.Business.Owner,added.Id,added.Revision,BusinessFixture.Phrase);Pay(f,issued,3000,f.Date.AddDays(1));
        if(originalDay>1)Assert.Throws<ArgumentException>(()=>Confirm(f,booking,date:f.Date.AddDays(1)));
        string request=Guid.NewGuid().ToString();var reservations=new BookingReservations(f.Business.Store){FailurePoint=_=>throw new IOException("Injected amended reservation failure")};
        Assert.Throws<IOException>(()=>reservations.Confirm(f.Business.Owner,booking.Id,booking.Revision,request,reviewDate,"Fictional recorded reservation",BusinessFixture.Phrase));
        Assert.Single(books.List(f.Business.Owner).Single().Reservations);
        reservations.FailurePoint=null;var reviewed=Confirm(f,booking,request,reviewDate);
        Assert.Equal("Confirmed",reviewed.Status);Assert.Equal(2,reviewed.Reservations.Length);Assert.Equal(original.History,reviewed.History);
        Assert.Equal(original.Reservations[0],reviewed.Reservations[0]);Assert.Equal(quote.Id,reviewed.Reservations[1].QuoteVersionId);
        Assert.Equal(10500,reviewed.Reservations[1].DepositCents);Assert.Equal(15000,reviewed.Reservations[1].TrialCents);
        Assert.Equal(reviewed.Revision,Confirm(f,booking,request.ToUpperInvariant(),reviewDate).Revision);Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);Assert.Equal(25500,f.Balance("1010"));
    }

    [Fact]
    public void AlreadyConfirmedCurrentAgreementDoesNotCreateAnotherReservationReview()
    {
        using var f=new LedgerFixture();var (booking,invoice)=Setup(f);Pay(f,invoice,22500);var confirmed=Confirm(f,booking);
        Assert.Throws<ArgumentException>(()=>Confirm(f,confirmed));Assert.Single(new BookingBook(f.Business.Store).List(f.Business.Owner).Single().Reservations);
    }

    [Fact]
    public void TrialAloneDoesNotReserveAndApprovedDepositAllowsOneRecordedConfirmation()
    {
        using var f=new LedgerFixture();
        var (booking,invoice)=Setup(f);
        Pay(f,invoice,15000);
        Assert.Throws<ArgumentException>(()=>Confirm(f,booking));
        Assert.Equal("Awaiting deposit",new BookingBook(f.Business.Store).List(f.Business.Owner).Single().Status);
        Pay(f,invoice,7500);
        string request=Guid.NewGuid().ToString();
        var confirmed=Confirm(f,booking,request);
        Assert.Equal("Confirmed",confirmed.Status);
        Assert.Equal(confirmed.Revision,Confirm(f,booking,request).Revision);
        Assert.Single(confirmed.Reservations);
        Assert.Equal(7500,confirmed.Reservations[0].DepositCents);
        Assert.Equal(22500,f.Balance("1010"));
    }

    [Fact]
    public void AnApprovedFundedCreditCanSatisfyTheReservationMilestones()
    {
        using var f=new LedgerFixture();
        var (booking,invoice)=Setup(f);
        Pay(f,invoice,22500,credit:true);
        Assert.Equal("Confirmed",Confirm(f,booking).Status);
        Assert.Equal(-22500,f.Balance("4000"));
    }

    [Fact]
    public void LaterPaymentsDoNotReserveAnEarlierDate()
    {
        using var f=new LedgerFixture();
        var (booking,invoice)=Setup(f);
        Pay(f,invoice,22500,f.Date.AddDays(1));
        Assert.Throws<ArgumentException>(()=>Confirm(f,booking));
        Assert.Equal("Confirmed",Confirm(f,booking,date:f.Date.AddDays(1)).Status);
    }

    [Fact]
    public void UnresolvedOrChangedEventDetailsBlockReservation()
    {
        using var f=new LedgerFixture();
        var (booking,invoice)=Setup(f,resolved:false);
        Pay(f,invoice,22500);
        Assert.Throws<ArgumentException>(()=>Confirm(f,booking));
        booking=new BookingBook(f.Business.Store).Save(f.Business.Owner,booking with{Venue="New venue not yet accepted"});
        Assert.Throws<ArgumentException>(()=>Confirm(f,booking));
    }

    [Fact]
    public void FailedReservationRollsBackStatusAndHistoryBeforeRetry()
    {
        using var f=new LedgerFixture();
        var (booking,invoice)=Setup(f);
        Pay(f,invoice,22500);
        var book=new BookingReservations(f.Business.Store){FailurePoint=_=>throw new IOException("Injected reservation failure")};
        string request=Guid.NewGuid().ToString();
        Assert.Throws<IOException>(()=>book.Confirm(f.Business.Owner,booking.Id,booking.Revision,request,f.Date,"Fictional reservation",BusinessFixture.Phrase));
        var unchanged=new BookingBook(f.Business.Store).List(f.Business.Owner).Single();
        Assert.Equal(booking.Revision,unchanged.Revision);
        Assert.Empty(unchanged.Reservations);
        Assert.Equal("Confirmed",Confirm(f,booking,request).Status);
    }

    [Fact]
    public void AnExplicitNoTrialDepositCanReserveWithoutInventingATrial()
    {
        using var f=new LedgerFixture();
        var (booking,invoice)=Setup(f,hasTrial:false);
        Pay(f,invoice,10000);
        var saved=Confirm(f,booking);
        Assert.Equal(0,saved.Reservations.Single().TrialCents);
        Assert.Equal(10000,saved.Reservations.Single().DepositCents);
    }

    [Fact]
    public void ReadyByConflictBlocksReservationAndReservationHistoryCannotBeEdited()
    {
        using var f=new LedgerFixture();
        var (booking,invoice)=Setup(f,late:true);
        Pay(f,invoice,22500);
        Assert.Throws<ArgumentException>(()=>Confirm(f,booking));
        var book=new BookingBook(f.Business.Store);
        booking=book.Save(f.Business.Owner,booking with{Appointments=booking.Appointments.Select(a=>a with{OverrideReason="Fictional client agrees to finish at noon"}).ToArray()});
        var confirmed=Confirm(f,booking);
        Assert.Throws<ArgumentException>(()=>book.Save(f.Business.Owner,confirmed with{Reservations=[]}));
    }
}
