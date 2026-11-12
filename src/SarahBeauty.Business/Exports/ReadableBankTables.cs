namespace SarahBeauty.Business.Exports;

internal static class ReadableBankTables
{
    internal static ReadableExportTable[] Build(ReadableExportSnapshot snapshot,ReadableExportBudget budget)=>snapshot.Read(db=>
    {
        var source=snapshot.Source;
        return new[]{
            ReadableSqlTables.Read(db,source,budget,"Bank statements",["StatementId","BusinessId","AccountId","PeriodStart","PeriodEnd","OpeningCents","ClosingCents","ContentSha256","SourceSha256","PayloadSha256","RequestId","ApprovedByOwnerId","ApprovedAtUtc","Withdrawn"],
                "SELECT S.Id,S.BusinessId,S.AccountId,S.PeriodStart,S.PeriodEnd,S.OpeningCents,S.ClosingCents,S.ContentHash,S.SourceHash,S.PayloadHash,S.RequestId,S.ApprovedBy,S.AtUtc,CASE WHEN EXISTS(SELECT 1 FROM StatementWithdrawals W WHERE W.StatementId=S.Id) THEN 'true' ELSE 'false' END FROM StatementImports S WHERE S.BusinessId=$business ORDER BY S.PeriodEnd,S.Id;"),
            ReadableSqlTables.Read(db,source,budget,"Bank import mappings",["StatementId","BusinessId","DateColumn","DescriptionColumn","AmountColumn","DebitColumn","CreditColumn","ReferenceColumn","DateFormat","Delimiter","NegateAmounts","ReliableReferences"],
                "SELECT Id,BusinessId,json_extract(MappingJson,'$.DateColumn'),json_extract(MappingJson,'$.DescriptionColumn'),json_extract(MappingJson,'$.AmountColumn'),json_extract(MappingJson,'$.DebitColumn'),json_extract(MappingJson,'$.CreditColumn'),json_extract(MappingJson,'$.ReferenceColumn'),json_extract(MappingJson,'$.DateFormat'),json_extract(MappingJson,'$.Delimiter'),CASE json_extract(MappingJson,'$.NegateAmounts') WHEN 1 THEN 'true' WHEN 0 THEN 'false' END,CASE json_extract(MappingJson,'$.ReliableReferences') WHEN 1 THEN 'true' WHEN 0 THEN 'false' END FROM StatementImports WHERE BusinessId=$business ORDER BY Id;"),
            ReadableSqlTables.Read(db,source,budget,"Bank imported source rows",["StatementId","BusinessId","SourceRow","BankLineId","OriginalDate","OriginalSignedCents","OriginalDescription","OriginalReference","OriginalReliableReference"],
                "SELECT R.StatementId,S.BusinessId,R.SourceRow,R.BankLineId,json_extract(R.SourceJson,'$.Date'),json_extract(R.SourceJson,'$.SignedCents'),json_extract(R.SourceJson,'$.Description'),json_extract(R.SourceJson,'$.Reference'),CASE json_extract(R.SourceJson,'$.ReliableReference') WHEN 1 THEN 'true' WHEN 0 THEN 'false' END FROM StatementRows R JOIN StatementImports S ON S.Id=R.StatementId WHERE S.BusinessId=$business ORDER BY R.StatementId,R.SourceRow;"),
            ReadableSqlTables.Read(db,source,budget,"Bank transactions",["BankLineId","BusinessId","AccountId","Date","SignedCents","Description","Reference","ReliableReference","HasActiveStatement"],
                "SELECT B.Id,B.BusinessId,B.AccountId,B.Date,B.SignedCents,B.Description,B.Reference,CASE B.ReliableReference WHEN 1 THEN 'true' ELSE 'false' END,CASE WHEN EXISTS(SELECT 1 FROM StatementRows R WHERE R.BankLineId=B.Id AND NOT EXISTS(SELECT 1 FROM StatementWithdrawals W WHERE W.StatementId=R.StatementId)) THEN 'true' ELSE 'false' END FROM BankLines B WHERE B.BusinessId=$business ORDER BY B.Date,B.Id;"),
            ReadableSqlTables.Read(db,source,budget,"Bank statement withdrawals",["WithdrawalId","BusinessId","StatementId","RequestId","OwnerId","AtUtc"],
                "SELECT W.Id,S.BusinessId,W.StatementId,W.RequestId,W.OwnerId,W.AtUtc FROM StatementWithdrawals W JOIN StatementImports S ON S.Id=W.StatementId WHERE S.BusinessId=$business ORDER BY W.AtUtc,W.Id;"),
            ReadableSqlTables.Read(db,source,budget,"Bank match approvals",["GroupId","BusinessId","RequestId","PayloadSha256","ApprovedByOwnerId","ApprovedAtUtc"],
                "SELECT Id,BusinessId,RequestId,PayloadHash,ApprovedBy,AtUtc FROM BankMatchGroups WHERE BusinessId=$business ORDER BY AtUtc,Id;"),
            ReadableSqlTables.Read(db,source,budget,"Bank match portions",["MatchId","BusinessId","GroupId","BankLineId","JournalLineId","Cents","Date","ReversesMatchId"],
                "SELECT M.Id,G.BusinessId,M.GroupId,M.BankLineId,M.JournalLineId,M.Cents,M.Date,M.ReversesId FROM BankMatches M JOIN BankMatchGroups G ON G.Id=M.GroupId WHERE G.BusinessId=$business ORDER BY M.Date,M.Id;")};
    });
}
