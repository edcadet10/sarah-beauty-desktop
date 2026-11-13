using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class LiveEntryGuardTests
{
    [Theory]
    [InlineData("receipt")][InlineData("quote")]
    public void UnreviewedLiveSetupCannotApproveMoneyOrIssueTaxBearingDocuments(string operation)
    {
        using var f=new Fixture();var money=new FinanceBook(f.Store);var draft=money.SaveDraft(f.Owner,f.Receipt());var quotes=new QuoteBook(f.Store);var quote=f.Quote();long watermark=f.Watermark;
        Assert.Throws<ArgumentException>(()=>{if(operation=="receipt")money.Approve(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);else quotes.Issue(f.Owner,quote.Id,quote.Revision,BusinessFixture.Phrase);});Assert.Equal(watermark,f.Watermark);Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Movements;"));Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM DocumentVersions;"));
    }
    [Fact]
    public void ExplicitReviewedSetupAllowsOperationsAndAnExactPostingRetryPreservesItsOriginalFact()
    {
        using var f=new Fixture();var reviewed=f.Review();var money=new FinanceBook(f.Store);var draft=money.SaveDraft(f.Owner,f.Receipt());var posted=money.Approve(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);var quotes=new QuoteBook(f.Store);var quote=f.Quote();Assert.Equal("LIVE",quotes.Issue(f.Owner,quote.Id,quote.Revision,BusinessFixture.Phrase).Snapshot.Mode);
        new BusinessProfiles(f.Store).Confirm(f.Owner,new(1,reviewed.Profile with{Tax=new()},"Fictional tax review reopened"),BusinessFixture.Phrase,Guid.NewGuid().ToString());long watermark=f.Watermark;Assert.Equal(posted.Id,money.Approve(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Id);Assert.Equal(watermark,f.Watermark);Assert.Equal(1L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Movements;"));
    }
    [Theory]
    [InlineData(-3)][InlineData(1)]
    public void LiveDatesCannotCrossTheReviewedStartOrBusinessToday(int difference)
    {
        using var f=new Fixture();f.Review();var money=new FinanceBook(f.Store);var draft=money.SaveDraft(f.Owner,f.Receipt() with{Date=f.Today.AddDays(difference)});long watermark=f.Watermark;Assert.Throws<ArgumentException>(()=>money.Approve(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));Assert.Equal(watermark,f.Watermark);Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Movements;"));
    }
    [Fact]
    public void ACompetingSetupReopeningBeforeWriterReservationPreventsPosting()
    {
        using var f=new Fixture();var reviewed=f.Review();var money=new FinanceBook(f.Store);var draft=money.SaveDraft(f.Owner,f.Receipt());long watermark=f.Watermark;int reservations=0;bool competed=false;var other=new BusinessProfiles(new BusinessStore(f.Store.DatabasePath,BusinessMode.Live));f.Store.BeforeTransactionReservation=()=>
        {
            if(++reservations!=2)return;f.Store.BeforeTransactionReservation=null;other.Confirm(f.Owner,new(1,reviewed.Profile with{Tax=new()},"Fictional concurrent tax review reopening"),BusinessFixture.Phrase,Guid.NewGuid().ToString());competed=true;
        };
        Assert.Throws<ArgumentException>(()=>money.Approve(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));Assert.True(competed);Assert.Equal(watermark+1,f.Watermark);Assert.Equal(0L,TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM Movements;"));Assert.Equal(2,new BusinessProfiles(f.Store).Current(f.Owner)!.Revision);
    }
    private sealed class Fixture:IDisposable
    {
        internal TestStore Files {get;}=new();
        internal BusinessStore Store {get;}
        internal OwnerSession Owner {get;}
        internal string CustomerId {get;}
        internal Dictionary<string,Account> Accounts {get;}
        internal DateOnly Today=>DateOnly.FromDateTime(DateTime.UtcNow);
        internal long Watermark=>Convert.ToInt64(TestStore.Scalar(Store.DatabasePath,"SELECT Watermark FROM Business;"));
        internal Fixture()
        {
            LocalDatabase.InitializeDatabaseAt(Files.DatabasePath,BusinessMode.Live);Store=new(Files.DatabasePath,BusinessMode.Live);Owner=new OwnerAccess(Store).Setup("Fictional guarded LIVE business","Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase);var chart=new ChartOfAccounts(Store);chart.Initialize(Owner);Accounts=chart.List(Owner).ToDictionary(a=>a.Code);CustomerId=new CustomerBook(Store).Save(Owner,new(){FullName="Fictional guarded LIVE payer"}).Id;
        }
        internal FinancialDraft Receipt()=>new(){Kind=FinancialKind.Receipt,Name="Fictional guarded receipt",Date=Today,AmountCents=1000,TaxResolved=true,CashAccountId=Accounts["1010"].Id,CounterAccountId=Accounts["4000"].Id,CustomerId=CustomerId};
        internal BusinessProfileVersion Review()=>new BusinessProfiles(Store).Confirm(Owner,new(0,LiveSetupPolicyTests.ReviewedProfile() with{DisplayName=Store.BusinessName(Owner),BooksStartDate=Today.AddDays(-2)},"Fictional explicit LIVE setup review"),BusinessFixture.Phrase,Guid.NewGuid().ToString());
        internal DocumentDraft Quote()
        {
            var booking=BookingRules.AddCharge(new Booking{Name="Fictional guarded LIVE booking",HasTrial=false,TrialCents=0,CustomDepositCents=0,PrimaryCustomerId=CustomerId,PayerMode="Single",SinglePayerCustomerId=CustomerId,UnpaidResponsibility="Fictional explicit payer agreement"},"Fictional service","Custom",1,5000);booking=new BookingBook(Store).Save(Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});var quotes=new QuoteBook(Store);return quotes.SaveDraft(Owner,quotes.FromBooking(Owner,booking) with{IssueDate=Today});
        }
        public void Dispose()=>Files.Dispose();
    }
}
