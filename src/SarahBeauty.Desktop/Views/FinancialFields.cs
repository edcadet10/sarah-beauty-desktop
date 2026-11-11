using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class FinancialFields
{
    private readonly TextBox _memo,_date,_amount,_tax,_fee,_principal,_interest,_reference,_provider,_evidence,_method;
    private readonly ComboBox _cash,_counter,_to,_customer,_economicOwner,_booking,_loan,_asset,_supplier,_original,_credit;
    private readonly SupplierPaymentFields _supplierPayment;
    private readonly AssetRefundFields _assetRefund;
    private readonly SupplierRefundFields _supplierRefund;
    private readonly TaxRefundFields _taxRefund;
    private readonly CheckBox _taxResolved,_reliable,_openingCredit;
    private readonly FinancialKind _kind;

    internal FinancialFields(BusinessStore store,OwnerSession owner,List<Account> accounts,FinancialDraft d)
    {
        _kind=d.Kind;
        _memo=Form.Input("Description / reason",d.Name);
        _date=Form.Input("Transaction date (YYYY-MM-DD)",d.Date?.ToString("yyyy-MM-dd")??"");
        string amountLabel=d.Kind switch
        {
            FinancialKind.AssetDisposal=>"Gross disposal proceeds in USD (zero for a documented write-off)",
            FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse=>"Total supplier amount in USD (including any personal share)",
            FinancialKind.TaxRecovery=>"Tax recovery amount in USD",
            _=>"Business amount in USD (gross for a receipt)"
        };
        _amount=Form.Input(amountLabel,Money.Input(d.AmountCents));
        _cash=Form.Records("Actual cash / bank / processor account",accounts.Where(a=>a.CashKind!="None"&&a.Active).Select(a=>(a.Id,a.ToString())),d.CashAccountId);
        _counter=Form.Records(d.Kind==FinancialKind.Opening?"Account whose opening is being approved":"Classification account",
            accounts.Where(a=>a.Active && (d.Kind==FinancialKind.Opening || a.CashKind=="None")).Select(a=>(a.Id,a.ToString())),d.CounterAccountId);
        _to=Form.Records("Receiving cash account",accounts.Where(a=>a.CashKind!="None"&&a.Active).Select(a=>(a.Id,a.ToString())),d.ToAccountId);
        _customer=Form.Records("Payer / customer",new CustomerBook(store).Search(owner,includeArchived:true).Select(c=>(c.Id,$"{c.FullName} • {c.Id[..8]}")),d.CustomerId);
        _economicOwner=Form.Records("Owner involved in this movement",new OwnerAccess(store).Owners(),d.EconomicOwnerId);
        _booking=Form.Records("Related booking",new BookingBook(store).List(owner).Select(b=>(b.Id,b.Name)),d.BookingId);
        var resources=new BusinessResources(store);
        _loan=Form.Records("Loan",resources.Loans(owner).Select(l=>(l.Id,l.Name)),d.LoanId);
        _asset=Form.Records("Asset",resources.Assets(owner).Select(a=>(a.Id,a.Name)),d.AssetId);
        _supplier=Form.Records("Supplier",resources.Suppliers(owner).Select(s=>(s.Id,s.Name)),d.SupplierId);
        var movements=new FinanceBook(store).Posted(owner).Where(p=>d.Kind is not (FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse)
            || p.Source.Kind is FinancialKind.PurchasePayment or FinancialKind.SupplierCreditUse);
        if(d.Kind==FinancialKind.TaxRecovery)movements=movements.Where(p=>p.Source.Kind==FinancialKind.Refund && p.Source.TaxRefundAllocation?.RecoverableCents>0);
        _original=Form.Records(d.Kind==FinancialKind.TaxRecovery?"Customer refund with reviewed recoverable tax":
            d.Kind is FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse?"Source supplier payment or prior credit use":"Original posted movement",
            movements.Select(p=>(p.Id,$"{p.Source} • {p.Id[..8]}")),d.OriginalId);
        _credit=Form.Records("Linked funded credit (for a refund from reserved value)",new CreditBook(store).Credits(owner).Select(c=>(c.Id,$"{c.CustomerName} • {c.Id[..8]}")),d.CreditId);
        _supplierPayment=new SupplierPaymentFields(store,owner,d,_cash,_economicOwner);
        _assetRefund=new AssetRefundFields(store,owner,d,_original);
        _supplierRefund=new SupplierRefundFields(store,owner,d,_original,_date);
        _taxRefund=new TaxRefundFields(d,_cash);
        _tax=Form.Input("Included sales tax in USD",Money.Input(d.TaxCents));
        _taxResolved=new CheckBox{Content="I have explicitly resolved the tax treatment",IsChecked=d.TaxResolved};
        _fee=Form.Input("Processor / loan fee in USD",Money.Input(d.FeeCents));
        _method=Form.Input("Payment method (cash, Zelle, card, transfer, or other; no account numbers)",d.PaymentMethod);
        _principal=Form.Input("Loan principal paid in USD",Money.Input(d.PrincipalCents));
        _interest=Form.Input("Loan interest paid in USD",Money.Input(d.InterestCents));
        _reference=Form.Input("External reference",d.ExternalReference);
        _provider=Form.Input("Reference provider",d.Provider);
        _reliable=new CheckBox{Content="This reference uniquely identifies the real movement",IsChecked=d.ReliableReference};
        _openingCredit=new CheckBox{Content="Opening credit balance (liability / equity)",IsChecked=d.OpeningCredit};
        _evidence=Form.Input("Managed evidence reference (optional)",d.EvidenceId);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(_memo,_date,_amount);
        if(_kind!=FinancialKind.Opening)panel.Add(_cash);
        if(_kind is not (FinancialKind.AssetDisposal or FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse or FinancialKind.TaxRecovery))panel.Add(_counter);
        if(_kind is FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse)panel.Add(_supplierPayment.Build());
        if(_kind==FinancialKind.Transfer)panel.Add(_to);
        if(_kind is FinancialKind.Receipt or FinancialKind.Refund or FinancialKind.AssetDisposal)panel.Add(_customer,_tax,_taxResolved);
        if(_kind is FinancialKind.Receipt or FinancialKind.LoanPayment)panel.Add(_fee);
        if(_kind==FinancialKind.LoanPayment)panel.Add(_principal,_interest);
        if(_kind is FinancialKind.Contribution or FinancialKind.Withdrawal or FinancialKind.OwnerPaidExpense or FinancialKind.OwnerReimbursement or FinancialKind.AssetContribution or FinancialKind.Opening)panel.Add(_economicOwner);
        if(_kind is FinancialKind.LoanProceeds or FinancialKind.LoanPayment or FinancialKind.Opening)panel.Add(_loan);
        if(_kind is FinancialKind.AssetPurchase or FinancialKind.AssetContribution or FinancialKind.Depreciation or FinancialKind.AssetDisposal or FinancialKind.Opening)panel.Add(_asset);
        if(_kind is FinancialKind.Expense or FinancialKind.SupplierRefund)panel.Add(_supplier);
        if(_kind is FinancialKind.Refund or FinancialKind.SupplierRefund or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse or FinancialKind.Reversal or FinancialKind.TaxRecovery)panel.Add(_original);
        if(_kind is FinancialKind.Refund or FinancialKind.TaxRecovery)panel.Add(_taxRefund.Build());
        if(_kind==FinancialKind.PurchaseRefund)panel.Add(_supplierRefund.Build());
        if(_kind is FinancialKind.SupplierRefund or FinancialKind.PurchaseRefund or FinancialKind.SupplierCreditUse)panel.Add(_assetRefund.Build());
        if(_kind==FinancialKind.Refund)panel.Add(_credit);
        if(_kind is FinancialKind.Receipt or FinancialKind.UnclassifiedReceipt or FinancialKind.Refund or FinancialKind.AssetDisposal or FinancialKind.PurchasePayment or FinancialKind.PurchaseRefund or FinancialKind.TaxRecovery)panel.Add(_method);
        if(_kind==FinancialKind.Opening)panel.Add(_openingCredit,Form.Text("Approve a documented starting book value. Link an individual loan, asset, or owner when its account requires one. This does not import earlier business activity. Confirm zero cash balances in Opening balances."));
        panel.Add(_booking,_reference,_provider,_reliable,_evidence,Form.Text("Missing evidence remains visible for review. It does not prove that a payment or expense occurred."));
        return panel;
    }

    internal FinancialDraft Read(FinancialDraft d)=>_taxRefund.Read(_supplierRefund.Read(_assetRefund.Read(_supplierPayment.Read(d with
    {
        Name=_memo.Text,Date=Input.Date(_date.Text),AmountCents=Money.Parse(_amount.Text),TaxCents=Money.Parse(_tax.Text),
        TaxResolved=_taxResolved.IsChecked==true,FeeCents=Money.Parse(_fee.Text),PrincipalCents=Money.Parse(_principal.Text),InterestCents=Money.Parse(_interest.Text),
        CashAccountId=_cash.SelectedId(),CounterAccountId=_counter.SelectedId(),ToAccountId=_to.SelectedId(),
        CustomerId=_customer.SelectedId(),EconomicOwnerId=_economicOwner.SelectedId(),BookingId=_booking.SelectedId(),
        LoanId=_loan.SelectedId(),AssetId=_asset.SelectedId(),SupplierId=_supplier.SelectedId(),OriginalId=_original.SelectedId(),
        CreditId=_kind==FinancialKind.Refund?_credit.SelectedId():d.CreditId,
        PaymentMethod=_method.Text.Trim(),
        ExternalReference=_reference.Text,Provider=_provider.Text,ReliableReference=_reliable.IsChecked==true,
        OpeningCredit=_openingCredit.IsChecked==true,EvidenceId=_evidence.Text.Trim()
    }))));
}
