using System.Security.Cryptography;

namespace SarahBeauty.Business.Attachments;

internal static class AttachmentCopy
{
    internal const long MaximumBytes=50*1024*1024;
    internal static StagedAttachment Stage(AttachmentPaths paths,string source,string id,Action<string>? failure,CancellationToken cancellation)
    {
        string partial="Attachments/"+id+".partial";
        try
        {
            paths.EnsureDirectory();using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length is <8 or >MaximumBytes)throw new ArgumentException("Choose a nonempty PDF, JPEG, or PNG file no larger than 50 MB.");
            byte[] header=new byte[8];input.ReadExactly(header);input.Position=0;var type=Type(Path.GetExtension(source).ToLowerInvariant(),header);
            string original=Path.GetFileName(source);if(original.Length>255 || original.Any(char.IsControl))throw new ArgumentException("Use a file name without control characters, up to 255 characters.");
            string target=paths.Resolve(partial);using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];long length=0;int count;
            while((count=input.Read(buffer))>0)
            {
                cancellation.ThrowIfCancellationRequested();length=checked(length+count);if(length>MaximumBytes)throw new ArgumentException("This file exceeds the 50 MB limit.");
                output.Write(buffer,0,count);hash.AppendData(buffer,0,count);failure?.Invoke("Copy block");
            }
            output.Flush(flushToDisk:true);return new(partial,"Attachments/"+id+type.Extension,original,type.MediaType,length,Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch{paths.DeletePartial(partial);throw;}
    }
    private static (string Extension,string MediaType) Type(string extension,byte[] header)
    {
        if(extension==".pdf" && header.AsSpan().StartsWith("%PDF-"u8))return(".pdf","application/pdf");
        if(extension is ".jpg" or ".jpeg" && header[0]==0xff && header[1]==0xd8 && header[2]==0xff)return(".jpg","image/jpeg");
        if(extension==".png" && header.AsSpan().SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return(".png","image/png");
        throw new ArgumentException("The file extension and signature must identify a supported PDF, JPEG, or PNG. Executables, HTML, and SVG are not supported.");
    }
}
