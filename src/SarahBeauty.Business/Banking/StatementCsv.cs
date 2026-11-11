using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using SarahBeauty.Business.Rules;

namespace SarahBeauty.Business.Banking;

public static class StatementCsv
{
    public const int MaximumBytes=4*1024*1024;
    public const int MaximumRows=10000;
    public static readonly IReadOnlyList<string> DateFormats=Array.AsReadOnly(new[]{"yyyy-MM-dd","MM/dd/yyyy","M/d/yyyy","dd/MM/yyyy","d/M/yyyy"});

    public static StatementSource Parse(string text,StatementCsvMapping mapping)
    {
        if(string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text)>MaximumBytes)throw new ArgumentException("Use nonempty UTF-8 CSV contents of at most 4 MB.");
        ValidateMapping(mapping);
        using var parser=new TextFieldParser(new StringReader(text.TrimStart('\uFEFF')))
            {TextFieldType=FieldType.Delimited,Delimiters=[mapping.Delimiter],HasFieldsEnclosedInQuotes=true,TrimWhiteSpace=true};
        try
        {
            var header=Header(parser);
            var rows=ReadRows(parser,header,mapping);
            var result=new StatementSource(rows,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))),JsonSerializer.Serialize(mapping));
            _=result.InflowCents;_=result.OutflowCents;
            return result;
        }
        catch(MalformedLineException error){throw new ArgumentException($"CSV row {error.LineNumber} has invalid quoting or delimiters.");}
        catch(OverflowException){throw new ArgumentException("A statement amount or total exceeds the supported 64-bit cent range.");}
    }

    private static void ValidateMapping(StatementCsvMapping m)
    {
        bool signed=m.AmountColumn.Length>0;
        var columns=new[]{m.DateColumn,m.DescriptionColumn,m.AmountColumn,m.DebitColumn,m.CreditColumn,m.ReferenceColumn}.Where(c=>c.Length>0).ToList();
        if(!DateFormats.Contains(m.DateFormat) || m.Delimiter is not ("," or ";" or "\t") || m.DateColumn.Length==0 || m.DescriptionColumn.Length==0
            || signed && (m.DebitColumn.Length>0 || m.CreditColumn.Length>0) || !signed && (m.DebitColumn.Length==0 || m.CreditColumn.Length==0 || m.NegateAmounts)
            || columns.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=columns.Count || m.ReliableReferences && m.ReferenceColumn.Length==0)
            throw new ArgumentException("Map distinct date/description columns and either one signed amount column or separate debit/credit columns. Choose an explicit date format and supported delimiter.");
    }

    private static Dictionary<string,int> Header(TextFieldParser parser)
    {
        var fields=parser.ReadFields()??throw new ArgumentException("The CSV needs a header row.");
        if(fields.Length<3 || fields.Length>100 || fields.Any(string.IsNullOrWhiteSpace) || fields.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=fields.Length)
            throw new ArgumentException("Use 3 to 100 distinct, nonempty CSV column headers.");
        return fields.Select((name,index)=>(name,index)).ToDictionary(p=>p.name,p=>p.index,StringComparer.OrdinalIgnoreCase);
    }

    private static StatementRow[] ReadRows(TextFieldParser parser,Dictionary<string,int> header,StatementCsvMapping mapping)
    {
        var rows=new List<StatementRow>();
        while(!parser.EndOfData)
        {
            int number=checked((int)parser.LineNumber);
            var fields=parser.ReadFields()!;
            if(rows.Count>=MaximumRows)throw new ArgumentException("Import at most 10,000 transaction rows in one statement.");
            if(fields.Length!=header.Count)throw new ArgumentException($"CSV row {number} does not have the same number of fields as the header.");
            rows.Add(Row(fields,header,mapping,number));
        }
        if(rows.Count==0)throw new ArgumentException("The CSV contains no transaction rows.");
        return rows.ToArray();
    }

    private static StatementRow Row(string[] fields,Dictionary<string,int> header,StatementCsvMapping m,int number)
    {
        string Field(string name)=>name.Length==0?"":header.TryGetValue(name,out int index)?fields[index]:throw new ArgumentException($"The mapped CSV column '{name}' is missing.");
        if(!DateOnly.TryParseExact(Field(m.DateColumn),m.DateFormat,CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))
            throw new ArgumentException($"CSV row {number} does not match the selected date format {m.DateFormat}.");
        string description=Field(m.DescriptionColumn),reference=Field(m.ReferenceColumn);
        if(description.Length==0 || description.Length>2000 || reference.Length>200 || m.ReliableReferences && reference.Length==0)
            throw new ArgumentException($"CSV row {number} needs a description of up to 2,000 characters and a reference of up to 200 characters if used.");
        long cents=m.AmountColumn.Length>0?ReadAmount(Field(m.AmountColumn),false,number):SeparateAmounts(Field(m.DebitColumn),Field(m.CreditColumn),number);
        if(m.NegateAmounts)cents=checked(-cents);
        if(cents==0 || cents==long.MinValue)throw new ArgumentException($"CSV row {number} needs a nonzero amount within the supported positive/negative cent range.");
        return new(number,date,cents,description,reference,m.ReliableReferences);
    }

    private static long SeparateAmounts(string debitText,string creditText,int number)
    {
        long debit=ReadAmount(debitText,true,number),credit=ReadAmount(creditText,true,number);
        if(debit<0 || credit<0 || debit>0 && credit>0)throw new ArgumentException($"CSV row {number} needs one nonnegative debit or credit amount.");
        return checked(credit-debit);
    }

    private static long ReadAmount(string text,bool blankIsZero,int number)
    {
        if(blankIsZero && text.Length==0)return 0;
        if(!decimal.TryParse(text,NumberStyles.Currency,CultureInfo.GetCultureInfo("en-US"),out decimal dollars) || decimal.Round(dollars,2)!=dollars || !ValidGrouping(text))
            throw new ArgumentException($"CSV row {number} needs USD amounts with at most two decimal places.");
        return Money.Cents(dollars);
    }

    private static bool ValidGrouping(string text)
    {
        if(!text.Contains(','))return true;
        string number=text.Trim().Trim('(',')','$','+','-',' ');
        var parts=number.Split('.');
        var groups=parts[0].Split(',');
        return parts.Skip(1).All(p=>!p.Contains(',')) && groups[0].Length is >=1 and <=3
            && groups.Skip(1).All(g=>g.Length==3) && groups.All(g=>g.All(char.IsAsciiDigit));
    }
}
