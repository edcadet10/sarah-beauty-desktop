namespace SarahBeauty.Business.Google;

public sealed class GoogleConnectionFailure(string code,string message):IOException(message)
{
    public string Code {get;}=code;
}
