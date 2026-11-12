using System.Text.Json;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableCreditTablesTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];
    private static PaymentApplication Apply(LedgerFixture f,DocumentVersion invoice,string payment="",string credit="",string reverse="",DateOnly? date=null)
    {
        var book=new PaymentApplications(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new PaymentApplicationDraft{Name="Fictional application source",PaymentId=payment,CreditId=credit,ReversesId=reverse,InvoiceVersionId=invoice.Id,Cents=4000,Date=date??f.Date});return Assert.Single(book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
    }
    [Fact]
    public void FundedCreditApplicationAndReturnPreserveSeparateSourcesWithoutAdditionalCashOrPrivateReasons()
    {
        using var f=new LedgerFixture();const string secret="Fictional-private-credit-reason";var invoice=ApplicationTests.Invoice(f,8000);var payment=f.Post(f.Draft(FinancialKind.Receipt,15000));var book=new CreditBook(f.Business.Store);var draft=book.SaveDraft(f.Business.Owner,new CreditDraft{Name=secret,PaymentId=payment.Id,Cents=10000,Date=f.Date});var credit=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);var application=Apply(f,invoice,credit:credit.Id);var returned=book.SaveDraft(f.Business.Owner,new CreditDraft{Name=secret,Action=CreditAction.ReturnToReceipt,CreditId=credit.Id,Cents=2000,Date=f.Date.AddDays(1)});book.Approve(f.Business.Owner,returned.Id,returned.Revision,BusinessFixture.Phrase);
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableCreditTables.Build(source,new());Assert.Equal(5,tables.Length);Assert.DoesNotContain(secret,JsonSerializer.Serialize(tables));var credits=tables.Single(t=>t.Name=="Customer credits");Assert.Equal("10000",Cell(credits,credits.Rows.Single(),"IssuedCents"));var funding=tables.Single(t=>t.Name=="Credit funding");Assert.Equal(2,funding.Rows.Count);Assert.Contains(funding.Rows,r=>Cell(funding,r,"Kind")=="Reserve" && Cell(funding,r,"Cents")=="10000");Assert.Contains(funding.Rows,r=>Cell(funding,r,"Kind")=="Release" && Cell(funding,r,"Cents")=="2000");var applications=tables.Single(t=>t.Name=="Payment applications");var row=applications.Rows.Single(r=>r[0]==application.Id);Assert.Equal(credit.Id,Cell(applications,row,"CreditId"));Assert.Equal("",Cell(applications,row,"PaymentId"));Assert.Equal("4000",Cell(applications,row,"Cents"));Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));Assert.Equal(4000,book.Available(f.Business.Owner,credit.Id,f.Date.AddDays(1)).Cents);
    }
    [Fact]
    public void DatedUnapplicationRetainsOriginalApplicationAndBothApprovalRecords()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,8000);var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));var original=Apply(f,invoice,payment:payment.Id);var reversed=Apply(f,invoice,reverse:original.Id,date:f.Date.AddDays(1));using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableCreditTables.Build(source,new());var applications=tables.Single(t=>t.Name=="Payment applications");Assert.Equal(2,applications.Rows.Count);var row=applications.Rows.Single(r=>r[0]==reversed.Id);Assert.Equal(original.Id,Cell(applications,row,"ReversesApplicationId"));Assert.Equal("2027-01-11",Cell(applications,row,"Date"));Assert.Equal("4000",Cell(applications,row,"Cents"));Assert.Equal(2,tables.Single(t=>t.Name=="Application approvals").Rows.Count);Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }
    [Fact]
    public void AnUnapprovedCreditDraftKeepsUnknownDateAndCreatesNoCreditOrReservation()
    {
        using var f=new LedgerFixture();var payment=f.Post(f.Draft(FinancialKind.Receipt,10000));var book=new CreditBook(f.Business.Store);book.SaveDraft(f.Business.Owner,new CreditDraft{Name="Fictional private draft explanation",PaymentId=payment.Id,Cents=0});using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableCreditTables.Build(source,new());Assert.Empty(tables.Single(t=>t.Name=="Customer credits").Rows);Assert.Empty(tables.Single(t=>t.Name=="Credit funding").Rows);var drafts=tables.Single(t=>t.Name=="Credit draft records");Assert.Equal("Draft",Cell(drafts,drafts.Rows.Single(),"Status"));Assert.Equal("",Cell(drafts,drafts.Rows.Single(),"Date"));Assert.DoesNotContain("Fictional private",JsonSerializer.Serialize(tables));
    }
}
