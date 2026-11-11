using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class ReconciliationCompleter
{
    internal static CompletedReconciliation Complete(DbWork db,OwnerSession owner,ReconciliationDraft draft,long reviewedWatermark,string request,Action<string>? failure)
    {
        string payload=Payload(draft);
        var existing=db.Read("SELECT Id,PayloadHash FROM Reconciliations WHERE BusinessId=$business AND RequestId=$request;",
            r=>(Id:r.GetString(0),Hash:r.GetString(1)),("$business",owner.BusinessId),("$request",request)).SingleOrDefault();
        if(existing.Id is not null)
        {
            if(existing.Hash!=payload)throw new ArgumentException("This reconciliation request already belongs to a different reviewed decision.");
            return ReconciliationQueries.List(db,owner,existing.Id).Single();
        }
        var sources=ReconciliationSourcesReader.Read(db,owner,draft.StatementId);
        if(sources.Watermark!=reviewedWatermark)throw new ArgumentException("Business records changed. Refresh and review the reconciliation again.");
        PostingValidation.CheckClosedPeriod(db,owner,sources.Statement.Through);
        ReconciliationLocks.RequireWritable(db,owner,sources.Statement.AccountId,sources.Statement.Through);
        var preview=ReconciliationCalculator.Calculate(sources,draft);
        StatementErrorHistory.CheckProposed(db,owner,preview);
        if(!preview.ReadyToComplete)throw new ArgumentException("Resolve every unexplained bank line, unsupported cash remainder, balance difference, and unconfirmed opening before completing.");
        return Save(db,owner,preview,request,payload,failure);
    }

    private static CompletedReconciliation Save(DbWork db,OwnerSession owner,ReconciliationPreview preview,string request,string payload,Action<string>? failure)
    {
        string id=Guid.NewGuid().ToString(),at=DateTimeOffset.UtcNow.ToString("O"),json=JsonSerializer.Serialize(preview),hash=ReconciliationQueries.Hash(json);
        var s=preview.Sources;
        db.Execute("""
            INSERT INTO Reconciliations (Id,BusinessId,AccountId,StatementId,AsOfDate,RequestId,PayloadHash,SnapshotJson,SnapshotHash,ReviewedWatermark,ApprovedBy,AtUtc)
            VALUES ($id,$business,$account,$statement,$date,$request,$payload,$snapshot,$hash,$watermark,$owner,$at);
            """,("$id",id),("$business",owner.BusinessId),("$account",s.Statement.AccountId),("$statement",s.Statement.Id),("$date",s.Statement.Through.ToString("yyyy-MM-dd")),
            ("$request",request),("$payload",payload),("$snapshot",json),("$hash",hash),("$watermark",s.Watermark),("$owner",owner.OwnerId),("$at",at));
        failure?.Invoke("reconciliation-snapshot");
        foreach(var bank in s.Bank)db.Execute("INSERT INTO ReconciliationBankCoverage (ReconciliationId,BankLineId) VALUES ($reconciliation,$bank);",("$reconciliation",id),("$bank",bank.Id));
        failure?.Invoke("reconciliation-coverage");
        StatementErrorHistory.Save(db,owner,id,preview.Draft,failure);
        BusinessStore.Audit(db,owner,"Complete reconciliation",id);failure?.Invoke("audit");
        return new(id,s.Statement.AccountId,s.Statement.Id,s.Statement.Through,owner.OwnerId,at,preview,hash,false);
    }

    private static string Payload(ReconciliationDraft draft)
    {
        string json=draft.StatementErrors.Length==0 && draft.BankCorrections.Length==0
            ?JsonSerializer.Serialize(new{Version=1,draft.StatementId,draft.Reason,draft.Outstanding,draft.PostedCorrectionIds})
            :JsonSerializer.Serialize(new{Version=2,draft.StatementId,draft.Reason,draft.Outstanding,draft.PostedCorrectionIds,draft.StatementErrors,draft.BankCorrections});
        return ReconciliationQueries.Hash(json);
    }
}
