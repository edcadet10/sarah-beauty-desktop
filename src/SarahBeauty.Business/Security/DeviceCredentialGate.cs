namespace SarahBeauty.Business.Security;

internal static class DeviceCredentialGate
{
    internal static FileStream Enter(string path)
    {
        for(int attempt=0;;attempt++)
        {
            try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException error) when((error.HResult&0xffff) is 32 or 33)
            {
                if(attempt>=499)throw new IOException("Device credential storage is busy. Wait for the other operation and retry.");Thread.Sleep(10);
            }
        }
    }
}
