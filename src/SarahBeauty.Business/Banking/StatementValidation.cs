using System.Text.Json;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Banking;

internal static class StatementValidation
{
    internal static StatementDraft Normalize(StatementDraft value)=>value with{Source=value.Source with
    {
        Rows=value.Source.Rows.OrderBy(r=>r.SourceRow).Select(r=>r with{Description=r.Description.Trim(),Reference=r.Reference.Trim()}).ToArray(),
        ContentHash=value.Source.ContentHash.Trim().ToUpperInvariant()
    }};

    internal static void Validate(DbWork db,OwnerSession owner,StatementDraft d)
    {
        if(!ChartOfAccounts.List(db,owner).Any(a=>a.Id==d.AccountId && a.Active && a.CashKind!="None"))throw new ArgumentException("Choose an active cash, bank, or processor account from this business.");
        if(d.From>d.Through || d.OpeningCents is null || d.ClosingCents is null || d.Source.Rows.Length is <1 or >StatementCsv.MaximumRows)
            throw new ArgumentException("Confirm statement dates, opening and closing balances, and 1 to 10,000 source rows.");
        if(d.Source.Rows.Select(r=>r.SourceRow).Distinct().Count()!=d.Source.Rows.Length || d.Source.Rows.Any(r=>r.SourceRow<=0 || r.Date<d.From || r.Date>d.Through
            || r.SignedCents is 0 or long.MinValue || r.Description.Length is <1 or >2000 || r.Reference.Length>200 || r.ReliableReference && r.Reference.Length==0))
            throw new ArgumentException("Each statement row needs a distinct source row number, date inside the period, nonzero amount, and valid description/reference.");
        if(d.Source.Rows.Where(r=>r.ReliableReference).GroupBy(r=>r.Reference,StringComparer.Ordinal).Any(g=>g.Count()>1))
            throw new ArgumentException("A reference marked unique occurs more than once in this source. Review the mapping and real transaction identities.");
        if(d.Source.ContentHash.Length>0 && (d.Source.ContentHash.Length!=64 || !d.Source.ContentHash.All(char.IsAsciiHexDigit)))throw new ArgumentException("The source content hash must be a SHA-256 hash, or blank for manual rows.");
        if(d.Source.MappingJson.Length>20000)throw new ArgumentException("The statement mapping is too large.");
        using var mapping=JsonDocument.Parse(d.Source.MappingJson);
        if(mapping.RootElement.ValueKind!=JsonValueKind.Object)throw new ArgumentException("Retain the statement mapping as an object.");
        try
        {
            _=d.Source.InflowCents;_=d.Source.OutflowCents;
            if(checked(d.OpeningCents.Value+d.Source.NetCents)!=d.ClosingCents.Value)throw new ArgumentException("Opening balance plus every source row must equal the statement closing balance.");
        }
        catch(OverflowException){throw new ArgumentException("Statement amounts exceed the supported cent range.");}
    }
}
