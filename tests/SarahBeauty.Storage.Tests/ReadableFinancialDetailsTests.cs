using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableFinancialDetailsTests
{
    private static string Cell(ReadableExportTable table,IReadOnlyList<string> row,string column)=>row[table.Columns.ToList().IndexOf(column)];

    [Fact]
    public void ApprovedLoanAndReversalKeepOriginalLenderAfterRegisterRename()
    {
        using var f=new LedgerFixture();
        var resources=new BusinessResources(f.Business.Store);
        var loan=resources.SaveLoan(f.Business.Owner,f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId});
        var posted=f.Post(f.Draft(FinancialKind.LoanProceeds,10000));
        resources.SaveLoan(f.Business.Owner,loan with{Name="Fictional changed current lender"});
        var undo=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=posted.Id});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var table=ReadableFinancialDetails.Build(source,new()).Single(t=>t.Name=="Financial frozen lender facts");
        var frozen=table.Rows.Where(r=>r[0]=="PostedMovement").ToArray();
        Assert.Equal(2,frozen.Length);
        Assert.Contains(frozen,r=>r[1]==posted.Id);
        Assert.Contains(frozen,r=>r[1]==undo.Id);
        Assert.All(frozen,r=>Assert.Equal(loan.Name,Cell(table,r,"LenderName")));
        Assert.All(frozen,r=>Assert.Equal("Alex",Cell(table,r,"OwnerLenderName")));
        Assert.Equal(0,f.Balance("2300"));
    }

    [Fact]
    public void MixedRefundTaxRetainsAllThreeExactAllocationsAndNoEvidenceInput()
    {
        using var f=new LedgerFixture();
        var receipt=f.Post(f.Draft(FinancialKind.Receipt,11000) with{TaxCents=1000});
        f.Post(f.Draft(FinancialKind.TaxRemittance,800));
        const string evidence="Fictional-private-financial-allocation-evidence";
        var refund=f.Post(f.Draft(FinancialKind.Refund,11000) with{OriginalId=receipt.Id,TaxCents=1000,TaxRefundAllocation=new(200,300,500),EvidenceId=evidence});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableFinancialDetails.Build(source,new());
        Assert.DoesNotContain(evidence,JsonSerializer.Serialize(tables));
        var table=tables.Single(t=>t.Name=="Financial refund tax allocations");
        Assert.Equal(2,table.Rows.Count);
        var row=table.Rows.Single(r=>r[0]=="PostedMovement");
        Assert.Equal(refund.Id,row[1]);
        Assert.Equal("200",Cell(table,row,"LiabilityCents"));
        Assert.Equal("300",Cell(table,row,"RecoverableCents"));
        Assert.Equal("500",Cell(table,row,"NonrecoverableCents"));
        Assert.Equal(300,f.Balance("1250"));
        Assert.Equal(3,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void SelectedSupplierRefundLineRemainsSeparateFromOtherPaidLines()
    {
        using var f=new LedgerFixture();
        var bill=SupplierPaymentTests.Bill(f);
        var payment=f.Post(SupplierPaymentTests.Payment(f,bill,35200));
        var refund=f.Post(f.Draft(FinancialKind.PurchaseRefund,1100) with{OriginalId=payment.Id,SupplierRefundPortions=[new(bill.Lines[0].Id,1100)]});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var table=ReadableFinancialDetails.Build(source,new()).Single(t=>t.Name=="Financial supplier refund portions");
        Assert.Equal(2,table.Rows.Count);
        var row=table.Rows.Single(r=>r[0]=="PostedMovement");
        Assert.Equal(refund.Id,row[1]);
        Assert.Equal(bill.Lines[0].Id,Cell(table,row,"PurchaseLineId"));
        Assert.Equal("1100",Cell(table,row,"GrossCents"));
        Assert.Equal(2,f.Book.Posted(f.Business.Owner).Count);
    }

    [Fact]
    public void ActualAssetRefundRetainsChosenTreatmentWithoutInternalGenerations()
    {
        using var f=new LedgerFixture();
        var purchase=f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Post(f.Draft(FinancialKind.Depreciation,10000));
        var refund=f.Post(f.Draft(FinancialKind.SupplierRefund,25000) with
        {
            OriginalId=purchase.Id,CounterAccountId=purchase.Source.CounterAccountId,
            EvidenceId="Fictional-private-asset-refund-evidence",AssetAdjustments=[new(f.Asset.Id,AssetValueChoice.Recover)]
        });
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var tables=ReadableFinancialDetails.Build(source,new());
        Assert.DoesNotContain("Generation",JsonSerializer.Serialize(tables.Select(t=>t.Columns)));
        var table=tables.Single(t=>t.Name=="Financial asset adjustment choices");
        var row=table.Rows.Single(r=>r[0]=="PostedMovement");
        Assert.Equal(refund.Id,row[1]);
        Assert.Equal(f.Asset.Id,Cell(table,row,"AssetId"));
        Assert.Equal(AssetValueChoice.Recover,Cell(table,row,"Treatment"));
        Assert.Empty(tables.Single(t=>t.Name=="Financial refund tax allocations").Rows);
        Assert.Equal(-5000,f.Balance("4010"));
        Assert.Equal(3,f.Book.Posted(f.Business.Owner).Count);
    }
}
