namespace SarahBeauty.Business.Recovery;

internal static class ActivePointerGate
{
    internal static FileStream Enter(string pointer)
    {
        string path=pointer+".publication.lock";ArchivePaths.RejectLink(path);
        for(int attempt=0;;attempt++)
        {
            try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException error) when((error.HResult&0xffff) is 32 or 33)
            {
                if(attempt>=499)throw new IOException("The working-store selection is busy. Wait for the other selection operation and retry.",error);Thread.Sleep(10);
            }
        }
    }
}
