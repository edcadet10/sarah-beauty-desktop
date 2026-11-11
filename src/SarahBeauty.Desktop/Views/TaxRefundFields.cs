using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class TaxRefundFields
{
    private readonly CheckBox _explicit=new(){Content="Specify refunded tax treatment"};
    private readonly TextBox _liability,_recoverable,_nonrecoverable;
    private readonly ComboBox _destination;
    private readonly FinancialKind _kind;

    internal TaxRefundFields(FinancialDraft draft,ComboBox cash)
    {
        _kind=draft.Kind;
        _explicit.IsChecked=draft.TaxRefundAllocation is not null;
        var split=draft.TaxRefundAllocation??new(draft.TaxCents,0,0);
        _liability=Form.Input("Reduce remaining sales tax payable in USD",Money.Input(split.LiabilityCents));
        _recoverable=Form.Input("Reviewed recoverable tax in USD",Money.Input(split.RecoverableCents));
        _nonrecoverable=Form.Input("Nonrecoverable refunded tax in USD",Money.Input(split.NonrecoverableCents));
        _destination=Form.Select("Actual tax recovery outcome",[TaxRecoveryDestination.Cash,TaxRecoveryDestination.Offset,TaxRecoveryDestination.WriteOff],draft.TaxRecoveryDestination);
        void ShowCash(){if(_kind==FinancialKind.TaxRecovery)cash.Visibility=_destination.SelectedItem as string==TaxRecoveryDestination.Cash?Visibility.Visible:Visibility.Collapsed;}
        _destination.SelectionChanged+=(_,_)=>ShowCash();
        ShowCash();
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        if(_kind==FinancialKind.TaxRecovery)panel.Add(_destination,Form.Text("Link the customer refund that created the recoverable tax. Record an actual cash recovery, an applied tax credit against liability, or an evidenced write-off. These actions create no service income and do not mark a return filed."));
        else panel.Add(_explicit,Form.Text("Without a split, all refunded tax reduces remaining liability. If tax was already remitted, review how much is recoverable and how much the business bears. The three parts must equal the tax refunded to the customer. Record the supporting decision in the evidence field."),_liability,_recoverable,_nonrecoverable);
        return new Expander{Header=_kind==FinancialKind.TaxRecovery?"Tax recovery decision":"Refunded tax treatment",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }

    internal FinancialDraft Read(FinancialDraft draft)=>draft.Kind switch
    {
        FinancialKind.Refund=>draft with{TaxRefundAllocation=_explicit.IsChecked==true?
            new(Money.Parse(_liability.Text),Money.Parse(_recoverable.Text),Money.Parse(_nonrecoverable.Text)):null},
        FinancialKind.TaxRecovery=>draft with{TaxRecoveryDestination=(string)_destination.SelectedItem},
        _=>draft
    };
}
