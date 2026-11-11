using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class PurchaseTests
{
    [Fact]
    public void ClosedDatesBlockUnpaidBillApprovalAndPreviewWhilePreservingExactRetries()
    {
        using var f=new LedgerFixture();var book=new PurchaseBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f));
        f.Business.Files.Execute($"UPDATE BookSettings SET ClosedThrough='{f.Date:yyyy-MM-dd}';");
        Assert.Throws<ArgumentException>(()=>book.Preview(f.Business.Owner,draft));
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal("Draft",book.List(f.Business.Owner).Single().Status);
        f.Business.Files.Execute("UPDATE BookSettings SET ClosedThrough=NULL;");
        var approved=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        f.Business.Files.Execute($"UPDATE BookSettings SET ClosedThrough='{f.Date:yyyy-MM-dd}';");
        Assert.Equal(approved.Revision,book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Revision);
    }

    internal static Purchase Draft(LedgerFixture f)
    {
        var supplier=new BusinessResources(f.Business.Store).SaveSupplier(f.Business.Owner,new Supplier{Name="Fictional purchase supplier"});
        return new Purchase{Name="Fictional mixed bill",SupplierId=supplier.Id,PurchaseDate=f.Date,DueDate=f.Date.AddDays(30),
            Lines=[new(Guid.NewGuid().ToString(),"Fictional supplies",2,1000,200,0.5m,f.Accounts["5000"].Id),
                new(Guid.NewGuid().ToString(),"Fictional equipment",1,30000,3000,1m,f.Accounts["1500"].Id,f.Asset.Id)]};
    }

    [Fact]
    public void ApprovedUnpaidBillRetainsReviewedSharesWithoutPostingAnExpenseOrCash()
    {
        using var f=new LedgerFixture();
        var book=new PurchaseBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f));
        var approved=book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
        Assert.Equal(35200,approved.Lines.Sum(l=>l.GrossCents));
        Assert.Equal(34100,approved.Lines.Sum(l=>l.BusinessCents));
        Assert.Equal(f.Business.Owner.OwnerId,approved.ApprovedBy);
        Assert.Equal(approved.Revision,book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Revision);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Throws<ArgumentException>(()=>book.SaveDraft(f.Business.Owner,approved with{Name="Fictional attempted overwrite"}));
    }

    [Fact]
    public void UnknownTaxUseAndWrongAssetClassificationBlockApproval()
    {
        using var f=new LedgerFixture();
        var book=new PurchaseBook(f.Business.Store);
        var value=Draft(f);
        foreach(var line in new[]{value.Lines[0] with{TaxPaidCents=null},value.Lines[0] with{BusinessUseFraction=null},
            value.Lines[1] with{AccountId=f.Accounts["5010"].Id}})
        {
            var draft=book.SaveDraft(f.Business.Owner,value with{Id="",Revision=0,Lines=[line]});
            Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        }
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void ReliableSupplierInvoiceReferencesCannotBeApprovedTwice()
    {
        using var f=new LedgerFixture();
        var book=new PurchaseBook(f.Business.Store);
        var value=Draft(f) with{ReliableReference=true,SupplierReference="Fictional supplier invoice 42"};
        var first=book.SaveDraft(f.Business.Owner,value);
        var duplicate=book.SaveDraft(f.Business.Owner,value with{SupplierReference="  fictional SUPPLIER invoice 42  "});
        book.Approve(f.Business.Owner,first.Id,first.Revision,BusinessFixture.Phrase);
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,duplicate.Id,duplicate.Revision,BusinessFixture.Phrase));
        Assert.Single(book.List(f.Business.Owner),p=>p.Status=="Approved");
    }

    [Fact]
    public void FailedApprovalRollsBackItsStatusAndRetriesOnce()
    {
        using var f=new LedgerFixture();
        var book=new PurchaseBook(f.Business.Store);
        var draft=book.SaveDraft(f.Business.Owner,Draft(f));
        book.FailurePoint=_=>throw new IOException("Injected bill approval failure");
        Assert.Throws<IOException>(()=>book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase));
        Assert.Equal("Draft",book.List(f.Business.Owner).Single().Status);
        book.FailurePoint=null;
        Assert.Equal("Approved",book.Approve(f.Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase).Status);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
    }

    [Fact]
    public void AChangedBillNeedsFreshReviewAndCannotApproveItsEarlierRevision()
    {
        using var f=new LedgerFixture();
        var book=new PurchaseBook(f.Business.Store);
        var first=book.SaveDraft(f.Business.Owner,Draft(f));
        book.SaveDraft(f.Business.Owner,first with{DueDate=f.Date.AddDays(60)});
        Assert.Throws<ArgumentException>(()=>book.Approve(f.Business.Owner,first.Id,first.Revision,BusinessFixture.Phrase));
        Assert.Equal("Draft",book.List(f.Business.Owner).Single().Status);
    }
}
