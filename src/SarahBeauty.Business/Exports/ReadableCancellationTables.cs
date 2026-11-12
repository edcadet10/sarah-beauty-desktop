using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableCancellationTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var drafts=new ReadableTableBuilder(source,budget,"Cancellation draft records","DraftId","BusinessId","Revision","BookingId","RequestedDate","DecisionDate","TermsVersionId","Status","ApprovedCaseId","Archived","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");
        var plans=new Plans(source,budget);
        ReadableRecords.ForEach<CancellationDraft>(db,source,"CancellationDraft",(value,audit)=>
        {
            drafts.Add(value.Id,source.BusinessId,value.Revision,value.BookingId,value.RequestedDate,value.Date,value.TermsVersionId,value.Status,value.ApprovedCaseId,value.Archived,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId);
            plans.Add("CurrentDraft",value.Id,value);
        });
        var observed=new ReadableTableBuilder(source,budget,"Cancellation frozen booking facts","CaseId","BusinessId","Phase","Mode","ObservedWatermark","GeneratedAtUtc","AsOf","BookingId","BookingRevision","BookingName","BookingStatus","AcceptedQuoteVersionId","ClosedThrough");
        var actions=new ReadableTableBuilder(source,budget,"Cancellation approved actions","CaseId","BusinessId","Sequence","Kind","SourceId","ResultId","Cents","TaxCents","DestinationBookingId");
        var journal=new ReadableTableBuilder(source,budget,"Cancellation resulting journal","CaseId","BusinessId","Sequence","AccountId","DebitCents","CreditCents","AssetId");
        foreach(var item in Approved(db,source))
        {
            var value=item.Value;plans.Add("ApprovedDecision",item.Id,value.Decision);
            AddObserved(observed,item.Id,source.BusinessId,"Before",value.Before);AddObserved(observed,item.Id,source.BusinessId,"After",value.After);
            for(int i=0;i<value.Actions.Length;i++){var row=value.Actions[i];actions.Add(item.Id,source.BusinessId,i+1,row.Kind,row.SourceId,row.ResultId,row.Cents,row.TaxCents,row.DestinationBookingId);}
            for(int i=0;i<value.Journal.Length;i++){var row=value.Journal[i];journal.Add(item.Id,source.BusinessId,i+1,row.AccountId,row.DebitCents,row.CreditCents,row.AssetId);}
        }
        var approvals=ReadableSqlTables.Read(db,source,budget,"Cancellation approvals",["CaseId","BusinessId","BookingId","DraftId","RequestedDate","DecisionDate","PayloadSha256","SnapshotSha256","ApprovedByOwnerId","ApprovedAtUtc"],
            "SELECT Id,BusinessId,BookingId,DraftId,RequestedDate,DecisionDate,PayloadHash,SnapshotHash,ApprovedBy,AtUtc FROM CancellationDecisions WHERE BusinessId=$business ORDER BY DecisionDate,AtUtc,Id;");
        return new[]{drafts.Freeze(),approvals,observed.Freeze(),actions.Freeze(),journal.Freeze()}.Concat(plans.Freeze()).ToArray();
    });

    private static void AddObserved(ReadableTableBuilder table,string id,string business,string phase,CancellationSnapshot value)=>table.Add(id,business,phase,value.Mode,value.Watermark,value.GeneratedAtUtc,value.AsOf,value.BookingId,value.BookingRevision,value.BookingName,value.BookingStatus,value.AcceptedQuoteVersionId,value.ClosedThrough);

    private static List<(string Id,CancellationPreview Value)> Approved(DbWork db,ReadableExportSource source)=>db.Read(
        "SELECT Id,BookingId,DraftId,RequestedDate,DecisionDate,SnapshotJson FROM CancellationDecisions WHERE BusinessId=$business ORDER BY DecisionDate,AtUtc,Id;",reader=>
        {
            CancellationPreview value;
            try{value=JsonSerializer.Deserialize<CancellationPreview>(reader.GetString(5))??throw new JsonException();}
            catch(JsonException){throw new InvalidDataException("A retained cancellation decision cannot be projected.");}
            if(value.Decision.Id!=reader.GetString(2)||value.Decision.BookingId!=reader.GetString(1)||value.Before.BookingId!=reader.GetString(1)||value.After.BookingId!=reader.GetString(1)||value.Decision.RequestedDate?.ToString("yyyy-MM-dd")!=reader.GetString(3)||value.Decision.Date?.ToString("yyyy-MM-dd")!=reader.GetString(4))throw new InvalidDataException("A retained cancellation does not match its relational identity.");
            return(reader.GetString(0),value);
        },("$business",source.BusinessId));

    private sealed class Plans
    {
        private readonly ReadableExportSource _source;
        private readonly ReadableTableBuilder _unapply,_reductions,_values;
        internal Plans(ReadableExportSource source,ReadableExportBudget budget)
        {
            _source=source;
            _unapply=new(source,budget,"Cancellation unapplication choices","SourceKind","SourceId","BusinessId","DraftId","ApplicationId","Cents");
            _reductions=new(source,budget,"Cancellation invoice reductions","SourceKind","SourceId","BusinessId","DraftId","InvoiceVersionId","ChargeId","Quantity");
            _values=new(source,budget,"Cancellation value choices","SourceKind","SourceId","BusinessId","DraftId","PaymentId","CreditId","RetainCents","RefundCents","RefundTaxCents","TransferCents","DestinationBookingId","RefundCashAccountId","RefundMethod","RefundTaxResolved","LiabilityTaxCents","RecoverableTaxCents","NonrecoverableTaxCents");
        }
        internal void Add(string kind,string id,CancellationDraft value)
        {
            foreach(var row in value.Unapplications.OrderBy(r=>r.ApplicationId))_unapply.Add(kind,id,_source.BusinessId,value.Id,row.ApplicationId,row.Cents);
            foreach(var row in value.Reductions.OrderBy(r=>r.InvoiceVersionId))foreach(var portion in row.Portions.OrderBy(p=>p.ChargeId))_reductions.Add(kind,id,_source.BusinessId,value.Id,row.InvoiceVersionId,portion.ChargeId,portion.Quantity);
            foreach(var row in value.Values.OrderBy(r=>r.PaymentId).ThenBy(r=>r.CreditId))_values.Add(kind,id,_source.BusinessId,value.Id,row.PaymentId,row.CreditId,row.RetainCents,row.RefundCents,row.RefundTaxCents,row.TransferCents,row.DestinationBookingId,row.RefundCashAccountId,row.RefundMethod,row.RefundTaxResolved,row.TaxRefundAllocation?.LiabilityCents,row.TaxRefundAllocation?.RecoverableCents,row.TaxRefundAllocation?.NonrecoverableCents);
        }
        internal ReadableExportTable[] Freeze()=>[_unapply.Freeze(),_reductions.Freeze(),_values.Freeze()];
    }
}
