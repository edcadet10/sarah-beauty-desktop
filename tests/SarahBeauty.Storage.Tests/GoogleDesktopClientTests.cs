using System.Text;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleDesktopClientTests
{
    private const string Fictional="{\"installed\":{\"client_id\":\"123456789-fictional-test.apps.googleusercontent.com\",\"client_secret\":\"Fictional-TEST-client-secret\",\"project_id\":\"fictional-sarah-test\",\"auth_uri\":\"https://accounts.google.com/o/oauth2/auth\",\"token_uri\":\"https://oauth2.googleapis.com/token\",\"redirect_uris\":[\"http://localhost\"]}}";
    [Fact]
    public void DesktopConfigurationReturnsOnlyKnownClientFactsAndHasRedactedDisplay()
    {
        var client=GoogleDesktopClient.Parse(Encoding.UTF8.GetBytes(Fictional));Assert.Equal("fictional-sarah-test",client.ProjectId);Assert.Equal("Fictional-TEST-client-secret",client.ClientSecret);
        Assert.DoesNotContain(client.ClientSecret,client.ToString());Assert.DoesNotContain(client.ClientId,client.ToString());
        Assert.NotNull(GoogleDesktopClient.Parse(Encoding.UTF8.GetBytes(Fictional.Replace("/o/oauth2/auth","/o/oauth2/v2/auth",StringComparison.Ordinal))));
    }
    [Theory]
    [InlineData("web")]
    [InlineData("service")]
    [InlineData("token")]
    [InlineData("authorization")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("identifier")]
    [InlineData("whitespace")]
    public void OtherCredentialTypesAmbiguityAndUntrustedEndpointsFailWithNoPrivateDetails(string change)
    {
        string json=change switch
        {
            "web"=>Fictional.Replace("installed","web",StringComparison.Ordinal),"service"=>"{\"type\":\"service_account\",\"private_key\":\"Fictional private text\"}",
            "token"=>Fictional.Replace("https://oauth2.googleapis.com/token","https://fictional.invalid/token",StringComparison.Ordinal),"authorization"=>Fictional.Replace("https://accounts.google.com/o/oauth2/auth","https://fictional.invalid/auth",StringComparison.Ordinal),
            "duplicate"=>Fictional.Replace("\"client_secret\":","\"client_secret\":\"Fictional duplicate\",\"client_secret\":",StringComparison.Ordinal),"missing"=>Fictional.Replace("\"client_secret\":\"Fictional-TEST-client-secret\",","",StringComparison.Ordinal),
            "malformed"=>"Fictional private non-JSON text","identifier"=>Fictional.Replace(".apps.googleusercontent.com",".fictional.invalid",StringComparison.Ordinal),"whitespace"=>Fictional.Replace("Fictional-TEST-client-secret","Fictional TEST client secret",StringComparison.Ordinal),_=>throw new ArgumentException()
        };
        var error=Assert.Throws<InvalidDataException>(()=>GoogleDesktopClient.Parse(Encoding.UTF8.GetBytes(json)));Assert.Null(error.InnerException);Assert.DoesNotContain("Fictional",error.ToString());
    }
    [Fact]
    public void BoundedStrictJsonRejectsEmptyOversizedAndInvalidUtf8Inputs()
    {
        Assert.Throws<InvalidDataException>(()=>GoogleDesktopClient.Parse([]));Assert.Throws<InvalidDataException>(()=>GoogleDesktopClient.Parse(new byte[GoogleDesktopClient.MaximumFileBytes+1]));
        byte[] invalid=Encoding.UTF8.GetBytes(Fictional);invalid[invalid.AsSpan().IndexOf(Encoding.UTF8.GetBytes("Fictional-TEST"))]=255;Assert.Throws<InvalidDataException>(()=>GoogleDesktopClient.Parse(invalid));
    }
}
