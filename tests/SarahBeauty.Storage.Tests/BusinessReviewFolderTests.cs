using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class BusinessReviewFolderTests
{
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public void FictionalReviewModesResolveSeparateFoldersWithoutCreatingData(BusinessMode mode)
    {
        using var f=new TestStore();string local=Path.Combine(f.Folder,"Local"),review=Path.Combine(f.Folder,"Review");Assert.Equal(Path.Combine(review,mode==BusinessMode.Test?"Test":"Live"),BusinessReviewFolders.Resolve(local,review,mode));Assert.Empty(Directory.EnumerateFileSystemEntries(f.Folder));
    }
    [Theory]
    [InlineData("parent")][InlineData("test")][InlineData("live")][InlineData("live-child")][InlineData("preview")][InlineData("preview-child")]
    public void ReviewCannotOverlapWorkingDataOrAnExistingPreview(string selection)
    {
        using var f=new TestStore();string local=Path.Combine(f.Folder,"Local"),preview=Path.Combine(f.Folder,"Preview");string live=BusinessDataFolders.Resolve(local,BusinessMode.Live);string review=selection switch{"parent"=>local,"test"=>BusinessDataFolders.Resolve(local,BusinessMode.Test),"live"=>live,"live-child"=>Path.Combine(live,"Child"),"preview"=>preview,_=>Path.Combine(preview,"Child")};Assert.Throws<ArgumentException>(()=>BusinessReviewFolders.Resolve(local,review,BusinessMode.Live,preview));Assert.Empty(Directory.EnumerateFileSystemEntries(f.Folder));
    }
    [Theory]
    [InlineData("")][InlineData("relative-review")]
    public void UnspecifiedOrRelativeReviewRootsAreRejected(string root)
    {
        using var f=new TestStore();Assert.Throws<ArgumentException>(()=>BusinessReviewFolders.Resolve(f.Folder,root,BusinessMode.Test));Assert.Empty(Directory.EnumerateFileSystemEntries(f.Folder));
    }
    [Fact]
    public void UnknownModeCannotResolveReviewData()
    {
        using var f=new TestStore();Assert.Throws<ArgumentOutOfRangeException>(()=>BusinessReviewFolders.Resolve(f.Folder,Path.Combine(f.Folder,"Review"),(BusinessMode)99));Assert.Empty(Directory.EnumerateFileSystemEntries(f.Folder));
    }
}
