using System.IO.Compression;

namespace SarahBeauty.Business.Recovery;

internal sealed record ExtractedArchive(ArchiveManifest Manifest,string ManifestHash);
internal static class ArchiveExtraction
{
    internal static ExtractedArchive Read(ArchiveWorkspace workspace,CancellationToken cancellation,Action<string>? stage=null)
    {
        using var zip=ZipFile.OpenRead(workspace.PackagePath());if(zip.Entries.Count is <2 or >100001)throw new InvalidDataException("The recovery entry count is not supported.");
        var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in zip.Entries)
        {
            int mode=(entry.ExternalAttributes>>16)&0xf000;
            if(!entries.TryAdd(entry.FullName,entry) || mode is not(0 or 0x8000) || (entry.ExternalAttributes&(int)FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Recovery entries cannot be duplicated or use links.");
            if(entry.FullName!="manifest.json" && ArchivePaths.Normalize(entry.FullName)!=entry.FullName)throw new InvalidDataException("A recovery entry path is not canonical.");
        }
        if(!entries.TryGetValue("manifest.json",out var manifestEntry) || manifestEntry.FullName!="manifest.json" || manifestEntry.Length is <1 or >ArchivePackage.MaximumManifestBytes)throw new InvalidDataException("The recovery manifest is missing or too large.");
        using var raw=new MemoryStream();string manifestHash=ArchiveEntryCopy.Copy(manifestEntry,raw,manifestEntry.Length,cancellation);var manifest=ArchiveManifestChecks.Read(raw.ToArray());
        if(entries.Count!=manifest.Files.Length+1)throw new InvalidDataException("The recovery files differ from the manifest.");
        foreach(var file in manifest.Files)
        {
            cancellation.ThrowIfCancellationRequested();if(!entries.TryGetValue(file.RelativePath,out var entry) || entry.FullName!=file.RelativePath)throw new InvalidDataException("A manifest file is missing or uses different path casing.");
            ArchiveEntryCopy.File(entry,workspace.FilePath(file.RelativePath),file,cancellation);stage?.Invoke("File extracted");
        }
        return new(manifest,manifestHash);
    }
}
