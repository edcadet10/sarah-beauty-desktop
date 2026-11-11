namespace SarahBeauty.Business.Attachments;

internal sealed class AttachmentPaths(string databasePath)
{
    private readonly string _root=Path.GetFullPath(Path.GetDirectoryName(databasePath)!);
    internal string Resolve(string relative)
    {
        var parts=relative.Replace('\\','/').Split('/');
        if(parts.Length!=2 || parts[0]!="Attachments" || !ValidName(parts[1]))throw new InvalidDataException("The managed attachment path is invalid.");
        RejectLink(_root);string directory=Path.Combine(_root,"Attachments");RejectLink(directory);string path=Path.Combine(directory,parts[1]);RejectLink(path);return path;
    }
    private static bool ValidName(string name)
    {
        string[] suffixes=[".pdf",".jpg",".png",".partial"];
        string? suffix=suffixes.SingleOrDefault(name.EndsWith);
        return suffix is not null && Guid.TryParseExact(name[..^suffix.Length],"N",out _);
    }
    private static void RejectLink(string path)
    {
        try
        {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Managed attachment paths cannot use links or reparse points.");
        }
        catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
    }
    internal void EnsureDirectory()
    {
        string probe=Resolve("Attachments/"+Guid.NewGuid().ToString("N")+".partial");
        Directory.CreateDirectory(Path.GetDirectoryName(probe)!);_ = Resolve("Attachments/"+Guid.NewGuid().ToString("N")+".partial");
    }
    internal void DeletePartial(string relative)
    {
        try{File.Delete(Resolve(relative));}catch(IOException){}catch(UnauthorizedAccessException){}catch(InvalidDataException){}
    }
}
