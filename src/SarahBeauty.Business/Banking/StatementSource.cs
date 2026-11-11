namespace SarahBeauty.Business.Banking;

public sealed record StatementCsvMapping(string DateColumn="Date",string DescriptionColumn="Description",string AmountColumn="Amount",
    string DebitColumn="",string CreditColumn="",string ReferenceColumn="",string DateFormat="yyyy-MM-dd",string Delimiter=",",
    bool NegateAmounts=false,bool ReliableReferences=false);
public sealed record StatementRow(int SourceRow,DateOnly Date,long SignedCents,string Description,string Reference,bool ReliableReference);
public sealed record StatementSource(StatementRow[] Rows,string ContentHash,string MappingJson)
{
    [System.Text.Json.Serialization.JsonIgnore] public long InflowCents=>Rows.Where(r=>r.SignedCents>0).Sum(r=>r.SignedCents);
    [System.Text.Json.Serialization.JsonIgnore] public long OutflowCents=>Rows.Where(r=>r.SignedCents<0).Sum(r=>checked(-r.SignedCents));
    [System.Text.Json.Serialization.JsonIgnore] public long NetCents=>checked(InflowCents-OutflowCents);
}
