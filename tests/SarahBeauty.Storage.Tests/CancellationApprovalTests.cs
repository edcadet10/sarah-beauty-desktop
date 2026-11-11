using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class CancellationApprovalTests
{
    private static (CancellationCases Book,CancellationDraft Draft) Mixed(LedgerFixture f)
    {
        var invoice=ApplicationTests.Invoice(f,10000,0.1m);var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        var application=CancellationSourceTests.Apply(f,receipt.Id,invoice,11000);var book=new CancellationCases(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,CancellationPreviewTests.Draft(f,invoice) with{Unapplications=[new(application.Id,11000)],
            Reductions=[new(invoice.Id,[new(invoice.Snapshot.Lines[0].ChargeId,1)])],Values=[CancellationPreviewTests.Value(f,receipt.Id,retain:2200,refund:3300,tax:300,transfer:5500)]});
        return (book,draft);
    }

    [Fact]
    public void MixedApprovalCommitsOnceAndRetainsOriginalTermsAndBalancesAcrossReload()
    {
        using var f=new LedgerFixture();var (book,draft)=Mixed(f);var preview=book.Preview(f.Business.Owner,draft);
        var saved=book.Approve(f.Business.Owner,preview,BusinessFixture.Phrase);
        Assert.Equal(saved.Id,book.Approve(f.Business.Owner,preview,BusinessFixture.Phrase).Id);
        Assert.Equal(f.Business.Owner.OwnerId,saved.ApprovedBy);Assert.NotEmpty(saved.AtUtc);Assert.Single(book.History(f.Business.Owner,draft.BookingId));
        Assert.Equal(7700,f.Balance("1010"));Assert.Equal(-7000,f.Balance("4000"));Assert.Equal(-700,f.Balance("2000"));
        Assert.Equal(new ValueBalance(5500,500),saved.Snapshot.After.Credits.Single().Available);Assert.Equal(new ValueBalance(2200,200),saved.Snapshot.After.Payments.Single().Available);
        Assert.Equal(0,saved.Snapshot.After.Dues.Sum(d=>d.Cents));Assert.Equal("Cancelled",saved.Snapshot.After.BookingStatus);
        var bookingBook=new BookingBook(f.Business.Store);var booking=bookingBook.List(f.Business.Owner).Single();
        bookingBook.Save(f.Business.Owner,booking with{Name="Fictional later booking label"});
        var loaded=new CancellationCases(new BusinessStore(f.Business.Files.DatabasePath)).Open(f.Business.Owner,saved.Id);
        Assert.Equal(saved.SnapshotHash,loaded.SnapshotHash);Assert.NotEqual("Fictional later booking label",loaded.Snapshot.Before.BookingName);
        Assert.Equal(saved.Snapshot.Decision.AgreementReview,loaded.Snapshot.Decision.AgreementReview);
        Assert.Equal(saved.Snapshot.Before.Documents.Single(d=>d.Id==draft.TermsVersionId).Hash,loaded.Snapshot.Before.Documents.Single(d=>d.Id==draft.TermsVersionId).Hash);
        Assert.Equal("Approved",book.Drafts(f.Business.Owner).Single().Status);
        Assert.Throws<ArgumentException>(()=>book.SaveDraft(f.Business.Owner,book.Drafts(f.Business.Owner).Single() with{Name="Fictional attempt to overwrite approval"}));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,preview with{Decision=draft with{Name="Fictional changed retry"}},BusinessFixture.Phrase));
    }

    [Theory]
    [InlineData("unapplications")]
    [InlineData("credit-notes")]
    [InlineData("refund")]
    [InlineData("credit")]
    [InlineData("value-decisions")]
    [InlineData("booking-status")]
    [InlineData("case-snapshot")]
    [InlineData("case-draft")]
    [InlineData("case-audit")]
    public void InterruptedApprovalRollsBackEveryEffectAndItsOriginalReviewCanRetry(string stage)
    {
        using var f=new LedgerFixture();var (book,draft)=Mixed(f);var preview=book.Preview(f.Business.Owner,draft);
        book.FailurePoint=p=>{if(p==stage)throw new IOException("Injected cancellation approval failure");};
        Assert.Throws<IOException>(()=>book.Approve(f.Business.Owner,preview,BusinessFixture.Phrase));
        Assert.Empty(book.History(f.Business.Owner));Assert.Equal("Draft",book.Drafts(f.Business.Owner).Single().Status);
        var current=new CancellationSources(f.Business.Store).Load(f.Business.Owner,draft.BookingId,draft.Date!.Value);
        Assert.Equal(preview.Before.Watermark,current.Watermark);Assert.Equal(preview.Before.BookingRevision,current.BookingRevision);Assert.Equal("Awaiting deposit",current.BookingStatus);
        Assert.Equal(11000,current.Applications.Single().Remaining.Cents);Assert.Empty(current.Credits);Assert.Single(f.Book.Posted(f.Business.Owner));Assert.Equal(11000,f.Balance("1010"));
        Assert.Equal(0L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM CreditNoteHeaders;"));
        book.FailurePoint=null;var saved=book.Approve(f.Business.Owner,preview,BusinessFixture.Phrase);
        Assert.Single(book.History(f.Business.Owner));Assert.Equal("C-000001",saved.Snapshot.After.Documents.Single(d=>d.Snapshot.Kind==DocumentKind.CreditNote).Snapshot.Number);
    }

    [Fact]
    public void ChangedSourcesUnsavedDecisionsAndInvalidOwnerConfirmationCannotApprove()
    {
        using var f=new LedgerFixture();var (book,draft)=Mixed(f);var old=book.Preview(f.Business.Owner,draft);
        f.Post(f.Draft(FinancialKind.Expense,1));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,old,BusinessFixture.Phrase));
        var fresh=book.Preview(f.Business.Owner,draft);var changed=book.SaveDraft(f.Business.Owner,draft with{Name="Fictional revised cancellation agreement"});
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,fresh,BusinessFixture.Phrase));
        var unsaved=book.Preview(f.Business.Owner,changed with{Name="Fictional unsaved cancellation agreement"});
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,unsaved,BusinessFixture.Phrase));
        var latest=book.Preview(f.Business.Owner,changed);
        Assert.Throws<UnauthorizedAccessException>(()=>book.Approve(f.Business.Owner,latest,"Fictional wrong owner phrase"));
        using var foreign=new BusinessFixture();Assert.Throws<UnauthorizedAccessException>(()=>book.Approve(foreign.Owner,latest,BusinessFixture.Phrase));
        Assert.Empty(book.History(f.Business.Owner));Assert.Empty(new CreditBook(f.Business.Store).Credits(f.Business.Owner));
    }

    [Fact]
    public void CompetingDecisionsCannotRefundTheSameSourceTwice()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);var receipt=f.Post(f.Draft(FinancialKind.Receipt,12000) with{BookingId=invoice.BookingId});
        var book=new CancellationCases(f.Business.Store);var value=CancellationPreviewTests.Draft(f,invoice) with{Values=[CancellationPreviewTests.Value(f,receipt.Id,refund:9000)]};
        var first=book.SaveDraft(f.Business.Owner,value);var second=book.SaveDraft(f.Business.Owner,value);
        var one=book.Preview(f.Business.Owner,first);var two=book.Preview(f.Business.Owner,second);
        book.Approve(f.Business.Owner,one,BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,two,BusinessFixture.Phrase));
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,second));
        Assert.Single(book.History(f.Business.Owner));Assert.Equal(3000,f.Balance("1010"));Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void StatusOnlyDecisionDoesNotInventARefundFeeOrCancelledInvoiceCharge()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f);var book=new CancellationCases(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,CancellationPreviewTests.Draft(f,invoice));var saved=book.Approve(f.Business.Owner,book.Preview(f.Business.Owner,draft),BusinessFixture.Phrase);
        Assert.Empty(saved.Snapshot.Actions);Assert.Empty(saved.Snapshot.Journal);Assert.Equal(12000,saved.Snapshot.After.Dues.Sum(d=>d.Cents));
        Assert.Empty(f.Book.Posted(f.Business.Owner));Assert.Equal("Cancelled",new BookingBook(f.Business.Store).List(f.Business.Owner).Single().Status);
        Assert.False(new BookingBook(f.Business.Store).List(f.Business.Owner).Single().HasTrial);
    }

    [Fact]
    public void VersionTwentySixUpgradeRetainsSourcesAndProtectsCaseSnapshotsFromTampering()
    {
        using var f=new LedgerFixture();var (book,draft)=Mixed(f);f.Business.Files.PrepareBankingVersion(26);
        Assert.Equal(BusinessSchema.Version,SarahBeauty_Desktop.Storage.LocalDatabase.InitializeTestDatabaseAt(f.Business.Files.DatabasePath).SchemaVersion);
        Assert.Single(f.Business.Files.Snapshots());
        var saved=book.Approve(f.Business.Owner,book.Preview(f.Business.Owner,draft),BusinessFixture.Phrase);
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("UPDATE CancellationDecisions SET SnapshotJson='{}';"));
        Assert.Throws<SqliteException>(()=>f.Business.Files.Execute("DELETE FROM CancellationDecisions;"));
        f.Business.Files.Execute("DROP TRIGGER CancellationDecisions_NoUpdate; UPDATE CancellationDecisions SET SnapshotJson='{}';");
        Assert.Throws<InvalidDataException>(()=>book.Open(f.Business.Owner,saved.Id));
    }
}
