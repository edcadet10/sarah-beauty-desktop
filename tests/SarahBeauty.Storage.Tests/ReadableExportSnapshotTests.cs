using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableExportSnapshotTests
{
    private static void Clean(LedgerFixture f){string parent=Path.Combine(f.Business.Files.Folder,"RecoveryStaging");if(Directory.Exists(parent))Assert.Empty(Directory.EnumerateFileSystemEntries(parent));}
    [Fact]
    public void OneReadOnlyGenerationRetainsEarlierWalFactsWhileNewLocalChangesContinue()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,10000));long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;var snapshots=new ReadableExportSnapshots(f.Business.Store){FailurePoint=point=>{if(point=="Database captured")f.Post(f.Draft(FinancialKind.Expense,2500));}};
        using(var captured=snapshots.Capture(f.Business.Owner))
        {
            Assert.Equal(watermark,captured.Source.Watermark);Assert.Equal(1,captured.Source.Facts.MovementCount);Assert.Equal(1L,captured.Read(db=>db.Scalar("SELECT COUNT(*) FROM Movements;")));Assert.Equal(2L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));Assert.Equal("TEST",captured.Source.Mode);
            Assert.Throws<SqliteException>(()=>captured.Read(db=>db.Execute("UPDATE Business SET DisplayName='Must not write';")));
        }
        Clean(f);Assert.True((long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!>watermark);
    }
    [Fact]
    public async Task SavedSheetPermissionCapturesWithoutUnlockingOrTouchingBusinessState()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();await f.Create();await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,true,1));var permit=f.Settings.Authorize(GoogleOutputKind.SheetsExport);long watermark=f.Watermark;f.Ledger.Business.Owner.Lock();var expiry=f.Ledger.Business.Owner.ExpiresAt;var snapshots=new ReadableExportSnapshots(f.Ledger.Business.Store);
        using(var captured=snapshots.Capture(permit)){Assert.Equal(watermark,captured.Source.Watermark);Assert.Equal(f.Ledger.Business.Store.InstanceId,captured.Source.InstanceId);Assert.Equal(expiry,f.Ledger.Business.Owner.ExpiresAt);Assert.Equal(watermark,f.Watermark);}Clean(f.Ledger);
    }
    [Theory]
    [InlineData("withdrawn")]
    [InlineData("cancelled")]
    [InlineData("deactivated")]
    [InlineData("instance")]
    public async Task LateCancellationOrChangedAuthorityDisposesThePrivateSnapshot(string change)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();await f.Create();await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,true,1));var permit=f.Settings.Authorize(GoogleOutputKind.SheetsExport);using var cancellation=new CancellationTokenSource();var snapshots=new ReadableExportSnapshots(f.Ledger.Business.Store){FailurePoint=point=>
        {
            if(point!="Database captured")return;
            if(change=="withdrawn")f.Settings.SaveAsync(f.Ledger.Business.Owner,new(false,false,2)).GetAwaiter().GetResult();
            if(change=="cancelled")cancellation.Cancel();
            if(change=="deactivated")f.Ledger.Business.Store.Transaction(db=>db.Execute("UPDATE Owners SET Active=0 WHERE Id=$id;",("$id",f.Ledger.Business.Owner.OwnerId)));
            if(change=="instance")f.Ledger.Business.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");
        }};
        if(change=="cancelled")Assert.Throws<OperationCanceledException>(()=>snapshots.Capture(permit,cancellation.Token));
        else if(change=="instance")Assert.Throws<UnauthorizedAccessException>(()=>snapshots.Capture(permit,cancellation.Token));
        else Assert.Equal("GoogleOutputPermissionUnavailable",Assert.Throws<GoogleConnectionFailure>(()=>snapshots.Capture(permit,cancellation.Token)).Code);Clean(f.Ledger);
    }
    [Fact]
    public async Task DriveOnlyPermissionCannotReadExportDataAndDisposalPreservesUnexpectedFiles()
    {
        using var f=new GoogleOutputSettingsTests.Fixture();await f.Create();await f.Settings.SaveAsync(f.Ledger.Business.Owner,new(true,false,1));var snapshots=new ReadableExportSnapshots(f.Ledger.Business.Store);Assert.Throws<UnauthorizedAccessException>(()=>snapshots.Capture(f.Settings.Authorize(GoogleOutputKind.DriveArchive)));Clean(f.Ledger);
        var captured=snapshots.Capture(f.Ledger.Business.Owner);string directory=Directory.EnumerateDirectories(Path.Combine(f.Ledger.Business.Files.Folder,"RecoveryStaging")).Single(),unexpected=Path.Combine(directory,"fictional-unexpected.txt");File.WriteAllText(unexpected,"Fictional file that cleanup does not own");captured.Dispose();captured.Dispose();Assert.Throws<ObjectDisposedException>(()=>captured.Read(db=>db.Scalar("SELECT 1;")));Assert.True(File.Exists(unexpected));Assert.False(File.Exists(Path.Combine(directory,"sarahbeauty.db")));
    }
}
