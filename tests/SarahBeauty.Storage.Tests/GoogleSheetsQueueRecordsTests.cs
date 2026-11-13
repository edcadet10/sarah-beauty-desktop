using System.Text.Json;
using SarahBeauty.Business.Google;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class GoogleSheetsQueueRecordsTests
{
    [Fact]
    public void PreparingAndAttachedJobReopenAndStaleWritersCannotOverwriteThem()
    {
        using var f=new BusinessFixture();var first=State(f);long watermark=Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;"));
        f.Store.Transaction(db=>GoogleSheetsQueueRecords.Put(db,null,first));var running=first with{Status="Running",JobId=Guid.NewGuid().ToString("N")};f.Store.Transaction(db=>GoogleSheetsQueueRecords.Put(db,first,running));f.Owner.Lock();
        var reopened=new BusinessStore(f.Store.DatabasePath);Assert.Equal(running,reopened.Read(db=>GoogleSheetsQueueRecords.Read(db,first.BusinessId,first.InstanceId)));
        Assert.Throws<InvalidDataException>(()=>reopened.Transaction(db=>GoogleSheetsQueueRecords.Put(db,first,first)));Assert.Throws<InvalidDataException>(()=>reopened.Transaction(db=>GoogleSheetsQueueRecords.Put(db,null,first)));
        Assert.Equal(running,reopened.Read(db=>GoogleSheetsQueueRecords.Read(db,first.BusinessId,first.InstanceId)));Assert.Equal(watermark,Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;")));Assert.Equal(DateTimeOffset.MinValue,f.Owner.ExpiresAt);
    }
    [Theory]
    [InlineData("version")]
    [InlineData("business")]
    [InlineData("instance")]
    [InlineData("connection")]
    [InlineData("watermark")]
    [InlineData("coverage")]
    [InlineData("status")]
    [InlineData("running")]
    [InlineData("success")]
    [InlineData("diagnostic")]
    [InlineData("retry")]
    [InlineData("timestamp")]
    [InlineData("revision")]
    public void CorruptPersistedQueueCannotAuthorizeAutomaticDelivery(string defect)
    {
        using var f=new BusinessFixture();var original=State(f);var value=defect switch
        {
            "version"=>original with{Version=2},"business"=>original with{BusinessId=Guid.NewGuid().ToString()},"instance"=>original with{InstanceId=new string('0',32)},"connection"=>original with{ConnectionId="bad"},
            "watermark"=>original with{ObservedWatermark=-1},"coverage"=>original with{DeliveredWatermark=original.ObservedWatermark+1},"status"=>original with{Status="Unexpected"},"running"=>original with{Status="Running"},
            "success"=>original with{Status="Succeeded"},"diagnostic"=>original with{FailureCode="Fictional private provider detail"},"retry"=>original with{Status="Failed",FailureCount=1,FailureCode="GoogleQuotaLimited"},
            "timestamp"=>original with{NextAttemptAtUtc="not UTC"},_=>original with{ProfileRevision=0}
        };
        f.Store.Transaction(db=>db.Execute("INSERT INTO AppMetadata(Key,Value) VALUES($key,$value);",("$key","GoogleSheetsQueue/"+original.BusinessId+"/"+original.InstanceId),("$value",JsonSerializer.Serialize(value))));
        Assert.Throws<InvalidDataException>(()=>f.Store.Read(db=>GoogleSheetsQueueRecords.Read(db,original.BusinessId,original.InstanceId)));
    }
    [Fact]
    public void QueueLeaseIsExclusiveAndCanHoldTheSeparateDeliveryLease()
    {
        using var f=new BusinessFixture();using(var queue=GoogleSheetsDeliveryLease.TryQueue(f.Store)){Assert.NotNull(queue);Assert.Null(GoogleSheetsDeliveryLease.TryQueue(new BusinessStore(f.Store.DatabasePath)));using var delivery=GoogleSheetsDeliveryLease.TryAcquire(f.Store);Assert.NotNull(delivery);}
        using var next=GoogleSheetsDeliveryLease.TryQueue(f.Store);Assert.NotNull(next);
    }
    private static GoogleSheetsQueueState State(BusinessFixture f)=>new(1,f.Owner.BusinessId,f.Store.InstanceId,1,1,Guid.NewGuid().ToString(),Guid.NewGuid().ToString("N"),
        Convert.ToInt64(TestStore.Scalar(f.Store.DatabasePath,"SELECT Watermark FROM Business;")),null,"Preparing",0,null,null,null,DateTimeOffset.UtcNow.ToString("O"));
}
