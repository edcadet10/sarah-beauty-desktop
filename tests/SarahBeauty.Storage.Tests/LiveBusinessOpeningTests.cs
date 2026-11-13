using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class LiveBusinessOpeningTests
{
    [Fact]
    public void MissingPrimaryIsDetectedWithoutCreatingAnyFiles()
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live");var businesses=new LiveBusinesses(root);Assert.False(businesses.Exists);Assert.Throws<FileNotFoundException>(()=>businesses.Open());Assert.False(Directory.Exists(root));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void OccupiedButMissingDatabaseNeverCreatesAReplacement(bool file)
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live"),primary=Path.Combine(root,"Primary");Directory.CreateDirectory(root);if(file)File.WriteAllText(primary,"Fictional sentinel");else Directory.CreateDirectory(primary);var businesses=new LiveBusinesses(root);Assert.True(businesses.Exists);Assert.Throws<FileNotFoundException>(()=>businesses.Open());if(file)Assert.Equal("Fictional sentinel",File.ReadAllText(primary));else Assert.Empty(Directory.EnumerateFileSystemEntries(primary));
    }
    [Fact]
    public void CurrentProfileReviewsMayRemainIncompleteSoTheOwnerCanFinishThem()
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live");var businesses=new LiveBusinesses(root);var created=Create(businesses);var profile=new BusinessProfiles(created.Store).Current(created.Owner)!;
        new BusinessProfiles(created.Store).Confirm(created.Owner,new(1,profile.Profile with{Currency=null,TimeZoneId=null,BooksStartDate=null,NewBusinessOnlyConfirmed=false},"Fictional setup reopened for review"),BusinessFixture.Phrase,Guid.NewGuid().ToString());created.Owner.Lock();Assert.True(businesses.Exists);var opened=businesses.Open();var owner=new OwnerAccess(opened).SignIn(created.Owner.OwnerId,BusinessFixture.Phrase);Assert.Equal(BusinessMode.Live,owner.Mode);Assert.Null(new BusinessProfiles(opened).Current(owner)!.Profile.Currency);
    }
    [Fact]
    public void TestDataInTheLiveLocationIsRejectedWithoutChangingItsBytes()
    {
        using var f=new BusinessFixture();string root=Path.Combine(f.Files.Folder,"SeparateLive"),primary=Path.Combine(root,"Primary");Directory.CreateDirectory(primary);string database=Path.Combine(primary,"sarahbeauty.db");File.Copy(f.Store.DatabasePath,database);byte[] original=File.ReadAllBytes(database);Assert.Throws<InvalidOperationException>(()=>new LiveBusinesses(root).Open());Assert.Equal(original,File.ReadAllBytes(database));
    }
    [Theory]
    [InlineData("empty")][InlineData("no-profile")][InlineData("owner")][InlineData("profile-hash")][InlineData("foreign-key")][InlineData("prepared")][InlineData("retired")]
    public void InconsistentOrNonActiveLiveDataIsNeverPresentedForNormalSignIn(string change)
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Live");var businesses=new LiveBusinesses(root);var created=Create(businesses);created.Owner.Lock();string database=created.Store.DatabasePath;
        if(change=="empty"){File.WriteAllBytes(database,[]);Assert.Throws<InvalidOperationException>(()=>businesses.Open());Assert.Empty(File.ReadAllBytes(database));return;}
        Execute(database,change switch
        {
            "no-profile"=>"DROP TRIGGER BusinessProfileVersions_NoDelete; DELETE FROM BusinessProfileVersions;",
            "owner"=>"UPDATE Owners SET Active=0 WHERE rowid=(SELECT min(rowid) FROM Owners);",
            "profile-hash"=>"DROP TRIGGER BusinessProfileVersions_NoUpdate; UPDATE BusinessProfileVersions SET SnapshotHash='AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA';",
            "foreign-key"=>"PRAGMA foreign_keys=OFF; UPDATE Audit SET OwnerId='missing';",
            "prepared"=>"UPDATE AppMetadata SET Value='Prepared' WHERE Key='WritableInstanceState';",
            _=>"UPDATE AppMetadata SET Value='Retired' WHERE Key='WritableInstanceState';"
        });byte[] before=File.ReadAllBytes(database);if(change is "prepared" or "retired")Assert.Throws<UnauthorizedAccessException>(()=>businesses.Open());else Assert.Throws<InvalidDataException>(()=>businesses.Open());Assert.Equal(before,File.ReadAllBytes(database));
    }
    private static void Execute(string path,string sql)
    {
        using var connection=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=path,Mode=Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite,Pooling=false}.ToString());connection.Open();using var command=connection.CreateCommand();command.CommandText=sql;command.ExecuteNonQuery();
    }
    private static CreatedLiveBusiness Create(LiveBusinesses businesses)=>businesses.Create(new(){DisplayName="Fictional LIVE opening",Currency="USD",TimeZoneId="UTC",BooksStartDate=new DateOnly(2026,11,12),NewBusinessOnlyConfirmed=true},"Alex",BusinessFixture.Phrase,"Morgan",BusinessFixture.Phrase,"Fictional reviewed clean start");
}
