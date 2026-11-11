using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

public sealed class BankMatching(BusinessStore store)
{
    internal Action<string>? FailurePoint {get;set;}
    public List<BankMatch> History(OwnerSession owner)=>store.Read(db=>{store.RequireSession(db,owner);return BankMatchQueries.History(db,owner);});
    public List<CashMatchLine> CashLines(OwnerSession owner,string account)=>store.Read(db=>{store.RequireSession(db,owner);return BankMatchQueries.CashLines(db,owner,account);});
    public List<BankMatchGroup> Approvals(OwnerSession owner)=>store.Read(db=>
    {
        store.RequireSession(db,owner);
        return BankMatchQueries.Approvals(db,owner);
    });
    public BankMatchPreview Preview(OwnerSession owner,BankMatchDraft draft)=>store.ReadSnapshot(db=>
    {store.RequireSession(db,owner);return BankMatchRules.Preview(db,owner,Normalize(draft));});

    public BankMatchingSources LoadSources(OwnerSession owner,string account,DateOnly date)=>store.ReadSnapshot(db=>
    {
        store.RequireSession(db,owner);
        var bank=StatementQueries.Lines(db,owner,account);
        var cash=BankMatchQueries.CashLines(db,owner,account);
        var history=BankMatchQueries.History(db,owner);
        return new BankMatchingSources(bank,cash,BankPositions(bank,history,date,StatementErrorHistory.Reservations(db,owner,account)),CashPositions(cash,history,date),history,BankMatchQueries.Approvals(db,owner));
    });

    public List<MatchPosition> BankPositions(OwnerSession owner,string account,DateOnly date)=>LoadSources(owner,account,date).BankPositions;
    public List<MatchPosition> CashPositions(OwnerSession owner,string account,DateOnly date)=>LoadSources(owner,account,date).CashPositions;

    internal static List<MatchPosition> BankPositions(List<BankLine> bank,List<BankMatch> history,DateOnly date,List<BankReservation>? reservations=null)
    {
        var lookup=history.ToLookup(r=>r.BankLineId);
        var reserved=(reservations??[]).Where(r=>r.Date<=date).ToLookup(r=>r.BankLineId);
        return bank.Where(b=>b.Date<=date).Select(b=>new MatchPosition(b.Id,b.SignedCents,BankMatchQueries.Matched(lookup[b.Id],date))
            {ReservedForStatementErrorsCents=ReconciliationSourcesReader.Cents(reserved[b.Id].Select(r=>r.Cents))}).ToList();
    }

    internal static List<MatchPosition> CashPositions(List<CashMatchLine> cash,List<BankMatch> history,DateOnly date)
    {
        var lookup=history.ToLookup(r=>r.JournalLineId);
        return cash.Where(c=>c.Date<=date).Select(c=>new MatchPosition(c.Id,c.IsMatchableAt(date)?c.SignedCents:0,BankMatchQueries.Matched(lookup[c.Id],date))).ToList();
    }

    public BankMatchGroup Approve(OwnerSession owner,BankMatchDraft draft,Dictionary<string,long> expectedGenerations,string requestId,string phrase)
    {
        new OwnerAccess(store).Confirm(owner,phrase);
        if(!Guid.TryParse(requestId,out _))throw new ArgumentException("Use a permanent request ID for this match approval.");
        draft=Normalize(draft);
        string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(draft))));
        return store.Transaction(db=>
        {
            store.RequireSession(db,owner);
            var existing=db.Read("SELECT Id,PayloadHash,ApprovedBy,AtUtc,DataJson FROM BankMatchGroups WHERE BusinessId=$business AND RequestId=$request;",
                r=>(Id:r.GetString(0),Hash:r.GetString(1),Owner:r.GetString(2),At:r.GetString(3),Draft:JsonSerializer.Deserialize<BankMatchDraft>(r.GetString(4))!),
                ("$business",owner.BusinessId),("$request",requestId)).SingleOrDefault();
            if(existing.Id is not null)
            {
                if(existing.Hash!=hash)throw new ArgumentException("This request already belongs to different matching decisions.");
                return new BankMatchGroup(existing.Id,existing.Owner,existing.At,existing.Draft);
            }
            var preview=BankMatchRules.Preview(db,owner,draft);
            if(preview.Generations.Count!=expectedGenerations.Count || preview.Generations.Any(g=>!expectedGenerations.TryGetValue(g.Key,out long value) || value!=g.Value))
                throw new ArgumentException("A source match balance changed. Preview and review the remaining amounts again.");
            return Save(db,owner,preview,requestId,hash);
        });
    }

    private BankMatchGroup Save(DbWork db,OwnerSession owner,BankMatchPreview preview,string request,string hash)
    {
        string group=Guid.NewGuid().ToString(),at=DateTimeOffset.UtcNow.ToString("O");
        db.Execute("INSERT INTO BankMatchGroups (Id,BusinessId,RequestId,PayloadHash,DataJson,ApprovedBy,AtUtc) VALUES ($id,$business,$request,$hash,$data,$owner,$at);",
            ("$id",group),("$business",owner.BusinessId),("$request",request),("$hash",hash),("$data",JsonSerializer.Serialize(preview.Draft)),("$owner",owner.OwnerId),("$at",at));
        FailurePoint?.Invoke("match-group");
        foreach(var p in preview.Draft.Portions)
        {
            db.Execute("INSERT INTO BankMatches (Id,GroupId,BankLineId,JournalLineId,Cents,Date,ReversesId) VALUES ($id,$group,$bank,$journal,$cents,$date,$reverse);",
                ("$id",Guid.NewGuid().ToString()),("$group",group),("$bank",p.BankLineId),("$journal",p.JournalLineId),("$cents",p.Cents),
                ("$date",preview.Draft.Date.ToString("yyyy-MM-dd")),("$reverse",p.ReversesId.Length==0?null:p.ReversesId));
            FailurePoint?.Invoke("bank-match");
        }
        BankMatchRules.ValidateHistory(db,owner,preview.Draft.Portions);
        foreach(string bank in preview.Draft.Portions.Select(p=>p.BankLineId).Distinct())ValueState.Advance(db,owner,"BankMatchBank",bank);
        foreach(string journal in preview.Draft.Portions.Select(p=>p.JournalLineId).Distinct())ValueState.Advance(db,owner,"BankMatchJournal",journal);
        BusinessStore.Audit(db,owner,"Approve bank matching",group);
        FailurePoint?.Invoke("audit");
        return new(group,owner.OwnerId,at,preview.Draft);
    }

    private static BankMatchDraft Normalize(BankMatchDraft draft)=>draft with{Reason=draft.Reason.Trim(),Portions=draft.Portions
        .Select(p=>p with{BankLineId=p.BankLineId.Trim(),JournalLineId=p.JournalLineId.Trim(),ReversesId=p.ReversesId.Trim()})
        .OrderBy(p=>p.BankLineId,StringComparer.Ordinal).ThenBy(p=>p.JournalLineId,StringComparer.Ordinal).ThenBy(p=>p.ReversesId,StringComparer.Ordinal).ToArray()};
}
