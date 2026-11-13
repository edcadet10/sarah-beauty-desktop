using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsPreviewTests
{
    private static ReadableExportSelection Selection=>new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
    [Fact]
    public void LocalPreviewIncludesVerifiedCountsAndExactJournalFactsWithoutGoogleJobsOrStagingFiles()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,12345));long watermark=Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        var preview=new GoogleSheetsPreview(f.Business.Store).Build(f.Business.Owner,Selection);Assert.Equal(165,preview.Tables.Count);Assert.Equal(Selection,preview.Selection);Assert.Equal("TEST",preview.Mode);Assert.Equal(watermark,preview.SourceWatermark);
        Assert.Equal(preview.Tables.Sum(table=>(long)table.DataRows),preview.DataRows);Assert.Equal(preview.Tables.Sum(table=>table.Cells),preview.SourceCells);Assert.Equal(preview.SourceCells+64,preview.PlannedWorkbookCells);
        Assert.Equal(2,preview.JournalLines);Assert.Equal("12345",preview.JournalDebitCents);Assert.Equal(preview.JournalDebitCents,preview.JournalCreditCents);
        Assert.Contains(preview.Notices,notice=>notice.Contains("unknown percentages remain unconfirmed",StringComparison.Ordinal));Assert.Contains(preview.Notices,notice=>notice.Contains("does not filter every record table",StringComparison.Ordinal));
        Assert.Contains(preview.Notices,notice=>notice.Contains("Verified retained quote and invoice decisions are included",StringComparison.Ordinal));
        Assert.False(Directory.Exists(new ReadableGenerationPaths(f.Business.Store.DatabasePath).Root));Assert.Equal(0L,Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT count(*) FROM AppMetadata WHERE Key GLOB 'GoogleSheets*';")));
        Assert.Equal(watermark,Convert.ToInt64(TestStore.Scalar(f.Business.Store.DatabasePath,"SELECT Watermark FROM Business;")));Assert.DoesNotContain(f.Business.Store.DatabasePath,JsonSerializer.Serialize(preview));
    }
    [Theory]
    [InlineData("lock")]
    [InlineData("cancel")]
    [InlineData("instance")]
    public void LateAccessOrCancellationChangesPreventReturningThePreview(string change)
    {
        using var f=new LedgerFixture();using var cancellation=new CancellationTokenSource();var service=new GoogleSheetsPreview(f.Business.Store)
        {
            BeforeReturn=()=>{if(change=="lock")f.Business.Owner.Lock();else if(change=="cancel")cancellation.Cancel();else f.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");}
        };
        var error=Record.Exception(()=>service.Build(f.Business.Owner,Selection,cancellation.Token));Assert.NotNull(error);
        if(change=="cancel")Assert.IsAssignableFrom<OperationCanceledException>(error);else Assert.IsAssignableFrom<UnauthorizedAccessException>(error);Assert.False(Directory.Exists(new ReadableGenerationPaths(f.Business.Store.DatabasePath).Root));
    }
    [Fact]
    public void ForeignAndLockedOwnersCannotBuildThePreview()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var service=new GoogleSheetsPreview(f.Business.Store);
        Assert.Throws<UnauthorizedAccessException>(()=>service.Build(foreign.Owner,Selection));f.Business.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>service.Build(f.Business.Owner,Selection));
    }
}
