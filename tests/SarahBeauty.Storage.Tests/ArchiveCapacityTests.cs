using SarahBeauty.Business.Attachments;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class ArchiveCapacityTests
{
    [Fact]
    public void EstimateIncludesTheLogicalDatabaseAndEveryManagedVersionWithoutCreatingSnapshots()
    {
        using var f=new LedgerFixture();var invoice=ApplicationTests.Invoice(f,10000);new DocumentArtifacts(f.Business.Store).Render(f.Business.Owner,invoice.Id);
        var attachments=new ManagedAttachments(f.Business.Store);var first=attachments.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());
        attachments.Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business,"Fictional replacement.pdf"),ManagedAttachmentTests.Draft with{AttachmentId=first.Version.AttachmentId,ExpectedVersion=1},Guid.NewGuid().ToString());
        long watermark=(long)TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;")!;var estimate=new ArchiveCapacity(f.Business.Store){AvailableBytes=_=>long.MaxValue}.Estimate(f.Business.Owner,f.Business.Files.Folder);
        Assert.Equal(3,estimate.ManagedFileCount);Assert.True(estimate.SourceBytes>0);Assert.True(estimate.ArchiveAllowanceBytes>estimate.SourceBytes);Assert.Equal(estimate.ArchiveAllowanceBytes*3,Assert.Single(estimate.Volumes).RequiredBytes);Assert.False(estimate.HasKnownShortage);
        Assert.Equal(watermark,TestStore.Scalar(f.Business.Files.DatabasePath,"SELECT Watermark FROM Business;"));Assert.False(Directory.Exists(Path.Combine(f.Business.Files.Folder,"RecoveryStaging")));
        using var captured=new ArchiveSources(f.Business.Store).Capture(f.Business.Owner);Assert.Equal(captured.Manifest.Files.Sum(file=>file.ByteLength),estimate.SourceBytes);
    }

    [Fact]
    public void AShortageIsDifferentFromUnknownOrSufficientSpace()
    {
        using var f=new LedgerFixture();var capacity=new ArchiveCapacity(f.Business.Store){AvailableBytes=_=>0};Assert.True(capacity.Estimate(f.Business.Owner,f.Business.Files.Folder).HasKnownShortage);
        capacity.AvailableBytes=_=>null;var unknown=capacity.Estimate(f.Business.Owner,f.Business.Files.Folder);Assert.False(unknown.HasKnownShortage);Assert.Null(Assert.Single(unknown.Volumes).AvailableBytes);
        capacity.AvailableBytes=_=>long.MaxValue;Assert.False(capacity.Estimate(f.Business.Owner,f.Business.Files.Folder).HasKnownShortage);
    }

    [Fact]
    public void ForeignAccessMissingFoldersAndMissingManagedFilesDoNotReceiveAUsableEstimate()
    {
        using var f=new LedgerFixture();using var foreign=new BusinessFixture();var capacity=new ArchiveCapacity(f.Business.Store);
        Assert.Throws<UnauthorizedAccessException>(()=>capacity.Estimate(foreign.Owner,f.Business.Files.Folder));Assert.Throws<DirectoryNotFoundException>(()=>capacity.Estimate(f.Business.Owner,Path.Combine(f.Business.Files.Folder,"missing")));
        var file=new ManagedAttachments(f.Business.Store).Import(f.Business.Owner,ManagedAttachmentTests.Pdf(f.Business),ManagedAttachmentTests.Draft,Guid.NewGuid().ToString());File.Delete(Path.Combine(f.Business.Files.Folder,file.Version.RelativePath));
        Assert.Throws<FileNotFoundException>(()=>capacity.Estimate(f.Business.Owner,f.Business.Files.Folder));
    }
}
