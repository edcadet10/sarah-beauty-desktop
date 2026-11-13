using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Google;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsWorkbookReadbackTests
{
    private const string FileId="fictional-workbook",Token="fictional-readback-access";
    [Theory]
    [InlineData("staging")]
    [InlineData("verified")]
    public async Task EverySourceAndStatusTableIsIndependentlyReadWithoutMutations(string state)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var job=new GoogleSheetsExportJobs(f.Ledger.Business.Store).Reserve(permit,artifact);
        var generation=new ReadableGenerationFiles(f.Ledger.Business.Store).Load(artifact);var schema=GoogleSheetsWorkbookSchema.Build(generation);var spec=GoogleSheetsJobRecords.Spec(job);
        var publication=new GoogleSheetsPublication(1,job.Id,job.BusinessId,job.InstanceId,FileId,job.GenerationId,job.ManifestSha256,job.SourceWatermark,DateTimeOffset.UtcNow.ToString("O"));var status=GoogleSheetsDeliveryStatus.Build(job,publication,generation);
        var expected=generation.Tables.Append(status).SelectMany(table=>GoogleSheetsGridReadPlan.ForTable(FileId,schema.Tabs.Single(tab=>tab.Name==table.Name).SheetId,table)).ToDictionary(request=>request.Uri.AbsoluteUri,StringComparer.Ordinal);
        var seen=new HashSet<string>();int accesses=0;var handler=new Handler(request=>
        {
            Assert.Equal(HttpMethod.Get,request.Method);
            if(request.RequestUri!.Host=="www.googleapis.com"){var facts=GoogleSheetsWorkbookCreationTests.FileFacts(job,permit);facts["id"]=FileId;facts["appProperties"]!["sarahBeautyExportState"]=state;return Json(facts);}
            if(expected.TryGetValue(request.RequestUri.AbsoluteUri,out var read)){Assert.True(seen.Add(request.RequestUri.AbsoluteUri));return Grid(read);}
            return Json(SchemaFacts(schema));
        });using var client=new HttpClient(handler);var service=new GoogleSheetsWorkbookReadback(client);Task<string> Access(CancellationToken token){token.ThrowIfCancellationRequested();accesses++;return Task.FromResult(Token);}
        await service.SourcesAsync(generation,spec,FileId,permit.Connection.Account.Account.PermissionId,Access,state:state);await service.StatusAsync(schema,status,spec,FileId,permit.Connection.Account.Account.PermissionId,Access,state:state);
        Assert.Equal(expected.Count,seen.Count);Assert.Equal(166,seen.Select(uri=>expected[uri].Expected.Table).Distinct(StringComparer.Ordinal).Count());Assert.Equal(handler.Calls,accesses);
    }

    [Theory]
    [InlineData("last cell")]
    [InlineData("schema after")]
    [InlineData("sharing after")]
    [InlineData("access withdrawn")]
    [InlineData("source")]
    public async Task LateChangesAndWrongSourcePreventFullReadbackConfirmation(string defect)
    {
        using var f=new GoogleOutputSettingsTests.Fixture();var (permit,artifact)=await GoogleSheetsExportJobsTests.Stage(f);var job=new GoogleSheetsExportJobs(f.Ledger.Business.Store).Reserve(permit,artifact);
        var generation=new ReadableGenerationFiles(f.Ledger.Business.Store).Load(artifact);var schema=GoogleSheetsWorkbookSchema.Build(generation);var spec=GoogleSheetsJobRecords.Spec(job);int layouts=0,privateReads=0,gridReads=0,accesses=0;
        var expected=generation.Tables.SelectMany(table=>GoogleSheetsGridReadPlan.ForTable(FileId,schema.Tabs.Single(tab=>tab.Name==table.Name).SheetId,table)).ToDictionary(request=>request.Uri.AbsoluteUri,StringComparer.Ordinal);
        var handler=new Handler(request=>
        {
            if(request.RequestUri!.Host=="www.googleapis.com"){privateReads++;var facts=GoogleSheetsWorkbookCreationTests.FileFacts(job,permit);facts["id"]=FileId;if(defect=="sharing after"&&privateReads>1)facts["shared"]=true;return Json(facts);}
            if(expected.TryGetValue(request.RequestUri.AbsoluteUri,out var read)){gridReads++;var body=GridFacts(read);if(defect=="last cell"&&gridReads==expected.Count)body["sheets"]![0]!["data"]![0]!["rowData"]![0]!["values"]![0]!["userEnteredValue"]=new JsonObject{["formulaValue"]="=1+1"};return Json(body);}
            layouts++;var layout=SchemaFacts(schema);if(defect=="schema after"&&layouts>1)layout["sheets"]![0]!["properties"]!["gridProperties"]!["rowCount"]=99999;return Json(layout);
        });using var client=new HttpClient(handler);Task<string> Access(CancellationToken token){accesses++;if(defect=="access withdrawn"&&accesses==5)throw new UnauthorizedAccessException();return Task.FromResult(Token);}
        if(defect=="source")spec=spec with{GenerationId=Guid.NewGuid().ToString("N")};
        if(defect=="access withdrawn")await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new GoogleSheetsWorkbookReadback(client).SourcesAsync(generation,spec,FileId,permit.Connection.Account.Account.PermissionId,Access));
        else await Assert.ThrowsAsync<GoogleConnectionFailure>(()=>new GoogleSheetsWorkbookReadback(client).SourcesAsync(generation,spec,FileId,permit.Connection.Account.Account.PermissionId,Access));
        if(defect=="source")Assert.Equal(0,handler.Calls);if(defect is "last cell" or "schema after" or "sharing after")Assert.Equal(expected.Count,gridReads);
    }

    internal static JsonObject SchemaFacts(GoogleSheetsWorkbookSchema schema)=>new(){["spreadsheetId"]=FileId,["sheets"]=new JsonArray(schema.Tabs.Select(tab=>(JsonNode)new JsonObject{["properties"]=new JsonObject
        {["sheetId"]=tab.SheetId,["title"]=tab.Name,["index"]=tab.Index,["sheetType"]="GRID",["hidden"]=false,["gridProperties"]=new JsonObject{["rowCount"]=tab.Rows,["columnCount"]=tab.Columns,["frozenRowCount"]=tab.FrozenRows}}}).ToArray())};
    private static HttpResponseMessage Grid(GoogleSheetsGridReadRequest read)=>Json(GridFacts(read));
    internal static JsonObject GridFacts(GoogleSheetsGridReadRequest read)=>JsonSerializer.SerializeToNode(new{spreadsheetId=FileId,sheets=new[]{new{properties=new{sheetId=read.SheetId,title=read.Expected.Table},data=new[]{new
        {startRow=read.Expected.StartRow,startColumn=read.Expected.StartColumn,rowData=read.Expected.Values.Select(row=>new{values=row.Select(value=>new{userEnteredValue=new{stringValue=value},effectiveValue=new{stringValue=value}}).ToArray()}).ToArray()}}}}})!.AsObject();
    private static HttpResponseMessage Json(JsonNode body)=>new(HttpStatusCode.OK){Content=new StringContent(body.ToJsonString())};
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();Calls++;Assert.Equal(Token,request.Headers.Authorization?.Parameter);return Task.FromResult(respond(request));
        }
    }
}
