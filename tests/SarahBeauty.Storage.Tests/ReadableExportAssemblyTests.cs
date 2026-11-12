using System.Globalization;
using System.Text.Json;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Exports;
using SarahBeauty.Business.Finance;

namespace SarahBeauty.Storage.Tests;

public sealed class ReadableExportAssemblyTests
{
    private static readonly ReadableExportSelection Dates=new(new(2027,1,1),new(2027,1,31),new(2027,1,31),new(2027,3,1),new(2027,3,31));

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void EveryCurrentGroupComposesFromOneCaptureWithoutPrivateDataOrLaterChanges(string culture)
    {
        using var f=new LedgerFixture();var ready=ReservationTests.Setup(f);ReservationTests.Pay(f,ready.Invoice,22500);
        ReservationTests.Confirm(f,ready.Booking);f.Post(f.Draft(FinancialKind.Expense,500));
        const string secret="Fictional-private-assembled-customer";
        var customers=new CustomerBook(f.Business.Store);var customer=customers.Search(f.Business.Owner).Single(c=>c.Id==f.CustomerId);
        customers.Save(f.Business.Owner,customer with{PrivateNotes=secret,Sensitivities=secret,MakeupPreferences=secret});
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        var first=ReadableExportAssembly.Build(source,Dates);
        customers.Save(f.Business.Owner,new Customer{FullName="Fictional later assembly customer"});f.Business.Owner.Lock();
        ReadableExportAssembly second;var previous=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);second=ReadableExportAssembly.Build(source,Dates);}
        finally{CultureInfo.CurrentCulture=previous;}
        Assert.Equal(JsonSerializer.Serialize(first),JsonSerializer.Serialize(second));
        Assert.Equal(29,first.Groups.Count);Assert.Equal(155,first.Tables.Count);Assert.Equal(first.Tables.Count,first.Groups.Sum(g=>g.Tables.Count));
        Assert.Equal(first.Tables.Count,first.Tables.Select(t=>t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(first.Tables,t=>{Assert.Equal(source.Source.GenerationId,t.GenerationId);Assert.Equal(source.Source.Watermark,t.Watermark);});
        string json=JsonSerializer.Serialize(first);Assert.DoesNotContain(secret,json);Assert.DoesNotContain(BusinessFixture.Phrase,json);
        Assert.DoesNotContain("Fictional later assembly customer",json);Assert.Equal(DateTimeOffset.MinValue,f.Business.Owner.ExpiresAt);
        Assert.Contains(first.Tables,t=>t.Name=="Journal lines"&&t.Rows.Count>0);
        Assert.Contains(first.Tables,t=>t.Name=="Dashboard upcoming booking values"&&t.Rows.Count==1);
        Assert.Throws<NotSupportedException>(()=>((IList<ReadableExportGroup>)first.Groups).Clear());
    }

    [Fact]
    public void InvalidWindowsAndCancellationReturnNoAssembly()
    {
        using var f=new LedgerFixture();using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        Assert.Throws<ArgumentException>(()=>ReadableExportAssembly.Build(source,Dates with{FinancialFrom=Dates.FinancialThrough.AddDays(1)}));
        Assert.Throws<ArgumentException>(()=>ReadableExportAssembly.Build(source,Dates with{FinancialFrom=new(2017,1,1)}));
        Assert.Throws<ArgumentException>(()=>ReadableExportAssembly.Build(source,Dates with{EventFrom=Dates.PlanningAsOf.AddDays(-1)}));
        Assert.Throws<ArgumentException>(()=>ReadableExportAssembly.Build(source,Dates with{EventThrough=new(2037,3,31)}));
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        Assert.Throws<OperationCanceledException>(()=>ReadableExportAssembly.Build(source,Dates,cancel.Token));
        var budget=new ReadableExportBudget(cancellation:cancel.Token);
        Assert.Throws<OperationCanceledException>(()=>new ReadableTableBuilder(source.Source,budget,"Cancelled facts","Fact"));
        using var laterCancel=new CancellationTokenSource();
        var running=new ReadableTableBuilder(source.Source,new(cancellation:laterCancel.Token),"Running facts","Fact");
        laterCancel.Cancel();Assert.Throws<OperationCanceledException>(()=>running.Add("Fictional later row"));
    }

    [Fact]
    public void ACorruptedRequiredGroupPreventsReturningAPartialAssembly()
    {
        using var f=new LedgerFixture();var catalog=new ServiceCatalog(f.Business.Store);catalog.Initialize(f.Business.Owner);
        var service=catalog.List(f.Business.Owner).First();
        f.Business.Store.Transaction(db=>db.Execute("UPDATE Records SET DataJson=json_set(DataJson,'$.Id',$other) WHERE Id=$id;",
            ("$other",Guid.NewGuid().ToString()),("$id",service.Id)));
        using var source=new ReadableExportSnapshots(f.Business.Store).Capture(f.Business.Owner);
        Assert.Throws<InvalidDataException>(()=>ReadableExportAssembly.Build(source,Dates));
    }
}
