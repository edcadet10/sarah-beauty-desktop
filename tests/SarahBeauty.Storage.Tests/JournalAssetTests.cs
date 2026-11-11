using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Storage;

namespace SarahBeauty.Storage.Tests;

public sealed class JournalAssetTests
{
    [Fact]
    public void SchemaTwelveUpgradePreservesIndividualAssetLinksAndLeavesCashUnassigned()
    {
        using var f=new LedgerFixture();
        var purchase=f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        f.Business.Files.Execute("DROP INDEX JournalLines_Asset; ALTER TABLE JournalLines DROP COLUMN AssetId; PRAGMA user_version=12;");
        var reopened=new BusinessStore(f.Business.Files.DatabasePath);
        var lines=new FinanceBook(reopened).Journal(f.Business.Owner,purchase.Id);
        Assert.Equal(f.Asset.Id,lines.Single(l=>l.AccountId==f.Accounts["1500"].Id).AssetId);
        Assert.Empty(lines.Single(l=>l.AccountId==f.Accounts["1010"].Id).AssetId);
        Assert.Equal(30000,f.Balance("1500"));
        Assert.Single(f.Business.Files.Snapshots());
    }

    [Fact]
    public void OneBatchCanRetainTwoAssetIdentitiesAndItsExactReversalRestoresBoth()
    {
        using var f=new LedgerFixture();
        var second=new BusinessResources(f.Business.Store).SaveAsset(f.Business.Owner,new Asset{Name="Fictional second equipment",BusinessUseFraction=1m});
        var source=f.Book.SaveDraft(f.Business.Owner,f.Draft(FinancialKind.Expense,40000));
        var movement=f.Business.Store.Write(f.Business.Owner,"Fictional multi-asset writer probe",source.Id,db=>
        {
            List<JournalLine> lines=[new(f.Accounts["1500"].Id,30000,0,f.Asset.Id),new(f.Accounts["1500"].Id,10000,0,second.Id),new(f.Accounts["1010"].Id,0,40000)];
            lines=JournalAssets.Assign(db,f.Business.Store,f.Business.Owner,source,lines,f.Accounts.Values.ToList());
            var result=PostingWriter.Save(db,f.Business.Owner,source,lines,null);
            JournalAssets.AfterPosting(db,f.Business.Owner,lines);
            return result;
        });
        var reversal=f.Post(f.Draft(FinancialKind.Reversal,0) with{OriginalId=movement.Id});
        var restored=f.Book.Journal(f.Business.Owner,reversal.Id);
        Assert.Equal(30000,restored.Single(l=>l.AssetId==f.Asset.Id).CreditCents);
        Assert.Equal(10000,restored.Single(l=>l.AssetId==second.Id).CreditCents);
        Assert.Empty(restored.Single(l=>l.AccountId==f.Accounts["1010"].Id).AssetId);
        Assert.Equal(0,f.Balance("1500"));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Depreciation,1) with{AssetId=second.Id}));
    }
}
