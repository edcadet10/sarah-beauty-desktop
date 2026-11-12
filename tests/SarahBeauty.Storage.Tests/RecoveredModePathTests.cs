using SarahBeauty.Business.Recovery;
using SarahBeauty.Business.Storage;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SarahBeauty.Storage.Tests;

public sealed class RecoveredModePathTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public void ReadingAMissingSelectionDoesNotCreateFoldersOrLockFiles(bool existingRoot)
    {
        using var f=new TestStore();string root=existingRoot?f.Folder:Path.Combine(f.Folder,"Missing");Assert.Null(ActiveStorePointer.Read(new(root)));if(existingRoot)Assert.Empty(Directory.EnumerateFileSystemEntries(root));else Assert.False(Directory.Exists(root));
    }
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public void ModePathsAreExplicitAndDoNotCreateData(BusinessMode mode)
    {
        using var f=new TestStore();string root=Path.Combine(f.Folder,"Missing");var paths=new RecoveredStorePaths(root,mode);string label=mode==BusinessMode.Test?"test":"live";Assert.Equal(Path.Combine(root,"active-"+label+"-store.json"),paths.Pointer);Assert.Equal(Path.Combine(root,".active-"+label+"-store.lock"),paths.Lock);Assert.Equal(mode==BusinessMode.Test?Path.Combine(root,"sarahbeauty.db"):Path.Combine(root,"Primary","sarahbeauty.db"),paths.DefaultDatabase);Assert.False(Directory.Exists(root));
    }
    [Theory]
    [InlineData(BusinessMode.Test)][InlineData(BusinessMode.Live)]
    public void SelectionRoundTripsOnlyInItsOwnMode(BusinessMode mode)
    {
        using var f=new TestStore();var paths=new RecoveredStorePaths(f.Folder,mode);var value=Selection(paths);byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);ActiveStorePointer.Write(paths.Pointer,bytes);Assert.Equal(value,ActiveStorePointer.Read(paths));var other=new RecoveredStorePaths(f.Folder,mode==BusinessMode.Test?BusinessMode.Live:BusinessMode.Test);File.WriteAllBytes(other.Pointer,bytes);Assert.Throws<InvalidDataException>(()=>ActiveStorePointer.Read(other));Assert.Equal(bytes,File.ReadAllBytes(paths.Pointer));Assert.Equal(bytes,File.ReadAllBytes(other.Pointer));
    }
    [Fact]
    public void LegacySelectionWithoutModeRemainsTestOnlyAndIsNotRewritten()
    {
        using var f=new TestStore();var test=new RecoveredStorePaths(f.Folder);var node=JsonSerializer.SerializeToNode(Selection(test))!.AsObject();node.Remove("Mode");string json=node.ToJsonString();File.WriteAllText(test.Pointer,json);Assert.Equal("TEST",ActiveStorePointer.Read(test)!.Mode);Assert.Equal(json,File.ReadAllText(test.Pointer));var live=new RecoveredStorePaths(f.Folder,BusinessMode.Live);File.WriteAllText(live.Pointer,json);Assert.Throws<InvalidDataException>(()=>ActiveStorePointer.Read(live));
    }
    [Theory]
    [InlineData("")][InlineData("test")][InlineData("live")]
    public void NonCanonicalSelectionModeIsRejected(string mode)
    {
        using var f=new TestStore();var paths=new RecoveredStorePaths(f.Folder);ActiveStorePointer.Write(paths.Pointer,JsonSerializer.SerializeToUtf8Bytes(Selection(paths) with{Mode=mode}));Assert.Throws<InvalidDataException>(()=>ActiveStorePointer.Read(paths));
    }
    [Theory]
    [InlineData("request")][InlineData("store")][InlineData("instance")]
    public void EmptyIdentitiesCannotSelectAWorkingStore(string identity)
    {
        using var f=new TestStore();var paths=new RecoveredStorePaths(f.Folder);var value=Selection(paths);value=identity switch{"request"=>value with{RequestId=Guid.Empty.ToString()},"store"=>value with{StoreId=Guid.Empty.ToString()},_=>value with{InstanceId=Guid.Empty.ToString("N")}};ActiveStorePointer.Write(paths.Pointer,JsonSerializer.SerializeToUtf8Bytes(value));Assert.Throws<InvalidDataException>(()=>ActiveStorePointer.Read(paths));
    }
    [Fact]
    public void UnknownModeIsRejectedBeforeResolvingPaths()=>Assert.Throws<ArgumentOutOfRangeException>(()=>new RecoveredStorePaths(".",(BusinessMode)99));
    private static ActiveStoreSelection Selection(RecoveredStorePaths paths)
    {
        string request=Guid.NewGuid().ToString();return new(1,request,Guid.NewGuid().ToString(),Guid.NewGuid().ToString("N"),paths.Relative(request),DateTimeOffset.UtcNow.ToString("O"),BusinessModes.StoredName(paths.Mode));
    }
}
