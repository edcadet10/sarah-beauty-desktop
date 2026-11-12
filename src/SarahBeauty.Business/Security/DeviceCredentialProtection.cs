using System.Security.Cryptography;

namespace SarahBeauty.Business.Security;

internal static class DeviceCredentialProtection
{
    internal const int MaximumPlainBytes=128*1024,MaximumProtectedBytes=256*1024;
    internal static byte[] Protect(byte[] clear,byte[] context)
    {
        if(clear.Length is <1 or >MaximumPlainBytes)throw new ArgumentException("The device credential payload exceeds its supported size.");
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Device credentials require Windows user-profile protection.");
        try{return ProtectedData.Protect(clear,context,DataProtectionScope.CurrentUser);}
        catch(CryptographicException){throw new InvalidDataException("Windows could not protect these device credentials. Reconnect Google from this Windows profile.");}
    }
    internal static byte[] Unprotect(byte[] encrypted,byte[] context)
    {
        if(encrypted.Length is <1 or >MaximumProtectedBytes)throw new InvalidDataException("The saved device credential file exceeds its supported size.");
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Device credentials require Windows user-profile protection.");
        byte[] clear;
        try{clear=ProtectedData.Unprotect(encrypted,context,DataProtectionScope.CurrentUser);}
        catch(CryptographicException){throw new InvalidDataException("The saved device credentials cannot be opened in this context. Preserve the file and reconnect Google.");}
        if(clear.Length is >0 and <=MaximumPlainBytes)return clear;CryptographicOperations.ZeroMemory(clear);throw new InvalidDataException("The saved device credential payload is invalid.");
    }
}
