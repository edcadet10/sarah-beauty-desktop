using System.Globalization;
using System.Numerics;
using System.Text;

namespace SarahBeauty.Business.Exports;

internal sealed record ReadableTableFacts(string Name,int DataRows,int Columns,long Cells,long RawUtf8Bytes,string Sha256);
internal sealed record ReadableJournalFacts(long Rows,string DebitCents,string CreditCents);
internal static class ReadableGenerationFacts
{
    internal static ReadableTableFacts Table(ReadableExportTable table)
    {
        long bytes=table.Columns.Sum(c=>(long)Encoding.UTF8.GetByteCount(c))+table.Rows.Sum(r=>r.Sum(c=>(long)Encoding.UTF8.GetByteCount(c)));
        return new(table.Name,table.Rows.Count,table.Columns.Count,checked((table.Rows.Count+1L)*table.Columns.Count),bytes,table.Sha256);
    }

    internal static ReadableJournalFacts Journal(ReadableExportTable table)
    {
        int debit=table.Columns.ToList().IndexOf("DebitCents"),credit=table.Columns.ToList().IndexOf("CreditCents");
        if(table.Name!="Journal lines"||debit<0||credit<0)throw Invalid();BigInteger debits=0,credits=0;
        foreach(var row in table.Rows){debits+=Cents(row[debit]);credits+=Cents(row[credit]);}
        if(debits!=credits)throw Invalid();return new(table.Rows.Count,debits.ToString(CultureInfo.InvariantCulture),credits.ToString(CultureInfo.InvariantCulture));
    }

    private static BigInteger Cents(string value)=>BigInteger.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var number)&&number>=0?number:throw Invalid();
    private static InvalidDataException Invalid()=>new("Readable journal facts must contain exact nonnegative integer cents with equal debit and credit totals.");
}
