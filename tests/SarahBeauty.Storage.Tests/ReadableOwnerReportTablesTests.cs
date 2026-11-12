using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableOwnerReportTablesTests
{
    private const string Private="Fictional-private-owner-report-evidence";
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void ActualOwnerCategoriesStaySeparateAndLegacyLoanRemainsUnattributed()
    {
        using var f=new LedgerFixture();string owner=f.Business.Owner.OwnerId;
        new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=owner});
        var draft=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.LoanProceeds,10000));
        f.Business.Store.Write(f.Business.Owner,"Fictional legacy owner table fixture",draft.Id,db=>PostingWriter.Save(db,f.Business.Owner,draft with{LoanLender=null},PostingRules.Build(draft,f.Accounts.Values.ToList()),null));
        f.Post(f.Draft(FinancialKind.LoanPayment,3000) with{PrincipalCents=3000});
        f.Post(f.Draft(FinancialKind.Contribution,10000));f.Post(f.Draft(FinancialKind.Withdrawal,2000));
        f.Post(f.Draft(FinancialKind.OwnerPaidExpense,4000) with{EvidenceId=Private});f.Post(f.Draft(FinancialKind.OwnerReimbursement,1500));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);f.Business.Owner.Lock();
        var tables=ReadableOwnerReportTables.Build(source,new(),new(2027,1,1),new(2027,1,31));
        Assert.Equal(5,tables.Length);Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var positions=tables.Single(t=>t.Name=="Report owner positions");var row=positions.Rows.Single(r=>Cell(positions,r,"OwnerId")==owner);
        Assert.Equal("10000",Cell(positions,row,"ContributionsCents"));Assert.Equal("2000",Cell(positions,row,"DrawsCents"));
        Assert.Equal("1500",Cell(positions,row,"ReimbursementsCents"));Assert.Equal("3000",Cell(positions,row,"LoanPrincipalPaidCents"));
        Assert.Equal("0",Cell(positions,row,"AttributedLoanPrincipalCents"));
        var loans=tables.Single(t=>t.Name=="Report owner loan attribution");var loan=Assert.Single(loans.Rows);
        Assert.Equal("",Cell(loans,loan,"AttributedOwnerId"));Assert.Equal("7000",Cell(loans,loan,"PrincipalCents"));
        Assert.Equal("Historical lender attribution unconfirmed",Cell(loans,loan,"Attribution"));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
    }

    [Fact]
    public void RetainedOwnerFiguresStayOriginalBesideLaterContributionAndPayable()
    {
        using var f=new LedgerFixture();BookPeriodTests.Ready(f);string owner=f.Business.Owner.OwnerId;
        var periods=new BookPeriods(f.Business.Store);var closed=BookPeriodTests.Close(f,periods);
        periods.Reopen(f.Business.Owner,closed.Id,Private,Guid.NewGuid().ToString(),BusinessFixture.Phrase);
        f.Post(f.Draft(FinancialKind.Contribution,5000) with{Date=new(2027,2,1)});
        f.Post(f.Draft(FinancialKind.OwnerPaidExpense,4000) with{Date=new(2027,2,1),EvidenceId=Private});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableOwnerReportTables.Build(source,new(),new(2027,1,1),new(2027,2,28));
        Assert.DoesNotContain(Private,JsonSerializer.Serialize(tables));
        var positions=tables.Single(t=>t.Name=="Report owner positions");
        var old=positions.Rows.Single(r=>r[0]==closed.Id&&Cell(positions,r,"OwnerId")==owner);
        var current=positions.Rows.Single(r=>r[0]==source.Source.GenerationId&&Cell(positions,r,"OwnerId")==owner);
        Assert.Equal("0",Cell(positions,old,"ContributionsCents"));Assert.Equal("0",Cell(positions,old,"PayableAddedCents"));
        Assert.Equal("5000",Cell(positions,current,"ContributionsCents"));Assert.Equal("4000",Cell(positions,current,"PayableAddedCents"));
        var accounts=tables.Single(t=>t.Name=="Report owner account balances");
        var payable=accounts.Rows.Single(r=>r[0]==source.Source.GenerationId&&Cell(accounts,r,"OwnerId")==owner&&Cell(accounts,r,"AccountCode")=="2100");
        Assert.Equal("-4000",Cell(accounts,payable,"ClosingDebitLessCreditCents"));
        var loans=tables.Single(t=>t.Name=="Report owner loan attribution");Assert.All(loans.Rows,r=>Assert.Equal("",Cell(loans,r,"PrincipalCents")));
        Assert.Equal(3,f.Book.Posted(f.Business.Owner).Count);
    }
}
