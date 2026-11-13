using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ModeInitializationTests
{
    [Fact]
    public void MissingLegacyModeCannotBePromotedToLiveButRetainsTestUpgradeCompatibility()
    {
        using var files=new TestStore();files.CreateLegacy();string id=(string)TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreId';")!;var note=TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='FictionalNote';");byte[] hash=SHA256.HashData(File.ReadAllBytes(files.DatabasePath));var error=Assert.Throws<InvalidOperationException>(()=>LocalDatabase.InitializeDatabaseAt(files.DatabasePath,BusinessMode.Live));Assert.Contains("LIVE store",error.Message);Assert.Equal(hash,SHA256.HashData(File.ReadAllBytes(files.DatabasePath)));Assert.Empty(files.Snapshots());
        var test=LocalDatabase.InitializeTestDatabaseAt(files.DatabasePath);Assert.Equal(id,test.StoreId);Assert.Equal("TEST",TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));Assert.Equal(note,TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='FictionalNote';"));Assert.Equal(0L,TestStore.Scalar(files.DatabasePath,"SELECT count(*) FROM Business;"));Assert.Single(files.Snapshots());
    }
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public void ExplicitEmptyInitializationRetainsModeIdentityAndCreatesNoBusinessFacts(BusinessMode mode)
    {
        using var files=new TestStore();var first=LocalDatabase.InitializeDatabaseAt(files.DatabasePath,mode);var reopened=LocalDatabase.InitializeDatabaseAt(files.DatabasePath,mode,allowCreate:false);Assert.Equal(first,reopened);Assert.Equal(BusinessModes.StoredName(mode),TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(files.DatabasePath,"PRAGMA user_version;"));foreach(string table in new[]{"Business","Owners","Audit","Movements","BusinessProfileVersions"})Assert.Equal(0L,TestStore.Scalar(files.DatabasePath,"SELECT count(*) FROM "+table+";"));Assert.Empty(files.Snapshots());
    }
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public void WrongExpectedModeCannotConvertTheStoreAndPreservesItsBytes(BusinessMode actual)
    {
        using var files=new TestStore();var original=LocalDatabase.InitializeDatabaseAt(files.DatabasePath,actual);byte[] hash=SHA256.HashData(File.ReadAllBytes(files.DatabasePath));var wrong=actual==BusinessMode.Test?BusinessMode.Live:BusinessMode.Test;Assert.Throws<InvalidOperationException>(()=>LocalDatabase.InitializeDatabaseAt(files.DatabasePath,wrong));Assert.Equal(hash,SHA256.HashData(File.ReadAllBytes(files.DatabasePath)));Assert.Equal(original.StoreId,TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreId';"));Assert.Empty(files.Snapshots());
    }
    [Fact]
    public void ExistingOnlyOpenDoesNotCreateAMissingOrEmptyBusiness()
    {
        using var files=new TestStore();Assert.Throws<SqliteException>(()=>LocalDatabase.InitializeDatabaseAt(files.DatabasePath,BusinessMode.Live,allowCreate:false));Assert.False(File.Exists(files.DatabasePath));File.WriteAllBytes(files.DatabasePath,[]);Assert.Throws<InvalidOperationException>(()=>LocalDatabase.InitializeDatabaseAt(files.DatabasePath,BusinessMode.Live,allowCreate:false));Assert.Empty(File.ReadAllBytes(files.DatabasePath));
    }
    [Fact]
    public void UndefinedModeIsRejectedBeforeCreatingAFile()
    {
        using var files=new TestStore();Assert.Throws<ArgumentOutOfRangeException>(()=>LocalDatabase.InitializeDatabaseAt(files.DatabasePath,(BusinessMode)99));Assert.False(File.Exists(files.DatabasePath));
    }
    [Fact]
    public void ExplicitLiveUpgradePreservesModeIdentityAndAnOpenablePriorSnapshot()
    {
        using var files=new TestStore();var first=LocalDatabase.InitializeDatabaseAt(files.DatabasePath,BusinessMode.Live);files.Execute("DROP TABLE BusinessProfileVersions; PRAGMA user_version=38;");var upgraded=LocalDatabase.InitializeDatabaseAt(files.DatabasePath,BusinessMode.Live,allowCreate:false);Assert.Equal(first.StoreId,upgraded.StoreId);Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(files.DatabasePath,"PRAGMA user_version;"));Assert.Equal("LIVE",TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));string path=Assert.Single(files.Snapshots());Assert.Equal(38L,TestStore.Scalar(path,"PRAGMA user_version;"));Assert.Equal("LIVE",TestStore.Scalar(path,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));Assert.Equal(0L,TestStore.Scalar(path,"SELECT count(*) FROM sqlite_schema WHERE name='BusinessProfileVersions';"));
    }
    [Fact]
    public async Task CompetingModesCannotBothInitializeTheSameEmptyFile()
    {
        using var files=new TestStore();var results=await Task.WhenAll(new[]{BusinessMode.Test,BusinessMode.Live}.Select(mode=>Task.Run(()=>
        {
            try{LocalDatabase.InitializeDatabaseAt(files.DatabasePath,mode);return(mode,Success:true);}catch(InvalidOperationException){return(mode,Success:false);}
        })));var winner=Assert.Single(results,r=>r.Success);Assert.Equal(BusinessModes.StoredName(winner.mode),TestStore.Scalar(files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='StoreMode';"));Assert.Equal(0L,TestStore.Scalar(files.DatabasePath,"SELECT count(*) FROM Business;"));
    }
}
