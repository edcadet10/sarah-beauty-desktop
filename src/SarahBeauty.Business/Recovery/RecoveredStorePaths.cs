using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Recovery;

internal sealed class RecoveredStorePaths
{
    internal string Root {get;}
    internal BusinessMode Mode {get;}
    internal RecoveredStorePaths(string parent,BusinessMode mode=BusinessMode.Test){BusinessModes.StoredName(mode);Root=Path.GetFullPath(parent);Mode=mode;}
    internal string Pointer=>Path.Combine(Root,Mode==BusinessMode.Test?"active-test-store.json":"active-live-store.json");
    internal string Lock=>Path.Combine(Root,Mode==BusinessMode.Test?".active-test-store.lock":".active-live-store.lock");
    internal string DefaultDatabase=>Mode==BusinessMode.Test?Path.Combine(Root,"sarahbeauty.db"):Path.Combine(Root,"Primary","sarahbeauty.db");
    internal string Database(string request)
    {
        if(!Guid.TryParse(request,out var id)||id==Guid.Empty)throw new InvalidDataException("The recovered store selection identity is invalid.");
        string path=Root;ArchivePaths.RejectLink(path);foreach(string part in new[]{"RestoredStores",id.ToString("N"),"sarahbeauty.db"}){path=Path.Combine(path,part);ArchivePaths.RejectLink(path);}return path;
    }
    internal string Relative(string request)=>"RestoredStores/"+Guid.Parse(request).ToString("N")+"/sarahbeauty.db";
}
