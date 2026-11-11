namespace SarahBeauty.Business.Banking;

internal static class StatementErrorCalculator
{
    internal static StatementErrorCalculation Calculate(ReconciliationLine[] bank,CarriedStatementError[] carried,StatementError[] errors,StatementErrorResolution[] resolutions)
    {
        var lines=bank.ToDictionary(b=>b.Id);
        var roots=carried.ToDictionary(e=>e.Id);
        var claims=new Dictionary<string,long>();
        foreach(var error in errors)Declare(error,lines,roots,claims);
        if(resolutions.Select(r=>(r.ErrorId,r.BankLineId)).Distinct().Count()!=resolutions.Length)
            throw new ArgumentException("Review each bank correction against an error once.");
        foreach(var resolution in resolutions)Resolve(resolution,lines,roots,claims);
        long adjustment=ReconciliationSourcesReader.Cents(roots.Values.Select(e=>Math.Sign(e.SignedCents)*-e.RemainingCents));
        return new(adjustment,claims,roots.Values.OrderBy(e=>e.Date).ThenBy(e=>e.Id,StringComparer.Ordinal).ToArray());
    }

    private static void Declare(StatementError error,Dictionary<string,ReconciliationLine> lines,Dictionary<string,CarriedStatementError> roots,Dictionary<string,long> claims)
    {
        RequireEvidence(error.Cents,error.Explanation,error.EvidenceId);
        if(!Guid.TryParse(error.Id,out _) || roots.ContainsKey(error.Id) || roots.Values.Any(e=>e.BankLineId==error.BankLineId))
            throw new ArgumentException("Use a distinct permanent error ID and review a bank transaction only once.");
        if(!lines.TryGetValue(error.BankLineId,out var line) || error.Cents!=line.RemainingCents)
            throw new ArgumentException("Support the full unmatched remainder of an available bank transaction as a statement error.");
        Claim(line,error.Cents,claims);
        roots.Add(error.Id,new(error.Id,line.Id,line.Date,Math.Sign(line.SignedCents)*error.Cents,error.Cents,error.Explanation,error.EvidenceId));
    }

    private static void Resolve(StatementErrorResolution resolution,Dictionary<string,ReconciliationLine> lines,Dictionary<string,CarriedStatementError> roots,Dictionary<string,long> claims)
    {
        RequireEvidence(resolution.Cents,resolution.Explanation,resolution.EvidenceId);
        if(!roots.TryGetValue(resolution.ErrorId,out var error) || resolution.Cents>error.RemainingCents)
            throw new ArgumentException("A bank correction must stay within the original statement error's remaining amount.");
        if(!lines.TryGetValue(resolution.BankLineId,out var line) || line.Date<error.Date || Math.Sign(line.SignedCents)==Math.Sign(error.SignedCents))
            throw new ArgumentException("Choose an available bank correction in the opposite direction on or after the original error.");
        Claim(line,resolution.Cents,claims);
        roots[error.Id]=error with{RemainingCents=error.RemainingCents-resolution.Cents};
    }

    private static void Claim(ReconciliationLine line,long cents,Dictionary<string,long> claims)
    {
        long total=ReconciliationSourcesReader.Cents([claims.GetValueOrDefault(line.Id),cents]);
        if(total>line.RemainingCents)throw new ArgumentException("Statement errors and corrections exceed the bank transaction's unmatched remainder.");
        claims[line.Id]=total;
    }

    private static void RequireEvidence(long cents,string explanation,string evidence)
    {
        if(cents<=0 || string.IsNullOrWhiteSpace(explanation) || string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("Record a positive statement-error amount, explanation, and bank evidence.");
    }
}
