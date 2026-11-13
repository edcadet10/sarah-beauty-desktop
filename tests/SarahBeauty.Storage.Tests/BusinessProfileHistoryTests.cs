using System.Text.Json;
using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Setup;

namespace SarahBeauty.Storage.Tests;

public sealed class BusinessProfileHistoryTests
{
    [Fact]
    public void ACorruptPrivateProfileCannotBecomeAVerifiedArchiveSource()
    {
        using var f=new BusinessFixture();Seed(f);f.Files.Execute("DROP TRIGGER BusinessProfileVersions_NoUpdate; UPDATE BusinessProfileVersions SET SnapshotJson=replace(SnapshotJson,'Fictional','Changed');");Assert.Throws<InvalidDataException>(()=>{using var source=new ArchiveSources(f.Store).Capture(f.Owner);});
    }
    [Fact]
    public void EmptyAndRevisionedHistoryRemainPrivateProtectedAndReadOnly()
    {
        using var f=new BusinessFixture();using var foreign=new BusinessFixture();var book=new BusinessProfiles(f.Store);Assert.Null(book.Current(f.Owner));Assert.Empty(book.History(f.Owner));var first=Seed(f);var second=Seed(f,2,first.Profile with{DisplayName="Fictional renamed beauty",LegalName="Fictional private legal entity"});
        var values=book.History(f.Owner);Assert.Equal(new[]{first,second},values);Assert.Equal(second,book.Current(f.Owner));Assert.Throws<NotSupportedException>(()=>((IList<BusinessProfileVersion>)values).Add(first));Assert.Throws<UnauthorizedAccessException>(()=>book.History(foreign.Owner));using var cancellation=new CancellationTokenSource();cancellation.Cancel();Assert.Throws<OperationCanceledException>(()=>book.History(f.Owner,cancellation.Token));f.Owner.Lock();Assert.Throws<UnauthorizedAccessException>(()=>book.History(f.Owner));
    }
    [Theory]
    [InlineData("payload")][InlineData("revision")][InlineData("mode")][InlineData("unknown field")][InlineData("display")]
    public void IndexedCanonicalAndStoreIdentityMismatchesRejectHistoryAndArchive(string defect)
    {
        using var f=new BusinessFixture();var value=Seed(f);f.Files.Execute("DROP TRIGGER BusinessProfileVersions_NoUpdate;");
        if(defect=="payload")f.Files.Execute("UPDATE BusinessProfileVersions SET PayloadHash='"+new string('A',64)+"';");
        else if(defect=="revision")f.Files.Execute("UPDATE BusinessProfileVersions SET Revision=2;");
        else if(defect=="display")f.Files.Execute("UPDATE Business SET DisplayName='Fictional contradictory name';");
        else
        {
            string json=defect=="mode"?JsonSerializer.Serialize(value with{Mode="LIVE"}):JsonSerializer.Serialize(value)[..^1]+",\"FuturePrivateField\":true}";
            f.Store.Transaction(db=>db.Execute("UPDATE BusinessProfileVersions SET SnapshotJson=$json,SnapshotHash=$hash;",("$json",json),("$hash",BusinessProfileCodec.Hash(json))));
        }
        Assert.Throws<InvalidDataException>(()=>new BusinessProfiles(f.Store).History(f.Owner));Assert.Throws<InvalidDataException>(()=>{using var source=new ArchiveSources(f.Store).Capture(f.Owner);});
    }
    [Fact]
    public void RevisionGapsAreRejectedAndInactiveHistoricalActorsRemainIdentifiable()
    {
        using(var gap=new BusinessFixture()){Seed(gap,2);Assert.Throws<InvalidDataException>(()=>new BusinessProfiles(gap.Store).History(gap.Owner));}
        using var f=new BusinessFixture();var value=Seed(f);string other=(string)TestStore.Scalar(f.Store.DatabasePath,"SELECT Id FROM Owners WHERE Name='Morgan';")!;var owner=new OwnerAccess(f.Store).SignIn(other,BusinessFixture.Phrase);f.Store.Transaction(db=>db.Execute("UPDATE Owners SET Active=0 WHERE Id=$id;",("$id",f.Owner.OwnerId)));Assert.Equal(value,Assert.Single(new BusinessProfiles(f.Store).History(owner)));
    }
    [Fact]
    public void CapturedEvidenceStaysOnItsOriginalVersionAndForgedDescriptorsAreRejected()
    {
        using var f=new BusinessFixture();var attachments=new ManagedAttachments(f.Store);var file=attachments.Import(f.Owner,ManagedAttachmentTests.Pdf(f),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());var evidence=ManagedEvidenceFile.From(file);var value=Seed(f,profile:new(){DisplayName="Fictional Beauty",Formation=new(BusinessSetupState.Confirmed,"Fictional formation evidence review",evidence)});
        var replacement=attachments.Import(f.Owner,ManagedAttachmentTests.Pdf(f,"Fictional newer private evidence.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=file.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());Assert.Equal(2,replacement.Version.Number);File.Delete(Path.Combine(f.Files.Folder,file.Version.RelativePath));Assert.Equal(value,Assert.Single(new BusinessProfiles(f.Store).History(f.Owner)));
        var changed=value with{Profile=value.Profile with{Formation=value.Profile.Formation with{Evidence=evidence with{Sha256=new('A',64)}}}};string json=JsonSerializer.Serialize(changed);f.Files.Execute("DROP TRIGGER BusinessProfileVersions_NoUpdate;");f.Store.Transaction(db=>db.Execute("UPDATE BusinessProfileVersions SET SnapshotJson=$json,SnapshotHash=$hash,PayloadHash=$payload;",("$json",json),("$hash",BusinessProfileCodec.Hash(json)),("$payload",BusinessProfileCodec.Payload(BusinessProfileCodec.Change(changed)))));Assert.Throws<InvalidDataException>(()=>new BusinessProfiles(f.Store).History(f.Owner));
    }
    [Fact]
    public void ReadableProjectionOmitsPrivateIdentityAndSetupReasons()
    {
        using var f=new BusinessFixture();const string reason="PRIVATE fictional business-setup review";Seed(f,profile:new(){DisplayName="Fictional Beauty",LegalName="PRIVATE fictional legal identity",Ein="00-0000000",EinReview=new(BusinessSetupState.Confirmed,reason)});using var source=new ReadableExportSnapshots(f.Store).Capture(f.Owner);string json=JsonSerializer.Serialize(ReadableExportGeneration.Build(source,DocumentDecisionExportContractTests.Selection));foreach(string privateValue in new[]{reason,"PRIVATE fictional legal identity","00-0000000"})Assert.DoesNotContain(privateValue,json);
    }
    internal static BusinessProfileVersion Seed(BusinessFixture f,long revision=1,BusinessProfile? profile=null)
    {
        profile??=new(){DisplayName="Fictional Beauty"};var value=new BusinessProfileVersion(1,Guid.NewGuid().ToString(),f.Owner.BusinessId,revision,Guid.NewGuid().ToString(),"TEST",BusinessProfileRules.Normalize(profile),"Fictional private profile history fixture",f.Owner.OwnerId,"2027-01-01T12:00:00.0000000+00:00");string json=JsonSerializer.Serialize(value);
        f.Store.Transaction(db=>db.Execute("INSERT INTO BusinessProfileVersions(Id,BusinessId,Revision,RequestId,SnapshotJson,SnapshotHash,PayloadHash,RecordedByOwnerId,RecordedAtUtc) VALUES($id,$business,$revision,$request,$json,$hash,$payload,$owner,$at); UPDATE Business SET DisplayName=$name WHERE Id=$business;",("$id",value.Id),("$business",value.BusinessId),("$revision",value.Revision),("$request",value.RequestId),("$json",json),("$hash",BusinessProfileCodec.Hash(json)),("$payload",BusinessProfileCodec.Payload(BusinessProfileCodec.Change(value))),("$owner",value.RecordedByOwnerId),("$at",value.RecordedAtUtc),("$name",value.Profile.DisplayName)));return value;
    }
}
