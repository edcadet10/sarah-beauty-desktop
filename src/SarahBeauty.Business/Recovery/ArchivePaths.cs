namespace SarahBeauty.Business.Recovery;

internal sealed class ArchivePaths(string root)
{
    private readonly string _root=Path.GetFullPath(root);
    internal static string Normalize(string relative)
    {
        var parts=relative.Replace('\\','/').Split('/');bool database=parts.Length==1 && parts[0]=="sarahbeauty.db";
        bool attachment=parts.Length==2 && parts[0]=="Attachments" && GeneratedFile(parts[1],[".pdf",".jpg",".png"]);
        bool document=parts.Length==3 && parts[0]=="Documents" && Guid.TryParseExact(parts[1],"D",out _) && GeneratedFile(parts[2],[".pdf"]);
        if(!database && !attachment && !document)throw new InvalidDataException("An archive file path is outside the supported managed store paths.");return string.Join('/',parts);
    }
    internal string Resolve(string relative)
    {
        string canonical=Normalize(relative);RejectLink(_root);string path=_root;
        foreach(string part in canonical.Split('/')){path=Path.Combine(path,part);RejectLink(path);}return path;
    }
    internal static void RejectLink(string path)
    {
        try{if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Recovery paths cannot use links or reparse points.");}
        catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
    }
    private static bool GeneratedFile(string name,string[] suffixes)
    {
        string? suffix=suffixes.SingleOrDefault(s=>name.EndsWith(s,StringComparison.Ordinal));return suffix is not null && Guid.TryParseExact(name[..^suffix.Length],"N",out _);
    }
}
