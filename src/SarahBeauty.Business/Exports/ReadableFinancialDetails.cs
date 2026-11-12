using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableFinancialDetails
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        string[] common=["SourceKind","SourceId","BusinessId","DraftId","SourceRevision"];
        var lenders=new ReadableTableBuilder(source,budget,"Financial frozen lender facts",[..common,"LoanId","LenderName","OwnerLenderId","OwnerLenderName"]);
        var assets=new ReadableTableBuilder(source,budget,"Financial asset adjustment choices",[..common,"AssetId","Treatment"]);
        var refunds=new ReadableTableBuilder(source,budget,"Financial supplier refund portions",[..common,"PurchaseLineId","GrossCents"]);
        var tax=new ReadableTableBuilder(source,budget,"Financial refund tax allocations",[..common,"LiabilityCents","RecoverableCents","NonrecoverableCents"]);
        void Add(string kind,string id,FinancialDraft value)
        {
            object?[] key=[kind,id,source.BusinessId,value.Id,value.Revision];
            if(value.LoanLender is {} lender)lenders.Add([..key,lender.LoanId,lender.LenderName,lender.OwnerLenderId,lender.OwnerLenderName]);
            foreach(var row in value.AssetAdjustments.OrderBy(r=>r.AssetId))assets.Add([..key,row.AssetId,row.Treatment]);
            foreach(var row in value.SupplierRefundPortions.OrderBy(r=>r.LineId))refunds.Add([..key,row.LineId,row.GrossCents]);
            if(value.TaxRefundAllocation is {} split)tax.Add([..key,split.LiabilityCents,split.RecoverableCents,split.NonrecoverableCents]);
        }
        ReadableRecords.ForEach<FinancialDraft>(db,source,"FinancialDraft",(value,_)=>Add("CurrentDraft",value.Id,value));
        ReadableFinancialSources.ForEachPosted(db,source,(value,audit)=>Add("PostedMovement",audit.MovementId,value));
        return new[]{lenders.Freeze(),assets.Freeze(),refunds.Freeze(),tax.Freeze()};
    });
}
