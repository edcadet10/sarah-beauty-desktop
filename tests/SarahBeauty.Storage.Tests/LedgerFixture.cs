using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;

namespace SarahBeauty.Storage.Tests;

internal sealed class LedgerFixture : IDisposable
{
    internal BusinessFixture Business { get; } = new();
    internal FinanceBook Book { get; }
    internal Dictionary<string,Account> Accounts { get; }
    internal string CustomerId { get; }
    internal Loan Loan { get; }
    internal Asset Asset { get; }
    internal DateOnly Date { get; } = new(2027,1,10);

    internal LedgerFixture()
    {
        Book = new(Business.Store);
        var chart = new ChartOfAccounts(Business.Store);
        chart.Initialize(Business.Owner);
        Accounts = chart.List(Business.Owner).ToDictionary(a=>a.Code);
        CustomerId = new CustomerBook(Business.Store).Save(Business.Owner,new Customer{FullName="Fictional payer"}).Id;
        var resources = new BusinessResources(Business.Store);
        Loan = resources.SaveLoan(Business.Owner,new Loan{Name="Fictional lender",OpeningPrincipalCents=0});
        Asset = resources.SaveAsset(Business.Owner,new Asset{Name="Fictional equipment",BusinessUseFraction=1m});
    }

    internal FinancialDraft Draft(FinancialKind kind,long cents) => new()
    {
        Kind=kind,Name="Fictional "+kind,Date=Date,AmountCents=cents,TaxResolved=true,
        CashAccountId=Accounts["1010"].Id,
        CounterAccountId=Accounts[kind is FinancialKind.Receipt or FinancialKind.Refund ? "4000"
            : kind is FinancialKind.AssetPurchase or FinancialKind.AssetContribution ? "1500" : "5010"].Id,
        CustomerId=kind is FinancialKind.Receipt or FinancialKind.Refund ? CustomerId : "",
        EconomicOwnerId=kind is FinancialKind.Contribution or FinancialKind.Withdrawal or FinancialKind.OwnerPaidExpense
            or FinancialKind.OwnerReimbursement or FinancialKind.AssetContribution ? Business.Owner.OwnerId : "",
        LoanId=kind is FinancialKind.LoanProceeds or FinancialKind.LoanPayment ? Loan.Id : "",
        AssetId=kind is FinancialKind.AssetPurchase or FinancialKind.AssetContribution or FinancialKind.Depreciation ? Asset.Id : ""
    };

    internal PostedMovement Post(FinancialDraft value)
    {
        var draft=Book.SaveDraft(Business.Owner,value);
        return Book.Approve(Business.Owner,draft.Id,draft.Revision,BusinessFixture.Phrase);
    }

    internal long Balance(string code) => new LedgerReports(Business.Store)
        .TrialBalance(Business.Owner,DateOnly.MinValue,DateOnly.MaxValue).Single(r=>r.Account.Code==code).Ending;
    public void Dispose()=>Business.Dispose();
}
