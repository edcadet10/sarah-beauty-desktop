using System.Text.Json;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class StatementWriter
{
    internal static StatementImport Save(DbWork db,OwnerSession owner,StatementImportPreview preview,StatementRowChoice[] choices,
        string request,string sourceHash,string payloadHash,Action<string>? failure)
    {
        string id=Guid.NewGuid().ToString(),at=DateTimeOffset.UtcNow.ToString("O");
        var draft=preview.Draft;
        db.Execute("""
            INSERT INTO StatementImports (Id,BusinessId,AccountId,PeriodStart,PeriodEnd,OpeningCents,ClosingCents,ContentHash,MappingJson,SourceHash,PayloadHash,RequestId,ApprovedBy,AtUtc)
            VALUES ($id,$business,$account,$from,$through,$opening,$closing,$content,$mapping,$source,$payload,$request,$owner,$at);
            """,("$id",id),("$business",owner.BusinessId),("$account",draft.AccountId),("$from",draft.From.ToString("yyyy-MM-dd")),("$through",draft.Through.ToString("yyyy-MM-dd")),
            ("$opening",draft.OpeningCents),("$closing",draft.ClosingCents),("$content",draft.Source.ContentHash.Length==0?null:draft.Source.ContentHash),
            ("$mapping",draft.Source.MappingJson),("$source",sourceHash),("$payload",payloadHash),("$request",request),("$owner",owner.OwnerId),("$at",at));
        failure?.Invoke("statement-header");
        var decisions=choices.ToDictionary(c=>c.SourceRow);
        foreach(var row in draft.Source.Rows)SaveRow(db,owner,id,draft.AccountId,row,decisions.GetValueOrDefault(row.SourceRow),failure);
        return new(id,draft.AccountId,draft.From,draft.Through,draft.OpeningCents!.Value,draft.ClosingCents!.Value,draft.Source.ContentHash,owner.OwnerId,at,draft.Source.Rows.Length);
    }

    private static void SaveRow(DbWork db,OwnerSession owner,string statement,string account,StatementRow row,StatementRowChoice? choice,Action<string>? failure)
    {
        string line=choice?.ExistingLineId??"";
        if(line.Length==0)
        {
            line=Guid.NewGuid().ToString();
            db.Execute("""
                INSERT INTO BankLines (Id,BusinessId,AccountId,Date,SignedCents,Description,Reference,ReliableReference)
                VALUES ($id,$business,$account,$date,$amount,$description,$reference,$reliable);
                """,("$id",line),("$business",owner.BusinessId),("$account",account),("$date",row.Date.ToString("yyyy-MM-dd")),("$amount",row.SignedCents),
                ("$description",row.Description),("$reference",row.Reference),("$reliable",row.ReliableReference?1:0));
        }
        failure?.Invoke("bank-line");
        db.Execute("INSERT INTO StatementRows (StatementId,SourceRow,BankLineId,SourceJson,Reason) VALUES ($statement,$row,$line,$source,$reason);",
            ("$statement",statement),("$row",row.SourceRow),("$line",line),("$source",JsonSerializer.Serialize(row)),("$reason",choice?.Reason??""));
        failure?.Invoke("statement-row");
    }
}
