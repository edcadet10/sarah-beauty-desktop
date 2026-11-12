using System.Net;
using System.Text;
using System.Text.Json;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsValuesTransportTests
{
    private const string Workbook="fictional-workbook",Token="Fictional-TEST-Sheets-access",Generation="00000000000000000000000000000001";

    [Fact]
    public async Task ActualRawPayloadAndIndependentTypedReadbackUseFreshAccessChecks()
    {
        var table=Table();var batch=GoogleSheetsValueRequests.ForTables(Generation,42,new[]{table}).Single();
        var read=GoogleSheetsGridReadPlan.ForTable(Workbook,7,table).Single();string[][]? written=null;int accessCalls=0;
        var handler=new Handler(async request=>
        {
            if(request.Method==HttpMethod.Post)
            {
                Assert.EndsWith("/values:batchUpdate",request.RequestUri!.AbsolutePath);Assert.Equal("application/json",request.Content!.Headers.ContentType!.MediaType);
                byte[] bytes=await request.Content.ReadAsByteArrayAsync();Assert.True(bytes.Length<=GoogleSheetsValueRequests.MaximumRequestBytes);
                using var json=JsonDocument.Parse(bytes);Assert.Equal("RAW",json.RootElement.GetProperty("valueInputOption").GetString());
                var data=Assert.Single(json.RootElement.GetProperty("data").EnumerateArray());Assert.Equal("'Facts'!A1:C2",data.GetProperty("range").GetString());
                written=data.GetProperty("values").EnumerateArray().Select(r=>r.EnumerateArray().Select(c=>{Assert.Equal(JsonValueKind.String,c.ValueKind);return c.GetString()!;}).ToArray()).ToArray();
                return Json(new{spreadsheetId=Workbook});
            }
            Assert.Equal(HttpMethod.Get,request.Method);Assert.True(request.Headers.CacheControl!.NoCache);Assert.True(request.Headers.CacheControl.NoStore);Assert.NotNull(written);
            return Grid(written!);
        });using var client=new HttpClient(handler);var transport=new GoogleSheetsValuesTransport(client);
        Task<string> Access(CancellationToken token){token.ThrowIfCancellationRequested();accessCalls++;return Task.FromResult(Token);}
        await transport.WriteAsync(Workbook,batch,Access);await transport.ReadAsync(read,Access);
        Assert.Equal(2,handler.Calls);Assert.Equal(2,accessCalls);Assert.Equal("=Fictional literal",written![1][0]);Assert.Equal("9223372036854775807",written[1][1]);Assert.Equal("",written[1][2]);
    }

    [Fact]
    public async Task AlteredPayloadOrReadLocationFailsBeforeObtainingAccessOrSendingData()
    {
        var table=Table();var batch=GoogleSheetsValueRequests.ForTables(Generation,42,new[]{table}).Single();
        var read=GoogleSheetsGridReadPlan.ForTable(Workbook,7,table).Single();var handler=new Handler(_=>throw new InvalidOperationException("No HTTP expected"));
        using var client=new HttpClient(handler);var transport=new GoogleSheetsValuesTransport(client);int accessCalls=0;
        Task<string> Access(CancellationToken token){accessCalls++;return Task.FromResult(Token);}
        byte[] altered=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(batch.Json.Span).Replace("\"RAW\"","\"USER_ENTERED\""));
        await Assert.ThrowsAsync<InvalidDataException>(()=>transport.WriteAsync(Workbook,batch with{Json=altered},Access));
        var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>transport.ReadAsync(read with{Uri=new Uri("https://example.invalid/private")},Access));
        Assert.Equal("ReadableReadLocationRejected",error.Code);Assert.Equal(0,accessCalls);Assert.Equal(0,handler.Calls);
    }

    [Fact]
    public async Task DeniedAccessAndCancellationAfterAccessPreventTheHttpRequest()
    {
        var batch=GoogleSheetsValueRequests.ForTables(Generation,42,new[]{Table()}).Single();var handler=new Handler(_=>throw new InvalidOperationException("No HTTP expected"));
        using var client=new HttpClient(handler);var transport=new GoogleSheetsValuesTransport(client);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>transport.WriteAsync(Workbook,batch,_=>Task.FromException<string>(new UnauthorizedAccessException("Fictional permission denied"))));
        using var cancel=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>transport.WriteAsync(Workbook,batch,_=>{cancel.Cancel();return Task.FromResult(Token);},cancel.Token));
        Assert.Equal(0,handler.Calls);
    }

    [Theory]
    [InlineData("fragment")]
    [InlineData("port")]
    [InlineData("scheme")]
    [InlineData("query")]
    public async Task ModifiedReadUrisAreRejectedBeforeAccess(string defect)
    {
        var read=GoogleSheetsGridReadPlan.ForTable(Workbook,7,Table()).Single();string address=read.Uri.AbsoluteUri;
        address=defect switch{"fragment"=>address+"#private","port"=>address.Replace("sheets.googleapis.com/","sheets.googleapis.com:444/"),"scheme"=>address.Replace("https:","http:"),_=>address+"&access_token=Fictional"};
        using var client=new HttpClient(new Handler(_=>throw new InvalidOperationException("No HTTP expected")));int accessCalls=0;
        await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleSheetsValuesTransport(client).ReadAsync(read with{Uri=new Uri(address)},_=>{accessCalls++;return Task.FromResult(Token);}));
        Assert.Equal(0,accessCalls);
    }

    private static ReadableExportTable Table()
    {
        var builder=new ReadableTableBuilder(Generation,42,new(),"Facts","Literal","Cents","Unknown");builder.Add("=Fictional literal",long.MaxValue,null);return builder.Freeze();
    }

    [Theory]
    [InlineData("wrong workbook","ReadableWriteUnconfirmed")]
    [InlineData("quota","GoogleQuotaLimited")]
    [InlineData("revoked","GrantUnavailable")]
    [InlineData("redirect","GoogleRequestRejected")]
    [InlineData("oversized","GoogleResponseTooLarge")]
    public async Task UnsupportedWriteResponsesRemainUnconfirmedWithoutFollowingLocations(string defect,string code)
    {
        var batch=GoogleSheetsValueRequests.ForTables(Generation,42,new[]{Table()}).Single();
        var handler=new Handler(_=>
        {
            var response=defect switch
            {
                "wrong workbook"=>Json(new{spreadsheetId="other-workbook"}),
                "quota"=>new HttpResponseMessage((HttpStatusCode)429){Content=new StringContent("Fictional private diagnostic")},
                "revoked"=>new HttpResponseMessage(HttpStatusCode.Unauthorized){Content=new StringContent("Fictional private diagnostic")},
                "oversized"=>new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(new byte[GoogleApiJson.MaximumResponseBytes+1])},
                _=>new HttpResponseMessage(HttpStatusCode.Redirect){Content=new StringContent("Fictional private diagnostic")}
            };
            if(defect=="redirect")response.Headers.Location=new Uri("https://example.invalid/private");return Task.FromResult(response);
        });using var client=new HttpClient(handler);var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleSheetsValuesTransport(client).WriteAsync(Workbook,batch,_=>Task.FromResult(Token)));
        Assert.Equal(code,error.Code);Assert.Equal(1,handler.Calls);Assert.DoesNotContain("Fictional private",error.ToString());Assert.DoesNotContain(Token,error.ToString());
    }

    [Fact]
    public async Task AWriteAcknowledgmentDoesNotConcealAChangedReadbackCell()
    {
        var table=Table();var batch=GoogleSheetsValueRequests.ForTables(Generation,42,new[]{table}).Single();var read=GoogleSheetsGridReadPlan.ForTable(Workbook,7,table).Single();
        var handler=new Handler(request=>Task.FromResult(request.Method==HttpMethod.Post?Json(new{spreadsheetId=Workbook}):Grid(new[]{new[]{"Literal","Cents","Unknown"},new[]{"Fictional changed","9223372036854775807",""}})));
        using var client=new HttpClient(handler);var transport=new GoogleSheetsValuesTransport(client);Task<string> Access(CancellationToken token)=>Task.FromResult(Token);
        await transport.WriteAsync(Workbook,batch,Access);var error=await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>transport.ReadAsync(read,Access));
        Assert.Equal("ReadableCellMismatch",error.Code);Assert.Equal(2,handler.Calls);
    }
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(value))};
    private static HttpResponseMessage Grid(string[][] rows)=>Json(new{spreadsheetId=Workbook,sheets=new[]{new{properties=new{sheetId=7,title="Facts"},data=new[]{new
        {rowData=rows.Select(row=>new{values=row.Select(value=>new{userEnteredValue=new{stringValue=value},effectiveValue=new{stringValue=value}}).ToArray()}).ToArray()}}}}});
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();Calls++;Assert.Equal("https",request.RequestUri!.Scheme);Assert.Equal("sheets.googleapis.com",request.RequestUri.Host);
            Assert.Equal(Token,request.Headers.Authorization?.Parameter);Assert.DoesNotContain(Token,request.RequestUri.AbsoluteUri);return respond(request);
        }
    }
}
