using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;
using SarahBeauty_Desktop.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class StoreInstanceTests
{
    [Fact]
    public void RestoredInstanceCannotReuseOriginalSessionEvenWithSameLogicalIdentity()
    {
        using var f=new BusinessFixture();using var copied=new TestStore();DatabaseBackup.CreateVerifiedSnapshotAt(f.Files.DatabasePath,copied.DatabasePath);
        copied.Execute($"INSERT INTO AppMetadata(Key,Value) VALUES('WritableInstanceId','{Guid.NewGuid():N}') ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;");
        copied.Execute("INSERT INTO AppMetadata(Key,Value) VALUES('WritableInstanceState','Active') ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;");
        var restored=new BusinessStore(copied.DatabasePath);Assert.Equal(f.Store.StoreId,restored.StoreId);
        Assert.Throws<UnauthorizedAccessException>(()=>restored.BusinessName(f.Owner));
        var fresh=new OwnerAccess(restored).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);Assert.Equal("Fictional Beauty",restored.BusinessName(fresh));Assert.Equal("Fictional Beauty",f.Store.BusinessName(f.Owner));
    }

    [Fact]
    public void ChangedGenerationInvalidatesOldHandlesIncludingOwnerlessTransactions()
    {
        using var f=new BusinessFixture();string original=f.Store.InstanceId;f.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';");
        Assert.Throws<UnauthorizedAccessException>(()=>f.Store.Transaction(db=>db.Execute("INSERT INTO AppMetadata VALUES('FictionalUnexpectedWrite','No');")));
        Assert.Throws<UnauthorizedAccessException>(()=>new OwnerAccess(f.Store).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
        var reopened=new BusinessStore(f.Files.DatabasePath);Assert.NotEqual(original,reopened.InstanceId);Assert.Throws<UnauthorizedAccessException>(()=>reopened.BusinessName(f.Owner));
        Assert.Throws<UnauthorizedAccessException>(()=>new OwnerAccess(reopened).Confirm(f.Owner,BusinessFixture.Phrase));
        var fresh=new OwnerAccess(reopened).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);Assert.Equal("Fictional Beauty",reopened.BusinessName(fresh));Assert.Null(TestStore.Scalar(f.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='FictionalUnexpectedWrite';"));
    }

    [Fact]
    public void RetirementBetweenOpeningAndWriterReservationPreventsTheWriteDelegate()
    {
        using var f=new BusinessFixture();bool invoked=false;f.Store.BeforeTransactionReservation=()=>f.Files.Execute("UPDATE AppMetadata SET Value='Retired' WHERE Key='WritableInstanceState';");
        Assert.Throws<UnauthorizedAccessException>(()=>f.Store.Transaction(db=>{invoked=true;return db.Execute("INSERT INTO AppMetadata VALUES('FictionalUnexpectedWrite','No');");}));
        Assert.False(invoked);Assert.Null(TestStore.Scalar(f.Files.DatabasePath,"SELECT Value FROM AppMetadata WHERE Key='FictionalUnexpectedWrite';"));
    }

    [Fact]
    public void AGenerationChangedDuringCredentialVerificationCannotIssueANewSession()
    {
        using var f=new BusinessFixture();var access=new OwnerAccess(f.Store){AfterCredentialsRead=()=>f.Files.Execute($"UPDATE AppMetadata SET Value='{Guid.NewGuid():N}' WHERE Key='WritableInstanceId';")};
        Assert.Throws<UnauthorizedAccessException>(()=>access.SignIn(f.Owner.OwnerId,BusinessFixture.Phrase));
        var reopened=new BusinessStore(f.Files.DatabasePath);Assert.Equal("Fictional Beauty",reopened.BusinessName(new OwnerAccess(reopened).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase)));
    }

    [Fact]
    public void RetiredInstanceRejectsExistingAndNewAccessWithoutChangingStoredBusinessData()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(SarahBeauty.Business.Finance.FinancialKind.Receipt,10000));f.Business.Files.Execute("UPDATE AppMetadata SET Value='Retired' WHERE Key='WritableInstanceState';");string before=f.Business.Files.Hash();
        Assert.Throws<UnauthorizedAccessException>(()=>f.Book.Posted(f.Business.Owner));Assert.Throws<UnauthorizedAccessException>(()=>f.Post(f.Draft(SarahBeauty.Business.Finance.FinancialKind.Expense,1000)));
        Assert.Throws<UnauthorizedAccessException>(()=>new BusinessStore(f.Business.Files.DatabasePath));Assert.Equal(before,f.Business.Files.Hash());Assert.Equal(1L,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT COUNT(*) FROM Movements;"));
    }

    [Theory]
    [InlineData("invalid-id")]
    [InlineData("empty-id")]
    [InlineData("unknown-state")]
    [InlineData("missing-id")]
    public void MissingOrInvalidInstanceMetadataCannotBeSilentlyReplaced(string defect)
    {
        using var f=new BusinessFixture();string sql=defect switch
        {
            "invalid-id"=>"UPDATE AppMetadata SET Value='invalid' WHERE Key='WritableInstanceId';",
            "empty-id"=>"UPDATE AppMetadata SET Value='00000000000000000000000000000000' WHERE Key='WritableInstanceId';",
            "unknown-state"=>"UPDATE AppMetadata SET Value='Unknown' WHERE Key='WritableInstanceState';",
            _=>"DELETE FROM AppMetadata WHERE Key='WritableInstanceId';"
        };f.Files.Execute(sql);string before=f.Files.Hash();Assert.Throws<InvalidDataException>(()=>new BusinessStore(f.Files.DatabasePath));Assert.Equal(before,f.Files.Hash());Assert.Empty(f.Files.Snapshots());
    }

    [Fact]
    public void VersionThirtyOneUpgradeCreatesInstanceIdentityAndPreservesThePriorSnapshot()
    {
        using var f=new BusinessFixture();string logical=f.Store.StoreId;f.Files.Execute("DELETE FROM AppMetadata WHERE Key IN ('WritableInstanceId','WritableInstanceState'); PRAGMA user_version=31;");
        var upgraded=new BusinessStore(f.Files.DatabasePath);Assert.Equal(logical,upgraded.StoreId);Assert.Equal((long)BusinessSchema.Version,TestStore.Scalar(f.Files.DatabasePath,"PRAGMA user_version;"));Assert.NotEqual(f.Store.InstanceId,upgraded.InstanceId);
        string snapshot=Assert.Single(f.Files.Snapshots());Assert.Equal(31L,TestStore.Scalar(snapshot,"PRAGMA user_version;"));Assert.Null(TestStore.Scalar(snapshot,"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceId';"));
        Assert.Throws<UnauthorizedAccessException>(()=>upgraded.BusinessName(f.Owner));var fresh=new OwnerAccess(upgraded).SignIn(f.Owner.OwnerId,BusinessFixture.Phrase);Assert.Equal("Fictional Beauty",upgraded.BusinessName(fresh));Assert.Equal(upgraded.InstanceId,new BusinessStore(f.Files.DatabasePath).InstanceId);
    }

    [Fact]
    public async Task EarlierCompleteSchemaThirtyOneArchivesRemainReviewableWithoutUpgradingTheirStagedDatabase()
    {
        using var f=new BusinessFixture();using var legacy=new TestStore();var older=await EarlierArchive(f,legacy);
        using(var review=await new ArchiveRestores().StageAsync(older.Archive,legacy.Folder,older.Key.ProtectedIdentity,BusinessFixture.Phrase))
        {
            Assert.Equal(31,review.Manifest.SchemaVersion);Assert.Equal(31L,TestStore.Scalar(review.FilePath("sarahbeauty.db"),"PRAGMA user_version;"));Assert.Null(TestStore.Scalar(review.FilePath("sarahbeauty.db"),"SELECT Value FROM AppMetadata WHERE Key='WritableInstanceId';"));
        }
        Assert.Equal("Fictional Beauty",f.Store.BusinessName(f.Owner));
    }
    internal static async Task<(RecoveryMaterial Key,string Archive)> EarlierArchive(BusinessFixture f,TestStore legacy,int schemaVersion=31,BusinessMode mode=BusinessMode.Test)
    {
        if(schemaVersion is <31 or >39)throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        DatabaseBackup.CreateVerifiedSnapshotAt(f.Files.DatabasePath,legacy.DatabasePath);
        var newer=new List<string>{"ClientDraftDiscards"};if(schemaVersion<39)newer.Add("BusinessProfileVersions");if(schemaVersion<36)newer.Add("DocumentDecisions");if(schemaVersion<33)newer.AddRange(["OwnerInterestShares","OwnerInterestVersions"]);
        foreach(string table in newer)if(Convert.ToInt64(TestStore.Scalar(legacy.DatabasePath,"SELECT count(*) FROM "+table+";"))!=0)throw new InvalidOperationException("An earlier archive fixture cannot discard newer business history.");
        legacy.Execute(string.Join(" ",newer.Select(table=>"DROP TABLE "+table+";"))+" DROP INDEX ClientDraftRecordScope;"+(schemaVersion<36?" DROP INDEX DocumentDecisionDocumentScope; DROP INDEX DocumentDecisionVersionScope; DROP INDEX DocumentDecisionEvidenceScope;":"")+(schemaVersion<33?" DROP INDEX OwnerInterestOwnerScope;":"")+(schemaVersion<32?" DELETE FROM AppMetadata WHERE Key IN ('WritableInstanceId','WritableInstanceState');":"")+$" PRAGMA user_version={schemaVersion};");
        string storedMode=BusinessModes.StoredName(mode);legacy.Execute("UPDATE AppMetadata SET Value='"+storedMode+"' WHERE Key='StoreMode';");
        ArchiveDatabaseFacts facts;using(var connection=legacy.Open())facts=ArchiveDatabaseChecks.Read(new DbWork(connection));
        byte[] bytes=File.ReadAllBytes(legacy.DatabasePath);var file=new ArchiveFile("sarahbeauty.db","Database",bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));
        var manifest=new ArchiveManifest(1,f.Owner.BusinessId,"Fictional Beauty",f.Store.StoreId,storedMode,schemaVersion,(long)TestStore.Scalar(legacy.DatabasePath,"SELECT Watermark FROM Business;")!,DateTimeOffset.UtcNow.ToString("O"),"Fictional earlier archive",[file],facts);
        string package=Path.Combine(legacy.Folder,"fictional-older.zip"),encrypted=Path.Combine(legacy.Folder,"fictional-older.age");
        using(var zip=ZipFile.Open(package,ZipArchiveMode.Create))
        {
            using(var output=zip.CreateEntry("manifest.json").Open())output.Write(JsonSerializer.SerializeToUtf8Bytes(manifest));using(var output=zip.CreateEntry("sarahbeauty.db").Open())output.Write(bytes);
        }
        var cipher=new RecoveryCipher();var key=await cipher.CreateAsync(BusinessFixture.Phrase);await cipher.EncryptAsync(package,encrypted,key.Recipient);
        return(key,encrypted);
    }
}
