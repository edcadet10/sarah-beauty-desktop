using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableGenerationFilesTests
{
    [Fact]
    public void FlushedFilesReloadThroughAFreshStoreAfterSnapshotDisposalWithoutOverwriting()
    {
        using var f=new LedgerFixture();f.Post(f.Draft(FinancialKind.Receipt,5000));var generation=Capture(f);f.Business.Owner.Lock();
        var files=new ReadableGenerationFiles(f.Business.Store);var artifact=files.Stage(generation);
        var reopened=new ReadableGenerationFiles(new BusinessStore(f.Business.Store.DatabasePath));var loaded=reopened.Load(artifact);
        Assert.Equal(JsonSerializer.Serialize(generation),JsonSerializer.Serialize(loaded));
        Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);Assert.Equal("5000",loaded.Manifest.Journal.DebitCents);
        Assert.Equal(generation.Manifest.Source.Watermark,f.Business.Store.Read(db=>Convert.ToInt64(db.Scalar("SELECT Watermark FROM Business WHERE Id=$id;",("$id",generation.Manifest.Source.BusinessId)))));
        Assert.Throws<IOException>(()=>files.Stage(generation));Assert.Equal(artifact.ManifestSha256,reopened.Load(artifact).ManifestSha256);
        Assert.Throws<InvalidDataException>(()=>reopened.Load(artifact with{ManifestSha256=new string('0',64)}));
        Assert.Throws<InvalidDataException>(()=>reopened.Load(artifact with{GenerationId="../outside"}));
        using var foreign=new LedgerFixture();Assert.Throws<UnauthorizedAccessException>(()=>new ReadableGenerationFiles(foreign.Business.Store).Stage(generation));
    }

    [Fact]
    public void FileResolutionStaysWithinGeneratedDirectoriesAndKnownIndexes()
    {
        using var f=new LedgerFixture();var paths=new ReadableGenerationPaths(f.Business.Store.DatabasePath);
        string outside=Path.GetDirectoryName(f.Business.Store.DatabasePath)!;
        Assert.Throws<InvalidDataException>(()=>paths.FilePath(outside,-1));
        Assert.Throws<InvalidDataException>(()=>paths.Generation(new string('0',32)));
        string directory=paths.Generation(Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(()=>paths.FilePath(directory,256));
        Assert.Throws<InvalidDataException>(()=>paths.FilePath(directory,-2));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("missing")]
    [InlineData("extra")]
    public void InvalidPublishedContentIsRejectedAndPreserved(string defect)
    {
        using var f=new LedgerFixture();var generation=Capture(f);var files=new ReadableGenerationFiles(f.Business.Store);var artifact=files.Stage(generation);
        var paths=new ReadableGenerationPaths(f.Business.Store.DatabasePath);string directory=paths.Generation(artifact.GenerationId);
        if(defect=="changed")File.AppendAllText(paths.FilePath(directory,0),"[\"Fictional changed row\"]\n");
        else if(defect=="missing")File.Delete(paths.FilePath(directory,0));
        else File.WriteAllText(Path.Combine(directory,"unrelated.txt"),"Fictional preserve this file");
        Assert.Throws<InvalidDataException>(()=>files.Load(artifact));Assert.True(Directory.Exists(directory));
        if(defect=="extra")Assert.Equal("Fictional preserve this file",File.ReadAllText(Path.Combine(directory,"unrelated.txt")));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("retire")]
    [InlineData("unknown file")]
    public void InterruptedStagesDoNotPublishAndOnlyCleanTheirOwnFiles(string defect)
    {
        using var f=new LedgerFixture();var generation=Capture(f);var paths=new ReadableGenerationPaths(f.Business.Store.DatabasePath);
        using var cancel=new CancellationTokenSource();var files=new ReadableGenerationFiles(f.Business.Store);
        files.FailurePoint=stage=>
        {
            if(defect=="unknown file"&&stage=="Files written")File.WriteAllText(Path.Combine(Directory.GetDirectories(paths.Root).Single(),"unrelated.txt"),"Fictional preserve this file");
            if(stage!="Before publish")return;
            if(defect=="cancel")cancel.Cancel();
            if(defect=="retire")f.Business.Store.Transaction(db=>db.Execute("UPDATE AppMetadata SET Value='Retired' WHERE Key='WritableInstanceState';"));
        };
        if(defect=="cancel")Assert.Throws<OperationCanceledException>(()=>files.Stage(generation,cancel.Token));
        else if(defect=="retire")Assert.Throws<UnauthorizedAccessException>(()=>files.Stage(generation));
        else Assert.Throws<InvalidDataException>(()=>files.Stage(generation));
        Assert.False(Directory.Exists(paths.Generation(generation.Manifest.Source.GenerationId)));
        if(defect=="unknown file")Assert.Equal("Fictional preserve this file",File.ReadAllText(Path.Combine(Directory.GetDirectories(paths.Root).Single(),"unrelated.txt")));
        else Assert.Empty(Directory.GetFileSystemEntries(paths.Root));
    }

    private static ReadableExportGeneration Capture(LedgerFixture f)
    {
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        return ReadableExportGeneration.Build(source,new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31)));
    }
}
