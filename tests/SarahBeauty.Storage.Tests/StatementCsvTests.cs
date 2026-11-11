using SarahBeauty.Business.Banking;

namespace SarahBeauty.Storage.Tests;

public sealed class StatementCsvTests
{
    [Fact]
    public void QuotedCommasMultilineDescriptionsAndIdenticalTransactionsAreRetained()
    {
        string csv="Date,Description,Amount,Reference\n2027-01-10,\"Fictional, deposit\",\"$1,234.56\",ref-1\n2027-01-11,\"Fictional\nfee\",(25.00),ref-2\n2027-01-11,\"Fictional\nfee\",(25.00),ref-3";
        var source=StatementCsv.Parse(csv,new(ReferenceColumn:"Reference"));
        Assert.Equal(3,source.Rows.Length);
        Assert.Equal(123456,source.InflowCents);
        Assert.Equal(5000,source.OutflowCents);
        Assert.Equal(118456,source.NetCents);
        Assert.Contains("Fictional, deposit",source.Rows[0].Description);
        Assert.Contains('\n',source.Rows[1].Description);
        Assert.Equal(new[]{2,3,5},source.Rows.Select(r=>r.SourceRow));
        Assert.Equal(source.ContentHash,StatementCsv.Parse(csv,new(ReferenceColumn:"Reference")).ContentHash);
        Assert.NotEqual(source.ContentHash,StatementCsv.Parse(csv+"\n",new(ReferenceColumn:"Reference")).ContentHash);
    }

    [Fact]
    public void ExplicitDateAndDebitCreditMappingDetermineSignsWithoutGuessing()
    {
        var mapping=new StatementCsvMapping(AmountColumn:"",DebitColumn:"Out",CreditColumn:"In",DateFormat:"dd/MM/yyyy",Delimiter:";");
        var source=StatementCsv.Parse("Date;Description;Out;In\n02/03/2027;Fictional payment;25.50;\n03/03/2027;Fictional deposit;;100.00",mapping);
        Assert.Equal(new DateOnly(2027,3,2),source.Rows[0].Date);
        Assert.Equal(-2550,source.Rows[0].SignedCents);
        Assert.Equal(7450,source.NetCents);
        var inverted=StatementCsv.Parse("Date,Description,Amount\n2027-01-10,Fictional expense,25.00",new(NegateAmounts:true));
        Assert.Equal(-2500,inverted.Rows[0].SignedCents);
    }

    [Theory]
    [InlineData("Date,Description,Amount\n2027-01-10,Fictional,1.001")]
    [InlineData("Date,Description,Amount\n01/02/2027,Fictional,1.00")]
    [InlineData("Date,Description,Amount\n2027-01-10,Fictional,0")]
    [InlineData("Date,Description,Amount\n2027-01-10,Fictional,\"1,23.45\"")]
    [InlineData("Date,Description,Amount\n2027-01-10,\"Unclosed,1.00")]
    [InlineData("Date,Date,Amount\n2027-01-10,Fictional,1.00")]
    [InlineData("Date,Description,Amount\n2027-01-10,Fictional,1.00,extra")]
    public void AmbiguousOrMalformedInputFailsBeforeAnyImport(string csv)=>Assert.Throws<ArgumentException>(()=>StatementCsv.Parse(csv,new()));

    [Fact]
    public void ContradictoryMappingAndMissingReliableReferencesAreRejected()
    {
        const string csv="Date,Description,Amount\n2027-01-10,Fictional,1.00";
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse(csv,new(DebitColumn:"Debit")));
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse(csv,new(DateColumn:"Description")));
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse(csv,new(ReliableReferences:true)));
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse("Date,Description,Out,In\n2027-01-10,Fictional,1,2",new(AmountColumn:"",DebitColumn:"Out",CreditColumn:"In")));
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse("Date,Description,Amount,Ref\n2027-01-10,Fictional,1,",new(ReferenceColumn:"Ref",ReliableReferences:true)));
    }

    [Fact]
    public void OversizedContentAndAmountsAndAggregateOverflowFailClearly()
    {
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse(new string('x',StatementCsv.MaximumBytes+1),new()));
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse("Date,Description,Amount\n2027-01-10,Fictional,92233720368547758.08",new()));
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse("Date,Description,Amount\n2027-01-10,Fictional,92233720368547758.07\n2027-01-10,Fictional,0.01",new()));
        string csv="Date,Description,Amount\n"+string.Join('\n',Enumerable.Repeat("2027-01-10,Fictional,0.01",StatementCsv.MaximumRows+1));
        Assert.Throws<ArgumentException>(()=>StatementCsv.Parse(csv,new()));
    }
}
