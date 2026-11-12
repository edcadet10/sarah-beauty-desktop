using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableTaxTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var periods=new ReadableTableBuilder(source,budget,"Tax filing periods","PeriodId","BusinessId","Revision","Name","Jurisdiction","From","Through","DueDate","Status","Archived","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var history=new ReadableTableBuilder(source,budget,"Tax filing history","PeriodId","BusinessId","Sequence","RequestId","ReviewedRevision","Status","FilingDate","ReportedTaxCents","Reference","OwnerId","AtUtc");
        ReadableRecords.ForEach<TaxPeriod>(db,source,"TaxPeriod",(value,audit)=>
        {
            periods.Add(value.Id,source.BusinessId,value.Revision,value.Name,value.Jurisdiction,value.From,value.Through,value.DueDate,value.Status,value.Archived,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            for(int index=0;index<value.History.Length;index++)
            {
                var item=value.History[index];var decision=item.Decision;
                history.Add(value.Id,source.BusinessId,index+1,decision.RequestId,decision.ExpectedRevision,decision.Status,decision.FilingDate,decision.ReportedTaxCents,decision.Reference,item.OwnerId,item.AtUtc);
            }
        });
        return new[]{periods.Freeze(),history.Freeze()};
    });
}
