using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class BankMatchRules
{
    internal static BankMatchPreview Preview(DbWork db,OwnerSession owner,BankMatchDraft draft)
    {
        PostingValidation.CheckClosedPeriod(db,owner,draft.Date);
        if(string.IsNullOrWhiteSpace(draft.Reason) || draft.Portions.Length is <1 or >1000 || draft.Portions.Any(p=>p.Cents<=0)
            || draft.Portions.Select(p=>(p.BankLineId,p.JournalLineId,p.ReversesId)).Distinct().Count()!=draft.Portions.Length)
            throw new ArgumentException("Review 1 to 1,000 distinct match portions with positive amounts and a reason.");
        var history=BankMatchQueries.History(db,owner);
        var cash=BankMatchQueries.CashLines(db,owner).ToDictionary(c=>c.Id);
        var bankLines=BankSources(db,owner,draft.Portions.Where(p=>cash.ContainsKey(p.JournalLineId)).Select(p=>cash[p.JournalLineId]));
        var generations=new Dictionary<string,long>();
        foreach(var portion in draft.Portions)
        {
            if(!cash.TryGetValue(portion.JournalLineId,out var journal))throw new ArgumentException("Choose a posted cash journal line from this business.");
            if(!bankLines.TryGetValue(portion.BankLineId,out var bank) || bank.AccountId!=journal.AccountId)
                throw new ArgumentException("Choose a bank line on the same account as the cash journal line.");
            ValidatePortion(draft,portion,bank,journal,history);
            generations["Bank:"+bank.Id]=ValueState.Generation(db,owner,"BankMatchBank",bank.Id);
            generations["Journal:"+journal.Id]=ValueState.Generation(db,owner,"BankMatchJournal",journal.Id);
        }
        var proposed=draft.Portions.Select((p,i)=>new BankMatch("preview-"+i,"",p.BankLineId,p.JournalLineId,p.Cents,draft.Date,p.ReversesId));
        ValidateHistory(db,owner,draft.Portions,proposed);
        return new(draft,generations);
    }

    private static void ValidatePortion(BankMatchDraft draft,MatchPortion p,BankLine bank,CashMatchLine cash,List<BankMatch> history)
    {
        if(draft.Date<bank.Date || draft.Date<cash.Date || Math.Sign(bank.SignedCents)!=Math.Sign(cash.SignedCents))
            throw new ArgumentException("Match the same cash direction on or after both source dates.");
        if(p.ReversesId.Length>0)
        {
            var original=history.SingleOrDefault(r=>r.Id==p.ReversesId && r.ReversesId.Length==0)
                ??throw new ArgumentException("Choose an original match to undo; an unmatch cannot itself be reversed.");
            if(original.BankLineId!=bank.Id || original.JournalLineId!=cash.Id || draft.Date<original.Date
                || p.Cents>original.Cents-history.Where(r=>r.ReversesId==original.Id && r.Date<=draft.Date).Sum(r=>r.Cents))
                throw new ArgumentException("The unmatch must retain its source lines and stay within the original match's remaining amount.");
            return;
        }
        if(cash.ReversesMovementId.Length>0 || cash.ReversedDate.HasValue && draft.Date>=cash.ReversedDate.Value)
            throw new ArgumentException("Match an active actual cash source. A posting cancellation is not an additional bank transaction; an actual cash return needs its refund record.");
        if(p.Cents>Math.Abs(bank.SignedCents)-BankMatchQueries.Matched(history.Where(r=>r.BankLineId==bank.Id),draft.Date)
            || p.Cents>Math.Abs(cash.SignedCents)-BankMatchQueries.Matched(history.Where(r=>r.JournalLineId==cash.Id),draft.Date))
            throw new ArgumentException("The match exceeds a remaining bank or cash-journal amount.");
    }

    internal static void ValidateHistory(DbWork db,OwnerSession owner,IEnumerable<MatchPortion> portions,IEnumerable<BankMatch>? proposed=null)
    {
        var all=BankMatchQueries.History(db,owner).Concat(proposed??[]).ToList();
        var cash=BankMatchQueries.CashLines(db,owner).ToDictionary(c=>c.Id);
        var bankLines=BankSources(db,owner,portions.Select(p=>cash[p.JournalLineId]));
        foreach(string id in portions.Select(p=>p.JournalLineId).Distinct())
        {
            var line=cash[id];
            var rows=all.Where(r=>r.JournalLineId==id).ToList();
            Check(rows,rows.Select(r=>r.Date).Concat(line.ReversedDate.HasValue?[line.ReversedDate.Value]:[]),d=>line.Date>d || line.ReversesMovementId.Length>0 || line.ReversedDate<=d?0:Math.Abs(line.SignedCents));
        }
        foreach(string id in portions.Select(p=>p.BankLineId).Distinct())
        {
            var line=bankLines[id];
            var rows=all.Where(r=>r.BankLineId==id).ToList();
            Check(rows,rows.Select(r=>r.Date),_=>Math.Abs(line.SignedCents));
        }
        foreach(string id in portions.Where(p=>p.ReversesId.Length>0).Select(p=>p.ReversesId).Distinct())
        {
            var original=all.Single(r=>r.Id==id);
            if(all.Where(r=>r.ReversesId==id).Aggregate(System.Numerics.BigInteger.Zero,(sum,r)=>sum+r.Cents)>original.Cents)
                throw new ArgumentException("Unmatches exceed the original match amount.");
        }
    }

    private static Dictionary<string,BankLine> BankSources(DbWork db,OwnerSession owner,IEnumerable<CashMatchLine> cash)=>
        cash.Select(c=>c.AccountId).Distinct().SelectMany(account=>StatementQueries.Lines(db,owner,account)).ToDictionary(b=>b.Id);

    private static void Check(List<BankMatch> rows,IEnumerable<DateOnly> dates,Func<DateOnly,long> capacity)
    {
        foreach(var date in dates.Distinct())
        {
            long matched=BankMatchQueries.Matched(rows,date);
            if(matched<0 || matched>capacity(date))throw new ArgumentException($"Matching would exceed a source's available amount on {date:yyyy-MM-dd}.");
        }
    }

    internal static void ValidateMovementReversal(DbWork db,OwnerSession owner,string movement,DateOnly date)
    {
        var history=BankMatchQueries.History(db,owner);
        foreach(var line in BankMatchQueries.CashLines(db,owner).Where(c=>c.MovementId==movement))
        {
            var matches=history.Where(r=>r.JournalLineId==line.Id).ToList();
            foreach(var affected in matches.Select(r=>r.Date).Append(date).Where(d=>d>=date).Distinct())
                if(BankMatchQueries.Matched(matches,affected)>0)throw new ArgumentException("Unmatch this movement's active bank matches before reversing it, including dated dependencies.");
        }
    }
}
