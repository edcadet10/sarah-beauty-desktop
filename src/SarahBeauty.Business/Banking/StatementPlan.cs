using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class StatementPlan
{
    internal static StatementImportPreview Preview(DbWork db,OwnerSession owner,StatementDraft draft)
    {
        StatementValidation.Validate(db,owner,draft);
        var lines=StatementQueries.Lines(db,owner,draft.AccountId);
        var amounts=lines.GroupBy(b=>(b.Date,b.SignedCents)).ToDictionary(g=>g.Key,g=>g.ToArray());
        var references=lines.Where(b=>b.ReliableReference).ToDictionary(b=>b.Reference);
        var counts=draft.Source.Rows.GroupBy(r=>(r.Date,r.SignedCents)).ToDictionary(g=>g.Key,g=>g.Count());
        var rows=draft.Source.Rows.Select(row=>new StatementRowReview(row,
            Candidates(row,amounts,references),counts[(row.Date,row.SignedCents)]>1)).ToArray();
        string existing=draft.Source.ContentHash.Length==0?"":db.Scalar("SELECT Id FROM StatementImports WHERE BusinessId=$business AND AccountId=$account AND ContentHash=$hash;",
            ("$business",owner.BusinessId),("$account",draft.AccountId),("$hash",draft.Source.ContentHash)) as string??"";
        return new(draft,ValueState.Generation(db,owner,"StatementImport",draft.AccountId),rows,existing);
    }

    private static BankLine[] Candidates(StatementRow row,Dictionary<(DateOnly Date,long SignedCents),BankLine[]> amounts,Dictionary<string,BankLine> references)
    {
        var matches=amounts.GetValueOrDefault((row.Date,row.SignedCents))??[];
        if(row.ReliableReference && references.TryGetValue(row.Reference,out var same) && matches.All(b=>b.Id!=same.Id))return [..matches,same];
        return matches;
    }

    internal static void ValidateChoices(StatementImportPreview preview,StatementRowChoice[] choices)
    {
        var sourceRows=preview.Rows.Select(r=>r.Row.SourceRow).ToHashSet();
        if(choices.Select(c=>c.SourceRow).Distinct().Count()!=choices.Length || choices.Any(c=>!sourceRows.Contains(c.SourceRow)))
            throw new ArgumentException("Review each source row once and keep its original row number.");
        var decisions=choices.ToDictionary(c=>c.SourceRow);
        var linked=new HashSet<string>();
        foreach(var review in preview.Rows)
        {
            var choice=decisions.GetValueOrDefault(review.Row.SourceRow);
            if(review.Candidates.Length>0 || review.SimilarInSource || choice?.ExistingLineId.Length>0)
                if(string.IsNullOrWhiteSpace(choice?.Reason))throw new ArgumentException($"Explain whether source row {review.Row.SourceRow} is an existing transaction or a distinct real transaction.");
            if(choice?.ExistingLineId.Length>0)
            {
                var existing=review.Candidates.SingleOrDefault(b=>b.Id==choice.ExistingLineId);
                if(existing is null || existing.Date!=review.Row.Date || existing.SignedCents!=review.Row.SignedCents
                    || review.Row.ReliableReference && (!existing.ReliableReference || existing.Reference!=review.Row.Reference) || !linked.Add(existing.Id))
                    throw new ArgumentException("Link an existing transaction with the same date, amount, and any unique reference, at most once in one statement.");
            }
            else if(review.Row.ReliableReference && review.Candidates.Any(b=>b.ReliableReference && b.Reference==review.Row.Reference))
                throw new ArgumentException("This unique bank reference already exists. Link its matching transaction or review the source mapping.");
        }
    }
}
