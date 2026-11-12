using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

internal sealed class GoogleSheetsTestServer(BusinessStore store,GoogleOutputPermit permit):HttpMessageHandler
{
    internal const string FileId="fictional-workbook";
    internal readonly Dictionary<(string Table,int Row,int Column),string> Cells=new();
    internal bool FileExists,Published,Shared,LostCreate,LostSchema,LostValues,LostPublication,EmptyLookup,Quota;
    internal int Calls,Creates,SchemaWrites,ValueWrites,PublicationWrites,GridReads;
    private JsonObject _schema=GoogleSheetsSchemaInitializationTests.Fresh(777);
    private GoogleSheetsExportJob Job=>store.Read(db=>GoogleSheetsJobRecords.List(db,permit.Policy.BusinessId).Single());

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();Calls++;Assert.Equal("https",request.RequestUri!.Scheme);Assert.Contains(request.RequestUri.Host,new[]{"www.googleapis.com","sheets.googleapis.com"});Assert.NotNull(request.Headers.Authorization);
        if(request.RequestUri.Host=="www.googleapis.com")return await Drive(request).ConfigureAwait(false);
        if(request.Method==HttpMethod.Post)return await Update(request).ConfigureAwait(false);
        string? range=Query(request.RequestUri,"ranges");if(range is null)return Json(_schema);
        GridReads++;return Grid(range);
    }
    private async Task<HttpResponseMessage> Drive(HttpRequestMessage request)
    {
        var job=Job;
        if(request.Method==HttpMethod.Post)
        {
            Creates++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Assert.Equal(job.Id,body.RootElement.GetProperty("appProperties").GetProperty("sarahBeautyExportJob").GetString());
            Assert.False(body.RootElement.TryGetProperty("id",out _));FileExists=true;if(LostCreate){LostCreate=false;throw Lost();}return Json(new JsonObject{["id"]=FileId});
        }
        if(request.Method==HttpMethod.Patch)
        {
            PublicationWrites++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Assert.Equal("verified",body.RootElement.GetProperty("appProperties").GetProperty("sarahBeautyExportState").GetString());
            Published=true;if(LostPublication){LostPublication=false;throw Lost();}return Json(new JsonObject{["id"]=FileId});
        }
        if(request.RequestUri!.AbsolutePath.EndsWith("/"+job.FolderId,StringComparison.Ordinal))return Json(GoogleSheetsWorkbookCreationTests.FolderFacts(job,permit));
        var facts=GoogleSheetsWorkbookCreationTests.FileFacts(job,permit);facts["id"]=FileId;facts["shared"]=Shared;if(Published)facts["appProperties"]!["sarahBeautyExportState"]="verified";
        if(request.RequestUri.AbsolutePath.EndsWith("/files",StringComparison.Ordinal))return Json(new JsonObject{["files"]=FileExists&&!EmptyLookup?new JsonArray(facts):new JsonArray()});
        return FileExists?Json(facts):new(HttpStatusCode.NotFound){Content=new StringContent("Fictional file missing")};
    }
    private async Task<HttpResponseMessage> Update(HttpRequestMessage request)
    {
        Assert.True(FileExists);Assert.False(Published);using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        if(request.RequestUri!.AbsolutePath.EndsWith("/values:batchUpdate",StringComparison.Ordinal))
        {
            ValueWrites++;Assert.Equal("RAW",body.RootElement.GetProperty("valueInputOption").GetString());if(Quota)return new((HttpStatusCode)429){Content=new StringContent("Fictional quota response")};
            foreach(var data in body.RootElement.GetProperty("data").EnumerateArray())
            {
                var address=Address(data.GetProperty("range").GetString()!);int row=address.Row;
                foreach(var values in data.GetProperty("values").EnumerateArray())
                {
                    int column=address.Column;foreach(var value in values.EnumerateArray()){Assert.Equal(JsonValueKind.String,value.ValueKind);Cells[(address.Table,row,column++)]=value.GetString()!;}Assert.Equal(address.LastColumn+1,column);row++;
                }
                Assert.Equal(address.LastRow+1,row);
            }
            if(LostValues){LostValues=false;throw Lost();}
        }
        else
        {
            SchemaWrites++;GoogleSheetsSchemaInitializationTests.Apply(_schema,body.RootElement.GetProperty("requests"));if(LostSchema){LostSchema=false;throw Lost();}
        }
        return Json(new JsonObject{["spreadsheetId"]=FileId});
    }
    private HttpResponseMessage Grid(string range)
    {
        var address=Address(range);var properties=_schema["sheets"]!.AsArray().Single(sheet=>sheet!["properties"]!["title"]!.GetValue<string>()==address.Table)!["properties"]!;
        var rows=new JsonArray();for(int row=address.Row;row<=address.LastRow;row++)
        {
            var values=new JsonArray();for(int column=address.Column;column<=address.LastColumn;column++)
            {
                string value=Cells.GetValueOrDefault((address.Table,row,column),"");values.Add(new JsonObject{["userEnteredValue"]=new JsonObject{["stringValue"]=value},["effectiveValue"]=new JsonObject{["stringValue"]=value}});
            }
            rows.Add(new JsonObject{["values"]=values});
        }
        return Json(new JsonObject{["spreadsheetId"]=FileId,["sheets"]=new JsonArray(new JsonObject{["properties"]=new JsonObject{["sheetId"]=properties["sheetId"]!.DeepClone(),["title"]=address.Table},
            ["data"]=new JsonArray(new JsonObject{["startRow"]=address.Row,["startColumn"]=address.Column,["rowData"]=rows})})});
    }
    private static (string Table,int Row,int Column,int LastRow,int LastColumn) Address(string range)
    {
        var match=Regex.Match(range,"^'([A-Za-z0-9 _-]+)'!([A-Z]+)([0-9]+):([A-Z]+)([0-9]+)$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));Assert.True(match.Success);
        static int Column(string text){int value=0;foreach(char letter in text)value=checked(value*26+letter-'A'+1);return value-1;}
        return(match.Groups[1].Value,int.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture)-1,Column(match.Groups[2].Value),int.Parse(match.Groups[5].Value,CultureInfo.InvariantCulture)-1,Column(match.Groups[4].Value));
    }
    private static string? Query(Uri uri,string key)=>uri.Query.TrimStart('?').Split('&').Select(part=>part.Split('=',2)).Where(pair=>pair.Length==2&&pair[0]==key).Select(pair=>Uri.UnescapeDataString(pair[1])).SingleOrDefault();
    private static HttpResponseMessage Json(JsonNode body)=>new(HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())};
    private static HttpRequestException Lost()=>new("Fictional response lost after the remote effect");
}
