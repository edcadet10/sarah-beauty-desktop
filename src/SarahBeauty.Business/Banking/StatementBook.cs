using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

public sealed class StatementBook(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public List<StatementImport> List(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return StatementQueries.Imports(db,owner);});
    public List<BankLine> BankLines(OwnerSession owner,string account)=>store.Read(db=>{store.RequireSession(db,owner);return StatementQueries.Lines(db,owner,account);});
    public List<StatementImportLine> SourceRows(OwnerSession owner,string statement)=>store.Read(db=>{store.RequireSession(db,owner);return StatementQueries.SourceRows(db,owner,statement);});
    public StatementImportPreview Preview(OwnerSession owner,StatementDraft draft)=>store.ReadSnapshot(db=>
    {store.RequireSession(db,owner);return StatementPlan.Preview(db,owner,StatementValidation.Normalize(draft));});

    public StatementImport Import(OwnerSession owner,StatementDraft draft,long expectedGeneration,StatementRowChoice[] choices,string requestId,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        if(!Guid.TryParse(requestId,out _))throw new ArgumentException("Use a permanent request ID for this reviewed import.");
        draft=StatementValidation.Normalize(draft);
        choices=choices.Select(c=>c with{ExistingLineId=c.ExistingLineId.Trim(),Reason=c.Reason.Trim()}).OrderBy(c=>c.SourceRow).ToArray();
        var legacy=new{draft.AccountId,draft.From,draft.Through,draft.OpeningCents,draft.ClosingCents,draft.Source};
        string sourceHash=draft.EvidenceId.Length==0?Hash(legacy):Hash(new{Statement=legacy,draft.EvidenceId});
        string payloadHash=draft.EvidenceId.Length==0?Hash(new{Draft=legacy,Choices=choices}):Hash(new{Draft=legacy,Choices=choices,draft.EvidenceId});
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var existing=db.Read("SELECT Id,PayloadHash FROM StatementImports WHERE BusinessId=$business AND RequestId=$request;",
                r=>(Id:r.GetString(0),Hash:r.GetString(1)),("$business",owner.BusinessId),("$request",requestId)).SingleOrDefault();
            if(existing.Id is not null)
            {
                if(existing.Hash!=payloadHash)throw new ArgumentException("This import request already belongs to different reviewed contents or decisions.");
                return StatementQueries.Imports(db,owner,existing.Id).Single();
            }
            var preview=StatementPlan.Preview(db,owner,draft);
            if(preview.ExistingImportId.Length>0)throw new ArgumentException("This CSV content was already imported. Open its existing statement and source rows.");
            if(preview.Generation!=expectedGeneration)throw new ArgumentException("This account's imported lines changed. Preview and review the statement again.");
            StatementPlan.ValidateChoices(preview,choices);
            ReconciliationLocks.Import(db,owner,preview,choices);
            var saved=StatementWriter.Save(db,owner,preview,choices,requestId,sourceHash,payloadHash,FailurePoint);
            ValueState.Advance(db,owner,"StatementImport",draft.AccountId);
            BusinessStore.Audit(db,owner,"Import reviewed statement",saved.Id);
            FailurePoint?.Invoke("audit");
            return saved;
        });
    }

    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
