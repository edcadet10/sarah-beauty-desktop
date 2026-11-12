using SarahBeauty.Business.Finance;

namespace SarahBeauty.Business.Exports;

internal static class ReadableCreditTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        var credits=ReadableSqlTables.Read(db,source,budget,"Customer credits",["CreditId","BusinessId","CustomerId","SourcePaymentId","IssueDate","IssuedCents","IssuedTaxCents","IssuedByOwnerId","IssuedAtUtc"],"SELECT Id,BusinessId,CustomerId,PaymentId,IssueDate,IssuedCents,IssuedTaxCents,OwnerId,AtUtc FROM CustomerCredits WHERE BusinessId=$business ORDER BY IssueDate,Id;");
        var funding=ReadableSqlTables.Read(db,source,budget,"Credit funding",["FundingId","BusinessId","CreditId","PaymentId","Kind","Date","Cents","TaxCents","RecordedByOwnerId","RefundMovementId","ReversesFundingId","SourceDraftId"],"SELECT F.Id,C.BusinessId,F.CreditId,F.PaymentId,F.Kind,F.Date,F.Cents,F.TaxCents,F.OwnerId,F.RefundMovementId,F.ReversesFundingId,F.SourceDraftId FROM CreditFunding F JOIN CustomerCredits C ON C.Id=F.CreditId WHERE C.BusinessId=$business ORDER BY F.Date,F.Id;");
        var groups=ReadableSqlTables.Read(db,source,budget,"Application approvals",["GroupId","BusinessId","DraftId","SourceRevision","PayloadSha256","ApprovedByOwnerId","ApprovedAtUtc"],"SELECT Id,BusinessId,DraftId,SourceRevision,PayloadHash,ApprovedBy,AtUtc FROM ApplicationGroups WHERE BusinessId=$business ORDER BY AtUtc,Id;");
        var applications=ReadableSqlTables.Read(db,source,budget,"Payment applications",["ApplicationId","BusinessId","GroupId","PaymentId","CreditId","InvoiceVersionId","MilestoneId","Cents","TaxCents","Date","ReversesApplicationId"],"SELECT A.Id,G.BusinessId,A.GroupId,A.PaymentId,A.CreditId,A.InvoiceVersionId,A.MilestoneId,A.Cents,A.TaxCents,A.Date,A.ReversesId FROM PaymentApplications A JOIN ApplicationGroups G ON G.Id=A.GroupId WHERE G.BusinessId=$business ORDER BY A.Date,A.Id;");
        var drafts=new ReadableTableBuilder(source,budget,"Credit draft records","DraftId","BusinessId","Revision","Action","PaymentId","CreditId","Cents","Date","Status","Archived","CreatedAtUtc","CreatedByOwnerId","UpdatedAtUtc","UpdatedByOwnerId");ReadableRecords.ForEach<CreditDraft>(db,source,"CreditDraft",(value,audit)=>drafts.Add(value.Id,source.BusinessId,value.Revision,value.Action,value.PaymentId,value.CreditId,value.Cents,value.Date,value.Status,value.Archived,audit.CreatedAtUtc,audit.CreatedByOwnerId,audit.UpdatedAtUtc,audit.UpdatedByOwnerId));
        return new[]{credits,funding,groups,applications,drafts.Freeze()};
    });
}
