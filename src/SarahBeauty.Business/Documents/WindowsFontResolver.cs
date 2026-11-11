using PdfSharp.Fonts;

namespace SarahBeauty.Business.Documents;

internal sealed class WindowsFontResolver : IFontResolver
{
    private static readonly Dictionary<string,string> Faces=new()
    {
        ["regular"]="arial.ttf",["bold"]="arialbd.ttf",["italic"]="ariali.ttf",["bolditalic"]="arialbi.ttf"
    };

    public FontResolverInfo ResolveTypeface(string familyName,bool isBold,bool isItalic)=>
        new(isBold?(isItalic?"bolditalic":"bold"):(isItalic?"italic":"regular"));

    public byte[] GetFont(string faceName)
    {
        if(!Faces.TryGetValue(faceName,out string? file))throw new ArgumentException("Unsupported PDF font face.");
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),file);
        if(!File.Exists(path))throw new InvalidOperationException("Windows Arial fonts could not be found. Repair the Windows fonts before creating this PDF.");
        return File.ReadAllBytes(path);
    }
}
