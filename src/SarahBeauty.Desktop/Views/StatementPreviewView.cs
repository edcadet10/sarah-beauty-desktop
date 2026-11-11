using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Finance;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class StatementPreviewView
{
    private readonly TextBox _csv=new(){Header="CSV contents (UTF-8; header required)",AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,Height=180};
    private readonly StatementCsvFields _fields=new();
    private readonly StackPanel _preview=Form.Panel();
    private readonly StackPanel _review=Form.Panel();
    private readonly ListView _history=new(){MaxHeight=220};
    private readonly BusinessStore _store;
    private readonly OwnerSession _owner;
    private readonly StatementBook _book;
    private readonly List<Account> _accounts;
    private readonly StatementImportFields _importFields;
    private bool _pendingReview;

    internal StatementPreviewView(BusinessStore store,OwnerSession owner)
    {
        (_store,_owner,_book)=(store,owner,new(store));
        var chart=new ChartOfAccounts(store);chart.Initialize(owner);
        _accounts=chart.List(owner);_importFields=new(_accounts);
    }

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var status=Form.Text();
        panel.Add(Form.Title("Statements"),Form.Text("Open or paste a USD statement export. Review dates, signs, balances, and transaction identities before import. Import saves statement records; it does not post cash or income."),
            Form.Action("Choose statement CSV",status,async()=>{string? text=await StatementFilePicker.ReadCsv();if(text is not null)_csv.Text=text;}),
            _csv,_fields.Build(),status,Form.Action("Preview statement rows",status,async()=>
            {
                Show(await Parse());
            }),_preview,_importFields.Build(),Form.Action("Review statement import",status,async()=>
            {
                if(!await ReplaceReview())return;
                var draft=_importFields.Read(await Parse());
                var preview=await Task.Run(()=>_book.Preview(_owner,draft));
                _review.Children.Clear();
                _review.Add(StatementImportReview.Build(_book,_owner,preview,_accounts.Single(a=>a.Id==draft.AccountId).ToString(),Imported));
                _pendingReview=preview.ExistingImportId.Length==0;
            }),_review,Form.Title("Imported statements"),_history);
        Refresh();
        _history.SelectionChanged+=async(_,_)=>
        {
            try
            {
                if(_history.SelectedItem is not ImportItem item)return;
                if(await ReplaceReview())Imported(item.Value);
                else _history.SelectedItem=null;
            }
            catch(Exception error){status.Text=error.Message;}
        };
        return panel;
    }

    private Task<StatementSource> Parse()
    {
        string contents=_csv.Text;
        if(_importFields.NoActivity)
        {
            if(!string.IsNullOrWhiteSpace(contents))throw new ArgumentException("Clear CSV contents before confirming a no-activity statement.");
            return Task.FromResult(new StatementSource([],"","{\"Kind\":\"Confirmed no-activity statement\"}"));
        }
        var mapping=_fields.Read();
        return Task.Run(()=>StatementCsv.Parse(contents,mapping));
    }

    private void Refresh()=>_history.ItemsSource=_book.List(_owner).OrderByDescending(s=>s.Through).Select(s=>new ImportItem(s,
        $"{_accounts.Single(a=>a.Id==s.AccountId).Name} • {s.From:yyyy-MM-dd} – {s.Through:yyyy-MM-dd} • {s.RowCount} source rows • Closing {Money.Display(s.ClosingCents)}{(s.Withdrawn?" • Withdrawn":"")}")).ToList();

    private void Imported(StatementImport saved)
    {
        saved=_book.List(_owner).Single(s=>s.Id==saved.Id);
        var rows=_book.SourceRows(_owner,saved.Id);
        string actor=new OwnerAccess(_store).Owners().FirstOrDefault(o=>o.Id==saved.ApprovedBy).Name??"Former owner";
        _review.Children.Clear();
        _review.Add(StatementWithdrawalView.Build(_store,_owner,saved,()=>Imported(saved)));
        _review.Add(StatementImportHistory.Build(saved,rows,_accounts.Single(a=>a.Id==saved.AccountId).ToString(),actor));
        _pendingReview=false;
        Refresh();
    }

    private async Task<bool> ReplaceReview()
    {
        if(!_pendingReview)return true;
        var dialog=new ContentDialog{XamlRoot=_review.XamlRoot,Title="Replace the current statement review?",
            Content="Unsaved transaction identity decisions in this review will be discarded.",PrimaryButtonText="Replace review",CloseButtonText="Keep review",DefaultButton=ContentDialogButton.Close};
        return await dialog.ShowAsync()==ContentDialogResult.Primary;
    }

    private sealed record ImportItem(StatementImport Value,string Label){public override string ToString()=>Label;}

    private void Show(StatementSource source)
    {
        _preview.Children.Clear();
        int repeated=source.Rows.GroupBy(r=>(r.Date,r.SignedCents,r.Description,r.Reference)).Where(g=>g.Count()>1).Sum(g=>g.Count());
        _preview.Add(Form.Text($"Rows: {source.Rows.Length} • Money in: {Money.Display(source.InflowCents)} • Money out: {Money.Display(source.OutflowCents)} • Net change: {Money.Display(source.NetCents)}"),
            Form.Text($"Identical source rows: {repeated}. Each remains separate for review. Equal dates and amounts do not prove a duplicate."),
            ReportTable.Build(["Source row","Date","Signed USD","Description","Reference"],source.Rows.Take(250).Select(r=>new[]{r.SourceRow.ToString(),r.Date.ToString("yyyy-MM-dd"),Money.Display(r.SignedCents),r.Description,r.Reference})));
        if(source.Rows.Length>250)_preview.Add(Form.Text("Showing the first 250 rows. The totals include all parsed rows."));
    }
}
