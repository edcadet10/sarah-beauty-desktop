using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Purchasing;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class MoneyView
{
    private readonly BusinessStore _store;
    private readonly OwnerSession _owner;
    private readonly FinanceBook _book;
    private readonly List<Account> _accounts;
    private readonly ListView _list = new(){MaxHeight=220};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();
    private readonly ComboBox _kind=Form.Select("Movement",Enum.GetNames<FinancialKind>(),"Expense");
    private readonly ContentControl _host=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private FinancialDraft? _editing;
    private Func<bool> _hasChanges=()=>false;
    private ContentDialog? _discardDialog;
    private bool _restoringSelection,_busy;

    internal MoneyView(BusinessStore store,OwnerSession owner)
    {
        (_store,_owner,_book)=(store,owner,new(store));
        var chart=new ChartOfAccounts(store);
        chart.Initialize(owner);
        _accounts=chart.List(owner);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Title("Money and approvals"),Form.Text("Drafts do not change the books. Review and approve a saved draft to post it."),
            _kind,Form.Action("Prepare movement",_status,Prepare),_status,_list,_detail);
        _list.ItemsSource=_book.Drafts(_owner).OrderByDescending(d=>d.Status=="Draft").ToList();
        _list.SelectionChanged+=async(_,_)=>{try{await SelectSaved();}catch(Exception error){_status.Text=error.Message;}};
        panel.Unloaded+=(_,_)=>_discardDialog?.Hide();
        _host.Content=panel;return _host;
    }

    private async Task Prepare()
    {
        if(!await ConfirmDiscard()){if(_editing is{} prior)_kind.SelectedItem=prior.Kind.ToString();return;}
        Edit(new FinancialDraft{Kind=Enum.Parse<FinancialKind>((string)_kind.SelectedItem),
            Date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.Now,"Eastern Standard Time").DateTime)});
    }

    private async Task SelectSaved()
    {
        if(_restoringSelection||_busy||_list.SelectedItem is not FinancialDraft draft)return;
        if(draft.Id==_editing?.Id&&draft.Revision==_editing.Revision)return;
        SelectRow(_editing);
        if(await ConfirmDiscard())Review(draft);
    }

    private void SelectRow(FinancialDraft? draft)
    {
        _restoringSelection=true;
        try{_list.SelectedItem=draft is null?null:_list.Items.OfType<FinancialDraft>().FirstOrDefault(row=>row.Id==draft.Id&&row.Revision==draft.Revision);}
        finally{_restoringSelection=false;}
    }

    private async Task<bool> ConfirmDiscard()
    {
        if(_busy||_discardDialog is not null)return false;
        if(!_hasChanges())return true;
        var dialog=new ContentDialog{XamlRoot=_detail.XamlRoot,Title="Discard unsaved money draft?",
            Content="These money fields have not been saved. Discard them to continue.",PrimaryButtonText="Discard changes",
            CloseButtonText="Keep editing",DefaultButton=ContentDialogButton.Close};
        _discardDialog=dialog;
        try{return await dialog.ShowAsync()==ContentDialogResult.Primary;}finally{_discardDialog=null;}
    }

    private async Task RunBusy(Func<Task> action)
    {
        if(_busy||_discardDialog is not null)return;
        _busy=true;_host.IsEnabled=false;
        try{await action();}finally{_busy=false;_host.IsEnabled=true;}
    }

    private void Edit(FinancialDraft draft)
    {
        _detail.Children.Clear();
        var fields=new FinancialFields(_store,_owner,_accounts,draft);
        var body=fields.Build();var state=new DraftFieldState(body);
        _editing=draft;_hasChanges=()=>state.HasChanges;_kind.SelectedItem=draft.Kind.ToString();SelectRow(draft);
        var status=Form.Text();
        _detail.Add(Form.Title($"{draft.Kind} draft"),body,status,Form.Action("Save draft for review",status,async()=>
        {
            var value=fields.Read(draft);
            await RunBusy(async()=>
            {
                var saved=await Task.Run(()=>_book.SaveDraft(_owner,value));
                _list.ItemsSource=_book.Drafts(_owner);Review(saved);
            });
        }));
    }

    private void Review(FinancialDraft draft)
    {
        _editing=draft;_hasChanges=()=>false;_kind.SelectedItem=draft.Kind.ToString();SelectRow(draft);
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title(draft.Status=="Posted"?"Posted movement":"Review saved draft"),Form.Text(draft.ToString()));
        if(draft.EvidenceId.Length>0)_detail.Add(Form.Text("Evidence / decision notes: "+draft.EvidenceId));
        if(draft.EvidenceFile is { } file)_detail.Add(FinancialEvidenceView.Build(_store,_owner,file));
        if(draft.LoanLender is { } lender)_detail.Add(Form.Text("Captured loan lender: "+lender.LenderName+" • "+(lender.OwnerLenderId.Length>0?"Owner lender: "+lender.OwnerLenderName:"No owner-lender link recorded")));
        if(draft.PurchaseId.Length>0)_detail.Add(Form.Text("Bill: "+new PurchaseBook(_store).List(_owner).Single(p=>p.Id==draft.PurchaseId).Name+" • Funding: "+draft.PurchaseFunding));
        if(draft.Kind==FinancialKind.PurchaseRefund)_detail.Add(Form.Text("Refund destination: "+draft.SupplierRefundDestination));
        if(draft.Kind==FinancialKind.SupplierCreditUse)_detail.Add(Form.Text("Credit treatment: "+draft.SupplierCreditTreatment+" • Transfers existing supplier credit."));
        if(draft.Kind==FinancialKind.TaxRecovery)_detail.Add(Form.Text("Tax recovery outcome: "+draft.TaxRecoveryDestination));
        if(draft.Kind==FinancialKind.ReceiptClassification)_detail.Add(Form.Text($"Classifies existing received value. Tax already collected on this share: {Money.Display(draft.SourceTaxCents)}. No additional cash payment."));
        if(draft.TaxRefundAllocation is { } split)_detail.Add(Form.Text($"Refunded tax: liability reduction {Money.Display(split.LiabilityCents)} • Recoverable {Money.Display(split.RecoverableCents)} • Nonrecoverable {Money.Display(split.NonrecoverableCents)}"));
        if(draft.AssetAdjustments.Length>0)
        {
            var assets=new BusinessResources(_store).Assets(_owner);
            _detail.Add(Form.Text("Asset decisions: "+string.Join("; ",draft.AssetAdjustments.Select(c=>$"{assets.Single(a=>a.Id==c.AssetId).Name}: {c.Treatment}"))));
        }
        if(draft.SupplierRefundPortions.Length>0)
        {
            var bill=new PurchaseBook(_store).List(_owner).Single(p=>p.Id==draft.PurchaseId);
            _detail.Add(Form.Text("Selected refund lines: "+string.Join("; ",draft.SupplierRefundPortions.Select(p=>$"{bill.Lines.Single(l=>l.Id==p.LineId).Description}: {Money.Display(p.GrossCents)}"))));
        }
        try
        {
            if(draft.EconomicOwnerId.Length>0)_detail.Add(Form.Text("Owner involved: "+(new OwnerAccess(_store).Owners().FirstOrDefault(o=>o.Id==draft.EconomicOwnerId).Name??"Owner unavailable")));
            var lines=draft.Status=="Posted"?_book.Journal(_owner,draft.PostedBatchId):_book.Preview(_owner,draft);
            if(lines.Count==0)_detail.Add(Form.Text("No amounts change in the books. The approval and linked record history are retained."));
            _detail.Add(Form.Text(string.Join("\n",lines.Select(l=>$"{_accounts.Single(a=>a.Id==l.AccountId).Name} • Debit {Money.Display(l.DebitCents)} • Credit {Money.Display(l.CreditCents)}"))));
            if(draft.Status=="Draft")_detail.Add(ReceiptSimilarityView.Build(new ReceiptSimilarities(_store).Find(_owner,draft)));
        }
        catch(Exception error){status.Text=error.Message;}
        _detail.Add(status);
        if(draft.Status!="Draft")return;
        var phrase=Form.Secret($"Confirm {_owner.OwnerName}'s passphrase to approve");
        _detail.Add(Form.Action("Edit this draft",status,()=>{Edit(draft);return Task.CompletedTask;}),phrase,
            Form.Action("Approve saved draft",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                await RunBusy(async()=>
                {
                    await Task.Run(()=>_book.Approve(_owner,draft.Id,draft.Revision,password));
                    _list.ItemsSource=_book.Drafts(_owner);_editing=null;_hasChanges=()=>false;SelectRow(null);
                    _detail.Children.Clear();
                    _detail.Add(Form.Text("Posted once. The approver and time are recorded in the activity history."));
                });
            }));
    }
}
