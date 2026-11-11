using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Dashboard;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class DashboardOperationsTests
{
    private static DashboardRequest Request=>new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
    private static DashboardOperationsSnapshot Read(LedgerFixture f,DateOnly? asOf=null)=>new DashboardOperationsSources(f.Business.Store).Load(f.Business.Owner,asOf??new(2027,1,31));

    [Fact]
    public void SavedDraftAndUnknownInvoiceDateRemainSeparateWithoutPostingMoney()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,5000));
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var result=new DashboardSources(f.Business.Store).Load(f.Business.Owner,Request);
        var pending=Assert.Single(result.Operations.Work,w=>w.Kind=="Draft approval");Assert.Equal(draft.Id,pending.RecordId);Assert.Equal("Money",pending.Route);
        var date=Assert.Single(result.Operations.Work,w=>w.Kind=="Invoice due date");Assert.Equal(invoice.Id,date.RecordId);Assert.Equal(10000,date.RelatedCents);
        Assert.Equal(watermark,result.Finance.Reports.Watermark);Assert.Equal(watermark,result.Upcoming.Watermark);Assert.Equal(watermark,result.Operations.Watermark);
        Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));Assert.Empty(f.Book.Posted(f.Business.Owner));Assert.Equal(0,f.Balance("1010"));
    }

    [Fact]
    public void UnclassifiedCreditReservationRemainsPendingUntilReleasedAndClassified()
    {
        using var f=new LedgerFixture();var receipt=f.Post(f.Draft(FinancialKind.UnclassifiedReceipt,10000) with{CustomerId=f.CustomerId});var book=new CreditBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,new(){Name="Fictional reserved suspense",PaymentId=receipt.Id,Cents=4000,Date=f.Date});
        var credit=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        FinancialDraft Classification(long cents)=>f.Draft(FinancialKind.ReceiptClassification,cents) with{OriginalId=receipt.Id,CustomerId=f.CustomerId,CounterAccountId=f.Accounts["4000"].Id,EvidenceId="Fictional classification review"};
        f.Post(Classification(6000));Assert.Equal(4000,Assert.Single(Read(f).Work,w=>w.Kind=="Received-value review").RelatedCents);
        var release=book.SaveDraft(f.Business.Owner,new(){Name="Fictional release for classification",Action=CreditAction.ReturnToReceipt,CreditId=credit.Id,Cents=4000,Date=f.Date});
        book.Approve(f.Business.Owner,release.Id,release.Revision,BusinessFixture.Phrase);f.Post(Classification(4000));
        Assert.DoesNotContain(Read(f).Work,w=>w.Kind=="Received-value review");Assert.Equal(10000,f.Balance("1010"));Assert.Equal(-10000,f.Balance("4000"));Assert.Equal(0,f.Balance("2400"));
    }

    [Fact]
    public void FullyLinkedTripDisappearsAndADatedReleaseRestoresOnlyTheUnpaidPart()
    {
        using var f=new LedgerFixture();var trip=TripPaymentStorageTests.Trip(f,1000);var payment=f.Post(f.Draft(FinancialKind.Expense,1000));var book=new TripPayments(f.Business.Store);
        var first=TripPaymentStorageTests.Link(f,book,TripPaymentStorageTests.Draft(f,trip,payment,600));
        Assert.Equal(400,Assert.Single(Read(f).Work,w=>w.Kind=="Trip reimbursement").RelatedCents);
        TripPaymentStorageTests.Link(f,book,TripPaymentStorageTests.Draft(f,trip,payment,400));Assert.DoesNotContain(Read(f).Work,w=>w.Kind=="Trip reimbursement");
        TripPaymentStorageTests.Release(f,book,first,100,2);
        Assert.DoesNotContain(Read(f,f.Date.AddDays(1)).Work,w=>w.Kind=="Trip reimbursement");
        Assert.Equal(100,Assert.Single(Read(f,f.Date.AddDays(2)).Work,w=>w.Kind=="Trip reimbursement").RelatedCents);
        Assert.Equal(1000,f.Balance("5010"));Assert.Equal(-1000,f.Balance("1010"));Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void SupplyAlertsUseInclusiveExpiryAndCurrentStatusWithoutPrivateNotes()
    {
        using var f=new LedgerFixture();var book=new OperationalResources(f.Business.Store);
        book.SaveSupply(f.Business.Owner,new(){Name="Fictional low",SupplyStatus="Low",ExpiryDate=f.Date.AddDays(-1),Notes="PRIVATE-SUPPLY"});
        book.SaveSupply(f.Business.Owner,new(){Name="Fictional expiry today",ExpiryDate=f.Date});
        book.SaveSupply(f.Business.Owner,new(){Name="Fictional finished",SupplyStatus="Finished",ExpiryDate=f.Date.AddDays(1)});
        book.SaveSupply(f.Business.Owner,new(){Name="Fictional unknown expiry"});
        book.SaveSupply(f.Business.Owner,new(){Name="Fictional archived",SupplyStatus="Low",ExpiryDate=f.Date.AddDays(-1),Archived=true});
        var result=Read(f,f.Date);Assert.Equal(4,result.Supplies.Length);Assert.Single(result.Supplies,s=>s.Low);Assert.Single(result.Supplies,s=>s.Finished);
        Assert.Equal(2,result.Supplies.Count(s=>s.ExpiryAlert));Assert.Single(result.Supplies,s=>s.ExpiryDate is null);Assert.DoesNotContain("PRIVATE-SUPPLY",JsonSerializer.Serialize(result));Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void ExplicitSetupReviewsClearTheirOwnTasksWithoutTurningUnknownValuesIntoZero()
    {
        using var f=new LedgerFixture();var catalog=new ServiceCatalog(f.Business.Store);catalog.Initialize(f.Business.Owner);var guest=catalog.List(f.Business.Owner).Single(s=>s.Code=="GUEST");
        var taxes=new TaxPeriodBook(f.Business.Store);var tax=taxes.Save(f.Business.Owner,new(){Name="Fictional tax review",Jurisdiction="Fictional authority",From=new(2027,1,1),Through=new(2027,1,31)});
        var before=Read(f);Assert.Contains(before.Work,w=>w.Kind=="Service setup" && w.RecordId==guest.Id);Assert.Contains(before.Work,w=>w.Kind=="Tax review" && w.Reasons.Any(r=>r.Contains("unconfirmed")));
        Assert.Contains(before.Work,w=>w.Kind=="Opening review" && w.RecordId==f.Accounts["1010"].Id);Assert.Contains(before.Work,w=>w.Kind=="Asset setup");
        catalog.Save(f.Business.Owner,guest with{TaxReviewStatus="Reviewed",TaxReviewEvidence="Fictional reviewed service treatment"});
        taxes.RecordStatus(f.Business.Owner,tax.Id,new(Guid.NewGuid().ToString(),tax.Revision,"Not required",null,null,"","Fictional reporting review","Fictional explicit decision"),BusinessFixture.Phrase);
        new OpeningBalances(f.Business.Store).ConfirmZero(f.Business.Owner,f.Accounts["1010"].Id,f.Date,"Fictional zero-opening evidence",BusinessFixture.Phrase);
        var after=Read(f);Assert.DoesNotContain(after.Work,w=>w.Kind=="Service setup" && w.RecordId==guest.Id);Assert.DoesNotContain(after.Work,w=>w.Kind=="Tax review");
        Assert.DoesNotContain(after.Work,w=>w.Kind=="Opening review" && w.RecordId==f.Accounts["1010"].Id);Assert.Contains(after.Work,w=>w.Kind=="Asset setup");Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void CompositeReadCannotMixConcurrentCashBookingSupplyOrDraftChanges()
    {
        using var f=new LedgerFixture();f.Business.Files.Execute("PRAGMA journal_mode=WAL;");var ready=ReservationTests.Setup(f);ReservationTests.Pay(f,ready.Invoice,22500);var booking=ReservationTests.Confirm(f,ready.Booking);
        var operations=new OperationalResources(f.Business.Store);var supply=operations.SaveSupply(f.Business.Owner,new(){Name="Fictional concurrent supply",SupplyStatus="Low"});
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;
        var sources=new DashboardSources(f.Business.Store){AfterFinanceRead=()=>
        {
            f.Post(f.Draft(FinancialKind.Expense,2000));operations.SaveSupply(f.Business.Owner,supply with{SupplyStatus="Finished"});f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,5000));
            new BookingBook(f.Business.Store).ChangeStatus(f.Business.Owner,booking,"Cancelled","Fictional concurrent cancellation");
        }};
        var captured=sources.Load(f.Business.Owner,Request);Assert.Equal(22500,captured.Finance.CashAccounts.Single(c=>c.Account.Code=="1010").BalanceCents);
        Assert.Single(captured.Upcoming.Bookings);Assert.True(Assert.Single(captured.Operations.Supplies).Low);Assert.DoesNotContain(captured.Operations.Work,w=>w.Kind=="Draft approval");
        Assert.Equal(watermark,captured.Finance.Reports.Watermark);Assert.Equal(watermark,captured.Upcoming.Watermark);Assert.Equal(watermark,captured.Operations.Watermark);
        var current=new DashboardSources(f.Business.Store).Load(f.Business.Owner,Request);Assert.Equal(20500,current.Finance.CashAccounts.Single(c=>c.Account.Code=="1010").BalanceCents);
        Assert.Empty(current.Upcoming.Bookings);Assert.True(Assert.Single(current.Operations.Supplies).Finished);Assert.Single(current.Operations.Work,w=>w.Kind=="Draft approval");
    }
}
