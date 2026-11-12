using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Storage.Tests;

public sealed class InvoiceVoidRecoveryTests
{
    [Fact]
    public async Task VoidedInvoicePdfAndHistoricalBalanceSurviveEncryptedRestoreAndActivation()
    {
        using var ledger=new LedgerFixture();var f=ledger.Business;using var target=new TestStore();ledger.Post(ledger.Draft(FinancialKind.Receipt,12345));var invoice=ApplicationTests.Invoice(ledger,5000,.09m);byte[] pdf=File.ReadAllBytes(new DocumentArtifacts(f.Store).Render(f.Owner,invoice.Id));
        var decision=InvoiceVoidTests.Book(ledger).VoidInvoice(f.Owner,InvoiceVoidTests.Draft(invoice),BusinessFixture.Phrase,Guid.NewGuid().ToString());var material=await new RecoveryCipher().CreateAsync(BusinessFixture.Phrase);string path=Path.Combine(f.Files.Folder,"fictional-invoice-void.age");var archive=await new FullArchives(f.Store).CreateAsync(f.Owner,path,material.Recipient);
        using var review=await new ArchiveRestores().StageAsync(path,target.Folder,material.ProtectedIdentity,BusinessFixture.Phrase,archive);const string phrase="fictional orchard meadow lantern";string request=Guid.NewGuid().ToString();await new RecoveredStores().PrepareAsync(review,target.Folder,new(f.Owner.OwnerId,"Fictional invoice-void recovery review",true),phrase,request);var active=await new ActiveTestStores(target.Folder).ActivateAsync(request,f.Owner.OwnerId,phrase);
        Assert.Equal(decision,Assert.Single(new DocumentDecisions(active.Store).History(active.Owner)));var retained=new QuoteBook(active.Store).Versions(active.Owner).Single(v=>v.Id==invoice.Id);Assert.Equal("Void",retained.Status);Assert.Equal(invoice.Hash,retained.Hash);Assert.Equal(pdf,File.ReadAllBytes(new DocumentArtifacts(active.Store).VerifiedPath(active.Owner,invoice.Id)));
        var applications=new PaymentApplications(active.Store);Assert.Equal(5450,applications.Outstanding(active.Owner,new(2027,1,19)).Sum(d=>d.Cents));Assert.Empty(applications.Outstanding(active.Owner,new(2027,1,20)));Assert.NotEqual(f.Store.InstanceId,active.Store.InstanceId);Assert.Equal(12345L,TestStore.Scalar(active.Store.DatabasePath,"SELECT SUM(DebitCents) FROM JournalLines;"));Assert.Equal(12345L,TestStore.Scalar(active.Store.DatabasePath,"SELECT SUM(CreditCents) FROM JournalLines;"));
    }
}
