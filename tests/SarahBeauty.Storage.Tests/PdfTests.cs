using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SarahBeauty.Storage.Tests;

public sealed class PdfTests
{
    private static DocumentVersion Quote(BusinessFixture f,bool manyLines=false)
    {
        var booking=QuoteTests.Booking(f);
        if(manyLines)
        {
            for(int i=0;i<90;i++)booking=BookingRules.AddCharge(booking,$"Fictional service {i+1}: a longer description that must wrap within the service column","Custom",1,100);
            booking=new BookingBook(f.Store).Save(f.Owner,booking with{Charges=booking.Charges.Select(c=>c with{TaxRate=0m}).ToArray()});
        }
        var quotes=new QuoteBook(f.Store);
        var draft=quotes.SaveDraft(f.Owner,quotes.FromBooking(f.Owner,booking) with{Terms="Fictional terms for a TEST document. Sending is manual."});
        return quotes.Issue(f.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    [Fact]
    public void RenderedPdfContainsClientAmountsAndExcludesPrivateFields()
    {
        using var f=new BusinessFixture();
        var quote=Quote(f);
        var artifacts=new DocumentArtifacts(f.Store);
        string path=artifacts.Render(f.Owner,quote.Id);
        using var pdf=PdfDocument.Open(path);
        string text=string.Join("\n",pdf.GetPages().Select(p=>ContentOrderTextExtractor.GetText(p)));
        Assert.Contains("Fictional client",text);
        Assert.Contains("1,456.60",text);
        Assert.Contains("TEST",text);
        Assert.DoesNotContain("PRIVATE-",text);
        string hash=artifacts.Get(f.Owner,quote.Id).Sha256!;
        Assert.Equal(path,artifacts.Render(f.Owner,quote.Id));
        Assert.Equal(hash,artifacts.Get(f.Owner,quote.Id).Sha256);
        Assert.Equal("Succeeded",artifacts.Get(f.Owner,quote.Id).Status);
    }

    [Fact]
    public void MultiPageOutputRepeatsTestLabelAndIncludesTheLastService()
    {
        using var f=new BusinessFixture();
        var quote=Quote(f,manyLines:true);
        string path=new DocumentArtifacts(f.Store).Render(f.Owner,quote.Id);
        using var pdf=PdfDocument.Open(path);
        Assert.True(pdf.NumberOfPages>1);
        Assert.All(pdf.GetPages(),p=>Assert.Contains("TEST",p.Text));
        string text=string.Join("\n",pdf.GetPages().Select(p=>ContentOrderTextExtractor.GetText(p)));
        Assert.Contains("Fictional service 90",text);
        Assert.Contains("1,546.60",text);
    }

    [Fact]
    public void FailedRenderCanBeRetriedWithoutReissuingTheQuote()
    {
        using var f=new BusinessFixture();
        var quote=Quote(f);
        var artifacts=new DocumentArtifacts(f.Store);
        string blocker=Path.Combine(f.Files.Folder,"Documents");
        File.WriteAllText(blocker,"Block the managed document directory.");
        Assert.ThrowsAny<IOException>(()=>artifacts.Render(f.Owner,quote.Id));
        Assert.Equal("Failed",artifacts.Get(f.Owner,quote.Id).Status);
        File.Delete(blocker);
        Assert.True(File.Exists(artifacts.Render(f.Owner,quote.Id)));
        Assert.Equal(1L,TestStore.Scalar(f.Files.DatabasePath,"SELECT COUNT(*) FROM DocumentVersions;"));
    }

    [Fact]
    public void ChangedOrUnsafeSavedPdfIsRejectedWithoutRewritingTheIssuedArtifact()
    {
        using var f=new BusinessFixture();
        var quote=Quote(f);
        var artifacts=new DocumentArtifacts(f.Store);
        string path=artifacts.Render(f.Owner,quote.Id);
        string expected=artifacts.Get(f.Owner,quote.Id).Sha256!;
        File.AppendAllText(path,"Changed after issuance");
        Assert.Throws<InvalidOperationException>(()=>artifacts.VerifiedPath(f.Owner,quote.Id));
        Assert.Throws<InvalidOperationException>(()=>artifacts.Render(f.Owner,quote.Id));
        Assert.Equal(expected,artifacts.Get(f.Owner,quote.Id).Sha256);
        f.Files.Execute("UPDATE DocumentArtifacts SET RelativePath='../outside.pdf';");
        Assert.Throws<InvalidOperationException>(()=>artifacts.VerifiedPath(f.Owner,quote.Id));
    }
}
