using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class RecoveryStageCleanupTests
{
    [Fact]
    public async Task CleanupWaitsForTheKnownOutputLeaseAndLeavesNoPublishedFile()
    {
        using var fixture=new TestStore();string destination=Path.Combine(fixture.Folder,"not-published.bin");
        var stage=new RecoveryOutputStage(destination);string directory=Path.GetDirectoryName(stage.Path)!;
        Task cleanup;bool returnedWhileLocked;
        using(var held=new FileStream(stage.Path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
        {
            held.Write([4,5,6]);held.Flush();cleanup=stage.DisposeAsync().AsTask();returnedWhileLocked=cleanup.IsCompleted;
        }
        await cleanup;
        Assert.False(returnedWhileLocked,"Cleanup must not report completion while its output is still exclusively leased.");
        Assert.False(File.Exists(destination));Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task CleanupRemovesOnlyTheOwnedOutputAndPreservesAnUnexpectedFile()
    {
        using var fixture=new TestStore();string destination=Path.Combine(fixture.Folder,"not-published.bin");
        var stage=new RecoveryOutputStage(destination);string unexpected=Path.Combine(Path.GetDirectoryName(stage.Path)!,"unrelated.txt");
        await File.WriteAllBytesAsync(stage.Path,[1,2,3]);await File.WriteAllBytesAsync(unexpected,[8,9]);
        await stage.DisposeAsync();
        Assert.False(File.Exists(stage.Path));Assert.False(File.Exists(destination));Assert.Equal(new byte[]{8,9},await File.ReadAllBytesAsync(unexpected));
    }

    [Fact]
    public async Task AContinuedOutputLeaseReportsCleanupFailureWithoutPublishingOrChangingItsBytes()
    {
        using var fixture=new TestStore();string destination=Path.Combine(fixture.Folder,"not-published.bin");
        var stage=new RecoveryOutputStage(destination);
        using(var held=new FileStream(stage.Path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
        {
            held.Write([4,5,6]);held.Flush();
            var error=await Assert.ThrowsAsync<RecoveryFailure>(()=>stage.DisposeAsync().AsTask());
            Assert.Equal("TemporaryOutputCleanupFailed",error.Code);Assert.False(File.Exists(destination));
            held.Position=0;byte[] retained=new byte[3];held.ReadExactly(retained);Assert.Equal(new byte[]{4,5,6},retained);
        }
        await stage.DisposeAsync();Assert.False(Directory.Exists(Path.GetDirectoryName(stage.Path)!));
    }
}
