using System.Globalization;
using System.Numerics;
using System.Text.Json;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableExportTableTests
{
    [Fact]
    public void ExplicitContactColumnsKeepPrivateFieldsAuthenticationAndFuturePropertiesOut()
    {
        using var f=new LedgerFixture();const string secret="Fictional-private-export-sentinel";var saved=new CustomerBook(f.Business.Store).Save(f.Business.Owner,new Customer{FullName="=Fictional literal formula-like name",Phone="+1 (555) 010-1234",Email="fictional@example.invalid",BillingName="Fictional billing",BillingAddress="Fictional street",PrivateNotes=secret,Sensitivities=secret,MakeupPreferences=secret,PermissionScope=secret,PermissionNotes=secret,PhotoPermission="Granted"});
        f.Business.Store.Transaction(db=>db.Execute("UPDATE Customers SET DataJson=json_set(DataJson,'$.FuturePrivateField',$secret) WHERE Id=$id;",("$secret",secret),("$id",saved.Id)));
        using var snapshot=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var tables=ReadableContactTables.Build(snapshot,new());string output=JsonSerializer.Serialize(tables);Assert.DoesNotContain(secret,output);Assert.DoesNotContain("Hash",string.Join(",",tables.SelectMany(t=>t.Columns)));Assert.DoesNotContain("Salt",output);Assert.DoesNotContain(BusinessFixture.Phrase,output);Assert.Equal(4,tables.Length);var customers=tables.Single(t=>t.Name=="Customers");var row=customers.Rows.Single(r=>r[0]==saved.Id);Assert.Equal(saved.FullName,row[3]);Assert.Equal(2,tables.Single(t=>t.Name=="Owners").Rows.Count);Assert.Equal("Granted",tables.Single(t=>t.Name=="Customer permission status").Rows.Single(r=>r[1]==saved.Id)[4]);Assert.All(tables,t=>{Assert.Equal(snapshot.Source.GenerationId,t.GenerationId);Assert.Equal(snapshot.Source.Watermark,t.Watermark);});
    }
    [Fact]
    public void IntegerPrecisionDecimalsDatesAndFormulaLikeTextRemainExplicitLiteralStrings()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var before=CultureInfo.CurrentCulture;try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");var builder=new ReadableTableBuilder(source.Source,new(),"Literal facts","MaximumCents","MinimumCents","ExactTotal","Rate","Date","Unknown","Literal");builder.Add(long.MaxValue,long.MinValue,BigInteger.Parse("18446744073709551614"),1.2500m,new DateOnly(2026,11,11),null,"=IMPORTXML(\"fictional\")");var table=builder.Freeze();Assert.Equal(new[]{"9223372036854775807","-9223372036854775808","18446744073709551614","1.25","2026-11-11","","=IMPORTXML(\"fictional\")"},table.Rows.Single());Assert.Throws<InvalidOperationException>(()=>builder.Add(1));Assert.Throws<NotSupportedException>(()=>((IList<string>)table.Rows[0])[0]="Must not change");
        }finally{CultureInfo.CurrentCulture=before;}
    }
    [Theory]
    [InlineData("cells")]
    [InlineData("bytes")]
    [InlineData("characters")]
    [InlineData("object")]
    [InlineData("binary")]
    [InlineData("floating-point")]
    public void UnsupportedOrOversizedFactsFailInsteadOfTruncatingOrDumpingRecords(string defect)
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var budget=defect switch{"cells"=>new ReadableExportBudget(maximumCells:1),"bytes"=>new ReadableExportBudget(maximumBytes:5),"characters"=>new ReadableExportBudget(maximumCellCharacters:4),_=>new ReadableExportBudget()};var builder=new ReadableTableBuilder(source.Source,budget,"Facts","Fact");object value=defect switch{"object"=>new{PrivateNotes="Fictional private"},"binary"=>new byte[]{1,2,3},"floating-point"=>1.25d,_=>"Fictional text"};Assert.Throws<InvalidDataException>(()=>builder.Add(value));
    }
    [Fact]
    public void StableSourceAndOrderingProduceTheSameTableHashesWhileLaterRowsStayOutsideTheGeneration()
    {
        using var f=new LedgerFixture();using var captured=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var first=ReadableContactTables.Build(captured,new());var added=new CustomerBook(f.Business.Store).Save(f.Business.Owner,new Customer{FullName="Fictional later customer"});var second=ReadableContactTables.Build(captured,new());Assert.Equal(first.Select(t=>t.Sha256),second.Select(t=>t.Sha256));Assert.DoesNotContain(second.Single(t=>t.Name=="Customers").Rows,r=>r[0]==added.Id);using var later=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);var latest=ReadableContactTables.Build(later,new()).Single(t=>t.Name=="Customers");Assert.Contains(latest.Rows,r=>r[0]==added.Id);Assert.Equal(second.Single(t=>t.Name=="Customers").Rows.Count+1,latest.Rows.Count);
    }
}
