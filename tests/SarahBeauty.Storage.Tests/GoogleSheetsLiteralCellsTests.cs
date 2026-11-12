using System.Text.Json;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsLiteralCellsTests
{
    [Theory]
    [InlineData("=SUM(1,2)")]
    [InlineData("9223372036854775807")]
    [InlineData("0")]
    [InlineData("1.25")]
    [InlineData("2027-01-10")]
    [InlineData("é\n\tFictional")]
    [InlineData("")]
    public void ExactEnteredAndEffectiveStringValuesRemainLiteral(string expected)
    {
        var cell=JsonSerializer.SerializeToElement(new{userEnteredValue=new{stringValue=expected},effectiveValue=new{stringValue=expected}});
        GoogleSheetsLiteralCells.Require(cell,expected);
    }

    [Fact]
    public void AFormulaWithTheSameCalculatedTextCannotPassLiteralVerification()
    {
        const string expected="=SUM(1,2)";
        var cell=JsonSerializer.SerializeToElement(new{userEnteredValue=new{formulaValue="=\"=SUM(1,2)\""},effectiveValue=new{stringValue=expected}});
        Assert.Equal(expected,cell.GetProperty("effectiveValue").GetProperty("stringValue").GetString());
        var error=Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsLiteralCells.Require(cell,expected));
        Assert.Equal("ReadableCellMismatch",error.Code);Assert.DoesNotContain(expected,error.Message);
    }

    [Theory]
    [InlineData("number")]
    [InlineData("bool")]
    [InlineData("error")]
    [InlineData("ambiguous")]
    [InlineData("null")]
    [InlineData("missing effective")]
    [InlineData("changed")]
    public void ConvertedMalformedOrChangedValuesAreRejected(string defect)
    {
        const string expected="Fictional expected literal";
        object? entered=defect switch
        {
            "number"=>new{numberValue=1234},"bool"=>new{boolValue=true},"error"=>new{errorValue=new{type="ERROR"}},
            "ambiguous"=>new{stringValue=expected,formulaValue="=1"},"null"=>null,"changed"=>new{stringValue="Fictional changed"},_=>new{stringValue=expected}
        };
        var cell=defect=="missing effective"?JsonSerializer.SerializeToElement(new{userEnteredValue=entered}):
            JsonSerializer.SerializeToElement(new{userEnteredValue=entered,effectiveValue=new{stringValue=expected}});
        Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsLiteralCells.Require(cell,expected));
    }

    [Fact]
    public void OmittedBlankCellsAreAllowedWhileComputedOrExplicitNullCellsAreRejected()
    {
        GoogleSheetsLiteralCells.Require(null,"");GoogleSheetsLiteralCells.Require(JsonSerializer.SerializeToElement(new{}),"");
        GoogleSheetsLiteralCells.Require(JsonSerializer.SerializeToElement(new{userEnteredValue=new{},effectiveValue=new{}}),"");
        Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsLiteralCells.Require(null,"Fictional nonblank"));
        var calculated=JsonSerializer.SerializeToElement(new{effectiveValue=new{stringValue="Fictional calculated"}});
        Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsLiteralCells.Require(calculated,""));
        Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsLiteralCells.Require(JsonSerializer.SerializeToElement<object?>(null),""));
    }

    [Fact]
    public void DuplicateOrUnexpectedFieldsCannotHideAnotherEnteredValue()
    {
        using var duplicate=JsonDocument.Parse("{\"userEnteredValue\":{\"formulaValue\":\"=1\"},\"userEnteredValue\":{},\"effectiveValue\":{}}");
        Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsLiteralCells.Require(duplicate.RootElement,""));
        var unexpected=JsonSerializer.SerializeToElement(new{formulaValue="=1"});
        Assert.Throws<GoogleConnectionFailure>(()=>GoogleSheetsLiteralCells.Require(unexpected,""));
    }
}
