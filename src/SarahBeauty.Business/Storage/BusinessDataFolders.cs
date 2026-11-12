namespace SarahBeauty.Business.Storage;

public static class BusinessDataFolders
{
    public static string Resolve(string localData,BusinessMode mode,string? testOverride=null)
    {
        BusinessModes.StoredName(mode);string? preview=mode==BusinessMode.Test&&!string.IsNullOrWhiteSpace(testOverride)?Path.GetFullPath(testOverride):null;
        if(string.IsNullOrWhiteSpace(localData))return preview??throw new InvalidOperationException("Windows could not locate the local application data folder.");
        string parent=Path.Combine(Path.GetFullPath(localData),"SarahBeautyDesktop"),live=Path.Combine(parent,"Live");
        if(preview is not null&&Overlaps(preview,live))throw new ArgumentException("The TEST preview directory must be separate from the LIVE business directory.");
        return mode==BusinessMode.Live?live:preview??Path.Combine(parent,"Test");
    }
    private static bool Overlaps(string first,string second)=>Contains(first,second)||Contains(second,first);
    private static bool Contains(string parent,string child)=>string.Equals(parent,child,StringComparison.OrdinalIgnoreCase)||child.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
}
