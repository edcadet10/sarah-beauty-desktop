using SarahBeauty.Business.Resources;

namespace SarahBeauty.Business.Exports;

internal static class ReadableResourceTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var suppliers=new ReadableTableBuilder(source,budget,"Suppliers","SupplierId","BusinessId","Revision","Name","Archived","Phone","Email","Address","Terms","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");ReadableRecords.ForEach<Supplier>(db,source,"Supplier",(value,audit)=>suppliers.Add(value.Id,source.BusinessId,value.Revision,value.Name,value.Archived,value.Phone,value.Email,value.Address,value.Terms,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId));
        var loans=new ReadableTableBuilder(source,budget,"Loan register","LoanId","BusinessId","Revision","LenderName","Archived","OpeningDate","OpeningPrincipalCents","Terms","DueDate","OwnerLenderId","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");ReadableRecords.ForEach<Loan>(db,source,"Loan",(value,audit)=>loans.Add(value.Id,source.BusinessId,value.Revision,value.Name,value.Archived,value.OpeningDate,value.OpeningPrincipalCents,value.Terms,value.DueDate,value.OwnerLenderId,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId));
        var assets=new ReadableTableBuilder(source,budget,"Asset register","AssetId","BusinessId","Revision","Name","Archived","LegalOwner","AcquisitionDate","InServiceDate","RegisteredCostCents","TaxBasisCents","BusinessUseFraction","Funding","SerialNumber","DisposedDate","DisposalMovementId","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var history=new ReadableTableBuilder(source,budget,"Asset registered fact reviews","RequestId","AssetId","BusinessId","Sequence","PreviousRegisteredCostCents","PreviousTaxBasisCents","RegisteredCostCents","TaxBasisCents","ReviewedByOwnerId","AtUtc");
        ReadableRecords.ForEach<Asset>(db,source,"Asset",(value,audit)=>
        {
            assets.Add(value.Id,source.BusinessId,value.Revision,value.Name,value.Archived,value.LegalOwner,value.AcquisitionDate,value.InServiceDate,value.CostCents,value.TaxBasisCents,value.BusinessUseFraction,value.Funding,value.SerialNumber,value.DisposedDate,value.DisposalMovementId,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            for(int i=0;i<value.FactHistory.Length;i++){var review=value.FactHistory[i];history.Add(review.RequestId,value.Id,source.BusinessId,i+1,review.PreviousCostCents,review.PreviousTaxBasisCents,review.CostCents,review.TaxBasisCents,review.OwnerId,review.AtUtc);}
        });return new[]{suppliers.Freeze(),loans.Freeze(),assets.Freeze(),history.Freeze()};
    });
}
