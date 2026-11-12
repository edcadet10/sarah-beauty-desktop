using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SarahBeauty.Storage.Tests;

public sealed class ModeStoreTests
{
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public void InvoicesCreditsReceiptsProfilesAndCapturedGenerationRetainTheStoreMode(BusinessMode mode)
    {
        using var files=new TestStore();LocalDatabase.InitializeDatabaseAt(files.DatabasePath,mode);var store=new BusinessStore(files.DatabasePath,mode);var owner=new OwnerAccess(store).Setup("Fictional mode beauty","Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase);PrepareLiveSetup(store,owner,mode);var quote=IssueQuote(store,owner);string customer=new CustomerBook(store).Search(owner).Single().Id;new QuoteBook(store).Accept(owner,quote.Id,quote.Snapshot.IssueDate,"Fictional mode acceptance");
        var invoices=new InvoiceBook(store);var draft=invoices.SaveDraft(owner,new(){Name="Fictional mode invoice",QuoteVersionId=quote.Id,PayerCustomerId=customer,IssueDate=quote.Snapshot.IssueDate,Portions=quote.Snapshot.Lines.Select(l=>new InvoicePortion(l.ChargeId,l.Quantity)).ToArray()});var invoice=invoices.Issue(owner,draft.Id,draft.Revision,BusinessFixture.Phrase);var credits=new CreditNoteBook(store);var creditDraft=credits.SaveDraft(owner,new(){Name="Fictional mode reduction",InvoiceVersionId=invoice.Id,IssueDate=invoice.Snapshot.IssueDate,Portions=[new(invoice.Snapshot.Lines[0].ChargeId,.2m)]});var credit=credits.Issue(owner,creditDraft.Id,creditDraft.Revision,BusinessFixture.Phrase);
        var chart=new ChartOfAccounts(store);chart.Initialize(owner);var accounts=chart.List(owner).ToDictionary(a=>a.Code);var book=new FinanceBook(store);var paymentDraft=book.SaveDraft(owner,new(){Kind=FinancialKind.Receipt,Name="Fictional mode receipt",Date=invoice.Snapshot.IssueDate,AmountCents=2000,TaxResolved=true,CashAccountId=accounts["1010"].Id,CounterAccountId=accounts["4000"].Id,CustomerId=customer});var payment=book.Approve(owner,paymentDraft.Id,paymentDraft.Revision,BusinessFixture.Phrase);var receipts=new ReceiptBook(store);var receiptDraft=receipts.SaveDraft(owner,new(){Name="Fictional mode acknowledgment",PaymentId=payment.Id,IssueDate=invoice.Snapshot.IssueDate});var receipt=receipts.Issue(owner,receiptDraft.Id,receiptDraft.Revision,BusinessFixture.Phrase);
        var profiles=new BusinessProfiles(store);var current=profiles.Current(owner);var profile=profiles.Confirm(owner,new(current?.Revision??0,current?.Profile??new(){DisplayName="Fictional mode beauty"},"Fictional mode profile review"),BusinessFixture.Phrase,Guid.NewGuid().ToString());string expected=BusinessModes.StoredName(mode);Assert.All(new[]{quote,invoice,credit,receipt},v=>Assert.Equal(expected,v.Snapshot.Mode));Assert.Equal(expected,profile.Mode);Assert.Equal(mode,owner.Mode);Assert.Equal(4000,receipt.Snapshot.Receipt!.CustomerInvoiceBalanceCents);
        using(var pdf=PdfDocument.Open(new DocumentArtifacts(store).Render(owner,quote.Id))){string text=string.Join("\n",pdf.GetPages().Select(p=>ContentOrderTextExtractor.GetText(p)));Assert.Contains(expected+" • Fictional mode beauty",text);Assert.DoesNotContain((mode==BusinessMode.Live?"TEST":"LIVE")+" • Fictional mode beauty",text);}
        using var snapshot=new ReadableExportSnapshots(store).Capture(owner);var generation=ReadableExportGeneration.Build(snapshot,DocumentDecisionExportContractTests.Selection);Assert.Equal(expected,snapshot.Source.Mode);Assert.Equal(expected,generation.Manifest.Source.Mode);Assert.Equal(165,generation.Tables.Count);Assert.Equal(2000L,TestStore.Scalar(store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(2000L,TestStore.Scalar(store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));
    }
    [Fact]
    public void CopiedIdsAndCredentialsCannotReuseSessionsAcrossModeContexts()
    {
        using var testFiles=new TestStore();using var liveFiles=new TestStore();var test=new BusinessStore(testFiles.DatabasePath);var owner=new OwnerAccess(test).Setup("Fictional copied-identity probe","Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase);DatabaseBackup.CreateVerifiedSnapshotAt(testFiles.DatabasePath,liveFiles.DatabasePath);liveFiles.Execute("UPDATE AppMetadata SET Value='LIVE' WHERE Key='StoreMode';");var live=new BusinessStore(liveFiles.DatabasePath,BusinessMode.Live);Assert.Equal(test.StoreId,live.StoreId);Assert.Equal(test.InstanceId,live.InstanceId);Assert.Throws<UnauthorizedAccessException>(()=>live.BusinessName(owner));var other=new OwnerAccess(live).SignIn(owner.OwnerId,BusinessFixture.Phrase);Assert.Equal(BusinessMode.Live,other.Mode);Assert.Throws<UnauthorizedAccessException>(()=>test.BusinessName(other));Assert.True(owner.IsActive);Assert.True(other.IsActive);Assert.Equal("TEST",TestStore.Scalar(testFiles.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));
    }
    [Fact]
    public void ExistingOnlyModeContextDoesNotCreateMissingDataAndPublicConstructorRemainsTestOnly()
    {
        using var files=new TestStore();Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(()=>new BusinessStore(files.DatabasePath,BusinessMode.Live));Assert.False(File.Exists(files.DatabasePath));LocalDatabase.InitializeDatabaseAt(files.DatabasePath,BusinessMode.Live);Assert.Throws<InvalidOperationException>(()=>new BusinessStore(files.DatabasePath));Assert.Equal("LIVE",TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));
    }
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public void AnOpenedContextRejectsALaterMetadataModeChange(BusinessMode mode)
    {
        using var files=new TestStore();LocalDatabase.InitializeDatabaseAt(files.DatabasePath,mode);var store=new BusinessStore(files.DatabasePath,mode);var owner=new OwnerAccess(store).Setup("Fictional guarded mode","Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase);files.Execute("UPDATE AppMetadata SET Value='"+(mode==BusinessMode.Test?"LIVE":"TEST")+"' WHERE Key='StoreMode';");Assert.Throws<InvalidDataException>(()=>store.BusinessName(owner));Assert.True(owner.IsActive);Assert.Equal(1L,TestStore.Scalar(files.DatabasePath,"SELECT Watermark FROM Business;"));
    }
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public void IssuedQuoteUsesTheExplicitStoreMode(BusinessMode mode)
    {
        using var files=new TestStore();LocalDatabase.InitializeDatabaseAt(files.DatabasePath,mode);var store=new BusinessStore(files.DatabasePath,mode);var owner=new OwnerAccess(store).Setup("Fictional mode beauty","Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase);PrepareLiveSetup(store,owner,mode);var quote=IssueQuote(store,owner);Assert.Equal(BusinessModes.StoredName(mode),quote.Snapshot.Mode);
    }
    private static void PrepareLiveSetup(BusinessStore store,OwnerSession owner,BusinessMode mode)
    {
        if(mode!=BusinessMode.Live)return;new BusinessProfiles(store).Confirm(owner,new(0,LiveSetupPolicyTests.ReviewedProfile() with{DisplayName=store.BusinessName(owner),BooksStartDate=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)},"Fictional reviewed mode fixture"),BusinessFixture.Phrase,Guid.NewGuid().ToString());
    }
    internal static DocumentVersion IssueQuote(BusinessStore store,OwnerSession owner)
    {
        var customer=new CustomerBook(store).Save(owner,new(){FullName="Fictional mode payer"});var booking=BookingRules.AddCharge(new Booking{Name="Fictional mode booking",HasTrial=false,TrialCents=0,CustomDepositCents=0,PrimaryCustomerId=customer.Id,PayerMode="Single",SinglePayerCustomerId=customer.Id,UnpaidResponsibility="Fictional explicit payer agreement"},"Fictional service","Custom",1,5000);booking=new BookingBook(store).Save(owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});var quotes=new QuoteBook(store);var draft=quotes.SaveDraft(owner,quotes.FromBooking(owner,booking));return quotes.Issue(owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }
}
