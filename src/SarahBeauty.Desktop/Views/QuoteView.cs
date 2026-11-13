using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Bookings;
using SarahBeauty.Business.Customers;
using SarahBeauty.Business.Documents;
using SarahBeauty.Business.Rules;
using SarahBeauty.Business.Security;
using SarahBeauty.Business.Storage;

namespace SarahBeauty_Desktop.Views;

internal sealed class QuoteView(BusinessStore store,OwnerSession owner)
{
    private readonly QuoteBook _quotes=new(store);
    private readonly ListView _drafts=new(){MaxHeight=160};
    private readonly ListView _versions=new(){MaxHeight=180};
    private readonly StackPanel _detail=Form.Panel();
    private readonly TextBlock _status=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        var bookings=new BookingBook(store).List(owner);
        var booking=Form.Records("Booking",bookings.Select(b=>(b.Id,b.Name)));
        panel.Add(Form.Title("Quotes"),booking,Form.Action("Prepare quote",_status,()=>
        {
            Edit(_quotes.FromBooking(owner,bookings.SingleOrDefault(b=>b.Id==booking.SelectedId())??throw new ArgumentException("Choose a saved booking.")));
            return Task.CompletedTask;
        }),_status,Form.Text("Saved drafts"),_drafts,Form.Text("Issued versions"),_versions,_detail);
        _drafts.SelectionChanged+=(_,_)=>{if(_drafts.SelectedItem is DocumentDraft draft)Review(draft);};
        _versions.SelectionChanged+=(_,_)=>{if(_versions.SelectedItem is DocumentVersion version)ShowVersion(version);};
        Refresh();
        return panel;
    }

    private void Refresh()
    {
        _drafts.ItemsSource=_quotes.Drafts(owner).Where(d=>d.Kind==DocumentKind.Quote && d.Status=="Draft").ToList();
        _versions.ItemsSource=_quotes.Versions(owner).Where(v=>v.Snapshot.Kind==DocumentKind.Quote).ToList();
    }

    private void Edit(DocumentDraft draft)
    {
        _detail.Children.Clear();
        var recipients=Form.Records("Quote recipient",new CustomerBook(store).Search(owner,includeArchived:true).Select(c=>(c.Id,$"{c.FullName} • {c.Id[..8]}")),draft.RecipientCustomerId);
        var date=Form.Input("Issue date",draft.IssueDate?.ToString("yyyy-MM-dd")??"");
        var expiry=Form.Input("Quote expiry (optional)",draft.DueDate?.ToString("yyyy-MM-dd")??"");
        var terms=Form.MultilineInput("Terms for this quote version",draft.Terms);
        var status=Form.Text();
        DocumentDraft Read()=>draft with{RecipientCustomerId=recipients.SelectedId(),IssueDate=Input.Date(date.Text),DueDate=Input.Date(expiry.Text),Terms=terms.Text};
        _detail.Add(Form.Title("Quote draft"),recipients,date,expiry,terms,status,
            Form.Action("Refresh from current booking",status,()=>
            {
                var current=new BookingBook(store).List(owner).Single(b=>b.Id==draft.BookingId);
                Edit(Read() with{BookingRevision=current.Revision});
                return Task.CompletedTask;
            }),Form.Action("Save draft for review",status,async()=>
            {
                var value=Read();
                var saved=await Task.Run(()=>_quotes.SaveDraft(owner,value));
                Refresh();
                Review(saved);
            }));
    }

    private void Review(DocumentDraft draft)
    {
        _detail.Children.Clear();
        var status=Form.Text();
        _detail.Add(Form.Title("Review saved quote draft"));
        try{_detail.Add(Snapshot(_quotes.Preview(owner,draft)));}
        catch(Exception error){status.Text=error.Message;}
        var phrase=Form.Secret($"Confirm {owner.OwnerName}'s passphrase to issue");
        _detail.Add(status,Form.Action("Edit draft",status,()=>{Edit(draft);return Task.CompletedTask;}),phrase,
            Form.Action("Issue this saved quote",status,async()=>
            {
                string password=phrase.Password;
                phrase.Password="";
                var issued=await Task.Run(()=>_quotes.Issue(owner,draft.Id,draft.Revision,password));
                Refresh();
                ShowVersion(issued);
            }));
    }

    private void ShowVersion(DocumentVersion version)
    {
        _detail.Children.Clear();
        _detail.Add(Form.Title(version.ToString()),Snapshot(version.Snapshot),PdfArtifactView.Build(store,owner,version.Id),Form.Text("Open Document decisions to review retained decline, withdrawal or void history for this exact version."));
        var date=Form.Input("Acceptance date",DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"));
        var evidence=Form.Input("Acceptance evidence / reference (private)");
        var status=Form.Text();
        _detail.Add(status,Form.Action("Prepare a new version",status,()=>
        {
            var booking=new BookingBook(store).List(owner).Single(b=>b.Id==version.BookingId);
            Edit(_quotes.FromBooking(owner,booking,version.DocumentId) with{Terms=version.Snapshot.Terms});
            return Task.CompletedTask;
        }));
        if(version.Status!="Issued")return;
        _detail.Add(date,evidence,Form.Action("Record quote acceptance",status,async()=>
        {
            var acceptedDate=Input.Date(date.Text)??throw new ArgumentException("Enter the acceptance date.");
            string details=evidence.Text;
            var accepted=await Task.Run(()=>_quotes.Accept(owner,version.Id,acceptedDate,details));
            Refresh();
            ShowVersion(accepted);
        }));
    }

    internal static UIElement Snapshot(ClientSnapshot snapshot)
    {
        var panel=Form.Panel();
        panel.Add(Form.Text($"{snapshot.Mode} • {snapshot.Number} v{snapshot.Version} • {snapshot.IssueDate:yyyy-MM-dd}"),
            Form.Text($"To: {snapshot.Client.Name}\n{snapshot.Client.BillingName}\n{snapshot.Client.BillingAddress}"),
            Form.Text($"{snapshot.Event.Name} • {snapshot.Event.Date?.ToString("yyyy-MM-dd")??"Date undecided"} • {snapshot.Event.Venue}"),
            Form.Text(string.Join("\n",snapshot.Lines.Select(l=>$"{l.Description} • {l.Quantity} × {Money.Display(l.UnitCents)} • {Money.Display(l.TotalCents)}"))),
            Form.Text($"Net {Money.Display(snapshot.NetCents)} • Tax {Money.Display(snapshot.TaxCents)} • Total {Money.Display(snapshot.TotalCents)}"),
            Form.Text(string.Join("\n",snapshot.Milestones.Select(m=>$"{m.Kind}: {Money.Display(m.Cents)} • Due {m.DueDate?.ToString("yyyy-MM-dd")??"Undecided"}"))),Form.Text(snapshot.Terms));
        if(snapshot.RelatedDocument.Length>0)panel.Add(Form.Text("Related document: "+snapshot.RelatedDocument));
        if(snapshot.Receipt is ClientReceipt receipt)panel.Add(ReceiptSummary(receipt));
        return panel;
    }

    private static UIElement ReceiptSummary(ClientReceipt receipt)=>Form.Text(
        $"Received {receipt.PaymentDate:yyyy-MM-dd} • Method: {receipt.Method} • Reference: {receipt.Reference}\n"
        +string.Join("\n",receipt.Applications.Select(a=>$"{a.InvoiceNumber} • {a.Milestone}: {Money.Display(a.Cents)}"))
        +$"\nRefunded: {Money.Display(receipt.RefundCents)} • Unallocated: {Money.Display(receipt.PaymentAvailableCents)}"
        +$"\nUnused funded credit: {Money.Display(receipt.UnusedFundedCreditCents)} • Payer's open invoices: {Money.Display(receipt.CustomerInvoiceBalanceCents)}");
}
