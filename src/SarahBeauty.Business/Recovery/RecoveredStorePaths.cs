namespace SarahBeauty.Business.Recovery;

internal sealed class RecoveredStorePaths(string parent)
{
    internal string Root {get;}=Path.GetFullPath(parent);
    internal string Pointer=>Path.Combine(Root,"active-test-store.json");
    internal string Lock=>Path.Combine(Root,".active-test-store.lock");
    internal string Database(string request)
    {
        if(!Guid.TryParse(request,out var id)||id==Guid.Empty)throw new InvalidDataException("The recovered store selection identity is invalid.");
        string path=Root;ArchivePaths.RejectLink(path);foreach(string part in new[]{"RestoredStores",id.ToString("N"),"sarahbeauty.db"}){path=Path.Combine(path,part);ArchivePaths.RejectLink(path);}return path;
    }
    internal string Relative(string request)=>"RestoredStores/"+Guid.Parse(request).ToString("N")+"/sarahbeauty.db";
}
