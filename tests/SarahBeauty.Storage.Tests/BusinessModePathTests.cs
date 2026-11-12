using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class BusinessModePathTests
{
    [Fact]
    public void DefaultModePathsStaySeparateAndAGetDoesNotCreateEitherStore()
    {
        using var files=new TestStore();string local=Path.Combine(files.Folder,"Fictional local application data");var test=BusinessDataFolders.Resolve(local,BusinessMode.Test);var live=BusinessDataFolders.Resolve(local,BusinessMode.Live);Assert.Equal(Path.Combine(local,"SarahBeautyDesktop","Test"),test);Assert.Equal(Path.Combine(local,"SarahBeautyDesktop","Live"),live);Assert.NotEqual(test,live);Assert.False(Directory.Exists(local));
    }
    [Fact]
    public void ExplicitTestOverrideCannotRedirectLiveAndStillWorksWithoutALocalDataFolder()
    {
        using var files=new TestStore();string local=Path.Combine(files.Folder,"Fictional local data"),preview=Path.Combine(files.Folder,"Fictional preview");Assert.Equal(preview,BusinessDataFolders.Resolve(local,BusinessMode.Test,preview));Assert.Equal(preview,BusinessDataFolders.Resolve("",BusinessMode.Test,preview));Assert.Equal(Path.Combine(local,"SarahBeautyDesktop","Live"),BusinessDataFolders.Resolve(local,BusinessMode.Live,preview));Assert.Throws<InvalidOperationException>(()=>BusinessDataFolders.Resolve("",BusinessMode.Live,preview));Assert.False(Directory.Exists(preview));
    }
    [Theory]
    [InlineData("same")][InlineData("parent")][InlineData("child")][InlineData("case")]
    public void TestOverrideCannotLexicallyOverlapTheReservedLiveDirectory(string relation)
    {
        using var files=new TestStore();string local=Path.Combine(files.Folder,"Fictional local data"),live=BusinessDataFolders.Resolve(local,BusinessMode.Live);string preview=relation switch{"parent"=>Path.GetDirectoryName(live)!,"child"=>Path.Combine(live,"Fictional nested preview"),"case"=>live.ToUpperInvariant(),_=>live};Assert.Throws<ArgumentException>(()=>BusinessDataFolders.Resolve(local,BusinessMode.Test,preview));Assert.False(Directory.Exists(local));
    }
    [Theory]
    [InlineData("test")][InlineData("LIVE ")][InlineData("")][InlineData("OTHER")]
    public void StoredModeParsingDoesNotInventOrNormalizeAMode(string mode)=>Assert.Throws<ArgumentException>(()=>BusinessModes.FromStoredName(mode));
    [Fact]
    public void KnownModesRoundTripAndUndefinedEnumsAreRejected()
    {
        foreach(var mode in new[]{BusinessMode.Test,BusinessMode.Live})Assert.Equal(mode,BusinessModes.FromStoredName(BusinessModes.StoredName(mode)));Assert.Throws<ArgumentOutOfRangeException>(()=>BusinessModes.StoredName((BusinessMode)99));Assert.Throws<ArgumentOutOfRangeException>(()=>BusinessDataFolders.Resolve("",(BusinessMode)99,"Fictional preview"));
    }
}
