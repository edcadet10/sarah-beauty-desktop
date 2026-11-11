using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

public sealed class ResourceTests
{
    [Fact]
    public void RegisterKeepsUnknownValuesAndDoesNotCreateFinancialMovements()
    {
        using var f=new LedgerFixture();
        var resources=new BusinessResources(f.Business.Store);
        var loan=resources.SaveLoan(f.Business.Owner,new Loan{Name="Fictional unknown loan"});
        var asset=resources.SaveAsset(f.Business.Owner,new Asset{Name="Fictional unvalued asset"});
        resources.SaveSupplier(f.Business.Owner,new Supplier{Name="Fictional supplier",Email="supplier@example.com"});
        Assert.Null(loan.OpeningPrincipalCents);
        Assert.Null(asset.CostCents);
        Assert.Null(asset.TaxBasisCents);
        Assert.Null(asset.BusinessUseFraction);
        Assert.Empty(f.Book.Posted(f.Business.Owner));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.LoanPayment,100) with{LoanId=loan.Id,PrincipalCents=100}));
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.AssetPurchase,100) with{AssetId=asset.Id}));
    }

    [Fact]
    public void PostedAssetBasisCannotBeOverwrittenAndArchivedAssetsCannotReceiveNewDepreciation()
    {
        using var f=new LedgerFixture();
        var resources=new BusinessResources(f.Business.Store);
        f.Post(f.Draft(FinancialKind.AssetPurchase,30000));
        Assert.Throws<ArgumentException>(()=>resources.SaveAsset(f.Business.Owner,f.Asset with{CostCents=40000}));
        var archived=resources.SaveAsset(f.Business.Owner,f.Asset with{Archived=true});
        Assert.Throws<ArgumentException>(()=>f.Post(f.Draft(FinancialKind.Depreciation,1000) with{AssetId=archived.Id}));
        Assert.Equal(30000,f.Balance("1500"));
    }

    [Fact]
    public void LoanOpeningDatesLendersAndStaleEditsAreValidated()
    {
        using var f=new LedgerFixture();
        var resources=new BusinessResources(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>resources.SaveLoan(f.Business.Owner,new Loan{Name="Fictional missing date",OpeningPrincipalCents=10000}));
        Assert.Throws<ArgumentException>(()=>resources.SaveLoan(f.Business.Owner,new Loan{Name="Fictional invalid lender",OwnerLenderId=Guid.NewGuid().ToString()}));
        resources.SaveLoan(f.Business.Owner,f.Loan with{Terms="Fictional revised terms"});
        Assert.Throws<InvalidOperationException>(()=>resources.SaveLoan(f.Business.Owner,f.Loan with{Terms="Fictional stale edit"}));
    }

    [Fact]
    public void SupplierContactValidationAndArchiveRetainThePermanentIdentity()
    {
        using var f=new LedgerFixture();
        var resources=new BusinessResources(f.Business.Store);
        Assert.Throws<ArgumentException>(()=>resources.SaveSupplier(f.Business.Owner,new Supplier{Name="Fictional invalid supplier",Email="not-an-email"}));
        var supplier=resources.SaveSupplier(f.Business.Owner,new Supplier{Name="Fictional supplier"});
        var archived=resources.SaveSupplier(f.Business.Owner,supplier with{Archived=true});
        Assert.Equal(supplier.Id,archived.Id);
        Assert.Equal(archived,Assert.Single(resources.Suppliers(f.Business.Owner)));
    }

    [Fact]
    public void PostedLoanCannotBeReassignedToAnOwnerByEditingItsRegister()
    {
        using var f=new LedgerFixture();
        f.Post(f.Draft(FinancialKind.LoanProceeds,10000));
        Assert.Throws<ArgumentException>(()=>new BusinessResources(f.Business.Store).SaveLoan(f.Business.Owner,
            f.Loan with{OwnerLenderId=f.Business.Owner.OwnerId}));
        Assert.Equal(-10000,f.Balance("2300"));
    }
}
