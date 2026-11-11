using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class TripPaymentStorageTests
{
    internal static MileageTrip Trip(LedgerFixture f,long cents=10000)=>new OperationalResources(f.Business.Store).SaveTrip(f.Business.Owner,
        TripReviewTests.Trip(f) with{RequestedReimbursementCents=cents,ReimbursementStatus="Requested",EvidenceId="Fictional reviewed reimbursement claim"});
    internal static TripPaymentDraft Draft(LedgerFixture f,MileageTrip trip,PostedMovement payment,long cents,int day=1)=>
        new(trip.Id,payment.Id,f.Date.AddDays(day),cents,"Fictional evidence confirms this payment paid the identified trip driver","Fictional actual payment evidence");
    internal static LinkedTripPayment Link(LedgerFixture f,TripPayments book,TripPaymentDraft draft)
    {
        var preview=book.Preview(f.Business.Owner,draft);return book.Approve(f.Business.Owner,draft,preview.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    }
    internal static ReleasedTripPayment Release(LedgerFixture f,TripPayments book,LinkedTripPayment link,long cents,int day=2)
    {
        var draft=new TripPaymentReleaseDraft(link.Id,f.Date.AddDays(day),cents,"Fictional corrected trip attribution","Fictional review evidence");
        var preview=book.PreviewRelease(f.Business.Owner,draft);return book.Release(f.Business.Owner,draft,preview.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
    }

    [Fact]
    public void PartialAndCombinedStoredLinksCreateNoAdditionalExpenseOrCashMovement()
    {
        using var f=new LedgerFixture();var first=Trip(f,6000);var second=Trip(f,4000);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var other=f.Post(f.Draft(FinancialKind.Expense,2000));
        var book=new TripPayments(f.Business.Store);Link(f,book,Draft(f,first,payment,4000));Link(f,book,Draft(f,second,payment,4000));Link(f,book,Draft(f,first,other,2000));
        var history=book.History(f.Business.Owner);Assert.Equal(3,history.Count);Assert.Equal(2,book.History(f.Business.Owner,first.Id).Count);
        Assert.Equal(12000,f.Balance("5010"));Assert.Equal(-12000,f.Balance("1010"));Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
        Assert.All(history,h=>{Assert.Equal(f.Business.Owner.OwnerId,h.ApprovedBy);Assert.NotEmpty(h.AtUtc);Assert.Equal("Requested",h.Snapshot.Trip.ReimbursementStatus);});
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,Draft(f,second,payment,1)));
        Assert.Equal(history.Select(h=>h.SnapshotHash),new TripPayments(new BusinessStore(f.Business.Files.DatabasePath)).History(f.Business.Owner).Select(h=>h.SnapshotHash));
    }

    [Fact]
    public void CompetingAndChangedRequestsCannotSpendTheSameApprovedPaymentTwice()
    {
        using var f=new LedgerFixture();var first=Trip(f);var second=Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);
        var one=Draft(f,first,payment,6000);var two=Draft(f,second,payment,6000);var a=book.Preview(f.Business.Owner,one);var b=book.Preview(f.Business.Owner,two);string request=Guid.NewGuid().ToString();
        var saved=book.Approve(f.Business.Owner,one,a.Watermark,request,BusinessFixture.Phrase);
        Assert.Equal(saved.Id,book.Approve(f.Business.Owner,one,a.Watermark,request,BusinessFixture.Phrase).Id);
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,one with{Cents=5000},a.Watermark,request,BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,two,b.Watermark,Guid.NewGuid().ToString(),BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,two));Assert.Single(book.History(f.Business.Owner));
    }

    [Fact]
    public void SourceReturnAndCancellationRequireEnoughDatedLinkReleasesInPreviewAndApproval()
    {
        using var f=new LedgerFixture();var trip=Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);var link=Link(f,book,Draft(f,trip,payment,8000));
        var refund=f.Draft(FinancialKind.SupplierRefund,3000) with{OriginalId=payment.Id,Date=f.Date.AddDays(3)};
        Assert.Throws<ArgumentException>(()=>f.Book.Preview(f.Business.Owner,refund));Assert.Throws<ArgumentException>(()=>f.Post(refund));
        Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Equal(-10000,f.Balance("1010"));
        Release(f,book,link,1000,3);var returned=f.Post(refund);Assert.Equal(-7000,f.Balance("1010"));Assert.Equal(7000,f.Balance("5010"));
        f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=returned.Id,Date=f.Date.AddDays(4)});
        var reverse=f.Draft(FinancialKind.Reversal,0) with{OriginalId=payment.Id,Date=f.Date.AddDays(4)};
        Assert.Throws<ArgumentException>(()=>f.Book.Preview(f.Business.Owner,reverse));Assert.Throws<ArgumentException>(()=>f.Post(reverse));
        Release(f,book,link,7000,5);
        Assert.Throws<ArgumentException>(()=>f.Post(reverse));
        f.Post(reverse with{Date=f.Date.AddDays(5)});Assert.Equal(0,f.Balance("1010"));Assert.Equal(0,f.Balance("5010"));
        Assert.Equal(10000,book.History(f.Business.Owner).Single().Snapshot.Payment.Source.AmountCents);
    }

    [Fact]
    public void ActiveClaimBasisIsProtectedAndAReleasedClaimCanBeReviewedWithoutRewritingHistory()
    {
        using var f=new LedgerFixture();var trip=Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);var link=Link(f,book,Draft(f,trip,payment,5000));
        var operations=new OperationalResources(f.Business.Store);trip=operations.SaveTrip(f.Business.Owner,trip with{ActualMiles=18.40m,EvidenceId="Fictional added receipt evidence"});
        Assert.Contains("Release",Assert.Throws<ArgumentException>(()=>operations.SaveTrip(f.Business.Owner,trip with{ActualMiles=30.1m})).Message);
        Assert.Throws<ArgumentException>(()=>operations.SaveTrip(f.Business.Owner,trip with{RequestedReimbursementCents=7000}));
        Release(f,book,link,5000);trip=operations.SaveTrip(f.Business.Owner,trip with{ActualMiles=30.1m,RequestedReimbursementCents=7000});
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,Draft(f,trip,payment,7000)));
        var revised=Link(f,book,Draft(f,trip,payment,7000,3));Assert.NotEqual(link.Snapshot.ClaimBasisHash,revised.Snapshot.ClaimBasisHash);
        Assert.Equal(10000,book.History(f.Business.Owner).Single(h=>h.Id==link.Id).Snapshot.Trip.RequestedReimbursementCents);
        Assert.Equal(18.4m,link.Snapshot.Trip.ActualMiles);Assert.Equal(30.1m,revised.Snapshot.Trip.ActualMiles);
    }

    [Fact]
    public void OwnerReimbursementMustPayItsActualOwnerAndDoesNotExpenseTheClaimAgain()
    {
        using var f=new LedgerFixture();var trip=Trip(f);f.Post(f.Draft(FinancialKind.OwnerPaidExpense,10000));var reimbursement=f.Post(f.Draft(FinancialKind.OwnerReimbursement,10000));
        var book=new TripPayments(f.Business.Store);Link(f,book,Draft(f,trip,reimbursement,10000));Assert.Equal(10000,f.Balance("5010"));Assert.Equal(0,f.Balance("2100"));
        var artists=new BookingBook(f.Business.Store);var artist=artists.SaveArtist(f.Business.Owner,new(){Name="Fictional other driver"});
        var other=new OperationalResources(f.Business.Store).SaveTrip(f.Business.Owner,TripReviewTests.Trip(f) with{DriverOwnerId="",DriverArtistId=artist.Id,RequestedReimbursementCents=10000,ReimbursementStatus="Requested"});
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,Draft(f,other,reimbursement,1)));
        var paid=f.Post(f.Draft(FinancialKind.Expense,1000));Link(f,book,Draft(f,other,paid,1000));
    }

    [Fact]
    public void UnreviewedAmountsUnpaidSourcesMissingEvidenceClosedDatesAndForeignSessionsCannotLink()
    {
        using var f=new LedgerFixture();var operations=new OperationalResources(f.Business.Store);var trip=operations.SaveTrip(f.Business.Owner,TripReviewTests.Trip(f));
        var expense=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);var draft=Draft(f,trip,expense,1000);
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft));
        trip=operations.SaveTrip(f.Business.Owner,trip with{ReimbursementStatus="Requested"});Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft));
        trip=operations.SaveTrip(f.Business.Owner,trip with{RequestedReimbursementCents=10000});
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{PayeeReview=""}));
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,10000));Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft with{PaymentId=receipt.Id}));
        var preview=book.Preview(f.Business.Owner,draft);using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>book.Preview(foreign.Owner,draft));
        Assert.Throws<UnauthorizedAccessException>(()=>book.Approve(f.Business.Owner,draft,preview.Watermark,Guid.NewGuid().ToString(),"Fictional incorrect phrase"));
        f.Business.Files.Execute($"UPDATE BookSettings SET ClosedThrough='{draft.Date:yyyy-MM-dd}';");Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft));Assert.Empty(book.History(f.Business.Owner));
    }

    [Theory]
    [InlineData("trip-link")]
    [InlineData("audit")]
    public void InterruptedLinkRollsBackAndTheSameReviewedRequestRetriesOnce(string stage)
    {
        using var f=new LedgerFixture();var trip=Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);var draft=Draft(f,trip,payment,5000);
        var preview=book.Preview(f.Business.Owner,draft);string request=Guid.NewGuid().ToString();book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected trip link failure");};
        Assert.Throws<IOException>(()=>book.Approve(f.Business.Owner,draft,preview.Watermark,request,BusinessFixture.Phrase));Assert.Empty(book.History(f.Business.Owner));
        Assert.Equal(preview.Watermark,book.Preview(f.Business.Owner,draft).Watermark);book.FailurePoint=null;
        var saved=book.Approve(f.Business.Owner,draft,preview.Watermark,request,BusinessFixture.Phrase);Assert.Equal(saved.Id,book.Approve(f.Business.Owner,draft,preview.Watermark,request,BusinessFixture.Phrase).Id);
    }

    [Theory]
    [InlineData("trip-release")]
    [InlineData("audit")]
    public void InterruptedReleaseRollsBackAndRetryDoesNotRestoreThePaymentTwice(string stage)
    {
        using var f=new LedgerFixture();var trip=Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));var book=new TripPayments(f.Business.Store);var linked=Link(f,book,Draft(f,trip,payment,5000));
        var draft=new TripPaymentReleaseDraft(linked.Id,f.Date.AddDays(2),5000,"Fictional wrong trip corrected","Fictional actual payment evidence");var preview=book.PreviewRelease(f.Business.Owner,draft);string request=Guid.NewGuid().ToString();
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected trip release failure");};Assert.Throws<IOException>(()=>book.Release(f.Business.Owner,draft,preview.Watermark,request,BusinessFixture.Phrase));
        Assert.Empty(book.ReleaseHistory(f.Business.Owner));book.FailurePoint=null;var saved=book.Release(f.Business.Owner,draft,preview.Watermark,request,BusinessFixture.Phrase);
        Assert.Equal(saved.Id,book.Release(f.Business.Owner,draft,preview.Watermark,request,BusinessFixture.Phrase).Id);Assert.Single(book.ReleaseHistory(f.Business.Owner));
        Assert.Throws<ArgumentException>(()=>book.PreviewRelease(f.Business.Owner,draft with{Date=f.Date.AddDays(3),Cents=1}));Assert.Equal(-10000,f.Balance("1010"));Assert.Single(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void OlderStoreUpgradeRetainsTripReviewsAndImmutableHashesProtectTheirLinks()
    {
        using var f=new LedgerFixture();var trip=Trip(f);var payment=f.Post(f.Draft(FinancialKind.Expense,10000));f.Business.Files.PrepareBankingVersion(27);
        Assert.Equal(BusinessSchema.Version,SarahBeauty_Desktop.Storage.LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);Assert.Single(f.Business.Files.Snapshots());
        var book=new TripPayments(f.Business.Store);var saved=Link(f,book,Draft(f,trip,payment,5000));Assert.Equal(trip.History.Single(),saved.Snapshot.Trip.History.Single());
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("UPDATE TripPaymentLinks SET Cents=1;"));Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM TripPaymentLinks;"));
        Assert.Throws<InvalidOperationException>(()=>f.Business.Files.PrepareBankingVersion(27));
        f.Business.Files.Execute("DROP TRIGGER TripPaymentLinks_NoUpdate; UPDATE TripPaymentLinks SET SnapshotJson='{}';");Assert.Throws<InvalidDataException>(()=>book.History(f.Business.Owner));
    }
}
