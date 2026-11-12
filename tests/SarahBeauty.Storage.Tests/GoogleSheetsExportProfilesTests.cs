using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsExportProfilesTests
{
    private static ReadableExportSelection Selection=>new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));
    [Fact]
    public void ExplicitFixedDatesAndAutomaticChoiceReopenWithoutGoogleOrBackgroundOwnerRenewal()
    {
        using var f=new BusinessFixture();var profiles=new GoogleSheetsExportProfiles(f.Store);Assert.Null(profiles.Current(f.Owner));long watermark=Watermark(f);
        var profile=profiles.Save(f.Owner,new(Selection,true,15,0));Assert.Equal(watermark+1,Watermark(f));Assert.Equal(profile,profiles.Save(f.Owner,new(Selection,true,15,1)));Assert.Equal(watermark+1,Watermark(f));
        var reopened=new BusinessStore(f.Store.DatabasePath);Assert.Equal(profile,new GoogleSheetsExportProfiles(reopened).Current(f.Owner));f.Owner.Lock();
        Assert.Equal(profile,reopened.Read(db=>GoogleSheetsProfileRecords.Read(db,profile.BusinessId,reopened.InstanceId)));reopened.Read(db=>{new GoogleSheetsExportProfiles(reopened).Require(db,profile);return true;});Assert.Equal(DateTimeOffset.MinValue,f.Owner.ExpiresAt);Assert.Equal(watermark+1,Watermark(f));
        f.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");var restored=new BusinessStore(f.Store.DatabasePath);Assert.Null(restored.Read(db=>GoogleSheetsProfileRecords.Read(db,profile.BusinessId,restored.InstanceId)));
    }
    [Fact]
    public void StaleForeignAndLockedOwnersCannotChangeReportDates()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var profiles=new GoogleSheetsExportProfiles(f.Store);profiles.Save(f.Owner,new(Selection,false,15,0));long watermark=Watermark(f);
        Assert.Throws<ArgumentException>(()=>profiles.Save(f.Owner,new(Selection,true,15,0)));Assert.Throws<UnauthorizedAccessException>(()=>profiles.Save(foreign.Owner,new(Selection,true,15,0)));f.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>profiles.Save(f.Owner,new(Selection,true,15,1)));Assert.Equal(watermark,Watermark(f));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(10081)]
    public void UnsupportedIntervalsCannotBeSaved(int minutes)
    {
        using var f=new BusinessFixture();long watermark=Watermark(f);Assert.Throws<ArgumentException>(()=>new GoogleSheetsExportProfiles(f.Store).Save(f.Owner,new(Selection,true,minutes,0)));Assert.Equal(watermark,Watermark(f));
    }
    [Fact]
    public void InvalidDateOrderCannotBeSaved()
    {
        using var f=new BusinessFixture();long watermark=Watermark(f);Assert.Throws<ArgumentException>(()=>new GoogleSheetsExportProfiles(f.Store).Save(f.Owner,new(Selection with{EventFrom=Selection.PlanningAsOf.AddDays(-1)},true,15,0)));Assert.Equal(watermark,Watermark(f));
    }
    [Theory]
    [InlineData("cancel")]
    [InlineData("instance")]
    public void WriterWaitChangesPreventSavingTheProfile(string change)
    {
        using var f=new BusinessFixture();using var cancellation=new CancellationTokenSource();f.Store.BeforeTransactionReservation=()=>{f.Store.BeforeTransactionReservation=null;if(change=="cancel")cancellation.Cancel();else f.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");};
        Assert.NotNull(Record.Exception(()=>new GoogleSheetsExportProfiles(f.Store).Save(f.Owner,new(Selection,true,15,0),cancellation.Token)));Assert.Equal(0L,Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT count(*) FROM AppMetadata WHERE Key GLOB 'GoogleSheetsProfile/*';")));
    }
    [Fact]
    public void AutomaticWithdrawalAndInactiveConfirmingOwnerInvalidateTheScheduleWithoutGoogle()
    {
        using var f=new BusinessFixture();var profiles=new GoogleSheetsExportProfiles(f.Store);var profile=profiles.Save(f.Owner,new(Selection,true,15,0));var disabled=profiles.Save(f.Owner,new(Selection,false,15,1));
        Assert.Throws<GoogleConnectionFailure>(()=>f.Store.Read(db=>{profiles.Require(db,profile);return true;}));Assert.Throws<GoogleConnectionFailure>(()=>f.Store.Read(db=>{profiles.Require(db,disabled);return true;}));
        var enabled=profiles.Save(f.Owner,new(Selection,true,15,2));f.Files.Execute($"UPDATE Owners SET Active=0 WHERE Id='{f.Owner.OwnerId}';");Assert.Throws<GoogleConnectionFailure>(()=>f.Store.Read(db=>{profiles.Require(db,enabled);return true;}));
    }
    private static long Watermark(BusinessFixture f)=>Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
}
