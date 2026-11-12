using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Business.Storage;

public static class BusinessReviewFolders
{
    public static string Resolve(string localData,string reviewRoot,BusinessMode mode,string? testOverride=null)
    {
        BusinessModes.StoredName(mode);
        if(string.IsNullOrWhiteSpace(reviewRoot)||!Path.IsPathFullyQualified(reviewRoot))throw new ArgumentException("Use a separate absolute directory for fictional review workspaces.");
        string root=Path.GetFullPath(reviewRoot);var protectedFolders=new[]{BusinessDataFolders.Resolve(localData,BusinessMode.Test),BusinessDataFolders.Resolve(localData,BusinessMode.Live),BusinessDataFolders.Resolve(localData,BusinessMode.Test,testOverride)};
        if(protectedFolders.Any(folder=>BusinessDataFolders.Overlaps(root,folder)))throw new ArgumentException("The fictional review root must be separate from normal TEST/LIVE and explicit TEST preview folders.");
        for(DirectoryInfo? path=new(root);path is not null;path=path.Parent)ArchivePaths.RejectLink(path.FullName);
        string selected=Path.Combine(root,mode==BusinessMode.Test?"Test":"Live");ArchivePaths.RejectLink(selected);return selected;
    }
}
