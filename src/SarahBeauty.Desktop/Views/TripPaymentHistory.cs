using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Resources;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed partial class TripPaymentView
{
    private void History(TripReimbursementReview review)
    {
        _result.Add(PagedReport.Build("Retained trip payment links",["Link date / payment","Linked amount","Remaining at date / after all actions","Original claim / evidence"],review.Links.Select(r=>new[]{
            $"{r.Source.Snapshot.Draft.Date:yyyy-MM-dd} • {r.Source.Snapshot.Payment.Source.Name}\n{r.Source.Id}",Money.Display(r.Source.Snapshot.Draft.Cents),
            $"{Money.Display(r.RemainingCents)} / {Money.Display(r.RemainingAfterAllDatedActionsCents)}",
            $"{(r.CurrentBasis?"Current":"Earlier")} claim {Money.Display(r.Source.Snapshot.Trip.RequestedReimbursementCents!.Value)}\n{r.Source.Snapshot.Draft.PayeeReview}\n{r.Source.Snapshot.Draft.EvidenceId}\n{Actor(r.Source.ApprovedBy)} • {r.Source.AtUtc}"})),
            PagedReport.Build("Retained trip payment releases",["Release date / link","Amount","Reason / evidence","Owner / time"],review.Releases.Select(r=>new[]{
                $"{r.Snapshot.Draft.Date:yyyy-MM-dd} • {r.Snapshot.Draft.LinkId}",Money.Display(r.Snapshot.Draft.Cents),$"{r.Snapshot.Draft.Reason}\n{r.Snapshot.Draft.EvidenceId}",$"{Actor(r.ApprovedBy)}\n{r.AtUtc}"})));
        var source=new SearchableRecords("Original trip reimbursement link");var detail=Form.Panel();var status=Form.Text();
        source.Update(review.Links.Select(r=>(r.Source.Id,$"{r.Source.Snapshot.Draft.Date:yyyy-MM-dd} • {r.Source.Snapshot.Payment.Source.Name} • Remaining after all actions {Money.Display(r.RemainingAfterAllDatedActionsCents)} • {r.Source.Id[..8]}")));
        var panel=Form.Panel();panel.Add(source.Build(),status,Form.Action("Review original trip payment details",status,async()=>
        {
            var chosen=review.Links.SingleOrDefault(r=>r.Source.Id==source.SelectedId)??throw new ArgumentException("Choose the original reimbursement link.");
            if(!await confirmDiscard(_releaseHasChanges()||_approval.Children.Count>0))return;
            ++_reviewVersion;_approval.Children.Clear();detail.Children.Clear();Original(chosen.Source,detail);
        }),detail);
        _result.Add(new Expander{Header="Original payment detail and release",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch});
    }

    private void Original(LinkedTripPayment link,StackPanel detail)
    {
        var source=link.Snapshot;var trip=source.Trip;
        detail.Add(Form.Title("Retained trip payment source"),Form.Text($"Original driver: {Driver(trip)}\n{trip.TripDate:yyyy-MM-dd} • {trip.Origin} → {trip.Destination}\n"
            +$"{trip.ActualMiles} actual miles • Reviewed request {Money.Display(trip.RequestedReimbursementCents!.Value)}\n"
            +$"Approved payment {source.Payment.Id}: {source.Payment.Source.Name} • {Money.Display(source.Payment.Source.AmountCents)}\n"
            +$"Linked {Money.Display(source.Draft.Cents)} on {source.Draft.Date:yyyy-MM-dd}\nPayee review: {source.Draft.PayeeReview}\nEvidence: {source.Draft.EvidenceId}\nApproved by {Actor(link.ApprovedBy)} at {link.AtUtc}"));
        var date=Form.Input("Trip link release date (YYYY-MM-DD)",_asOf.Text);var amount=Form.Input("Trip link amount to release in USD");
        var reason=Form.Input("Reason to release trip payment attribution");var evidence=Form.Input("Trip payment release evidence");var status=Form.Text();int version=_version;
        string initialDate=date.Text;
        _releaseHasChanges=()=>date.Text!=initialDate||amount.Text.Length>0||reason.Text.Length>0||evidence.Text.Length>0;
        TripPaymentReleaseDraft Read()=>new(link.Id,Input.Date(date.Text)??throw new ArgumentException("Enter the release date."),Money.Parse(amount.Text),reason.Text,evidence.Text);
        var fields=Form.Panel();fields.Add(Form.Text("Release corrects payment attribution. It does not refund cash or undo the actual expense/payment. Record any actual financial return separately after resolving its links."),
            date,amount,reason,evidence,status,Form.Action("Review trip payment release",status,async()=>
            {
                RequireSavedTrip();var draft=Read();int selection=++_reviewVersion;
                var preview=await Task.Run(()=>_book.PreviewRelease(owner,draft));if(version!=_version || selection!=_reviewVersion)return;
                _approval.Children.Clear();string request=Guid.NewGuid().ToString();
                _approval.Add(Form.Title("Review captured trip payment release"),Form.Text($"Release {Money.Display(draft.Cents)} from link {link.Id} on {draft.Date:yyyy-MM-dd}\n"
                    +$"Remaining before release: {Money.Display(preview.RemainingCents)}\nReason: {draft.Reason}\nEvidence: {draft.EvidenceId}"),
                    Form.Action("Edit trip payment release",_status,()=>{++_reviewVersion;_approval.Children.Clear();return Task.CompletedTask;}),
                    Approval("Approve trip payment release",async phrase=>
                    {
                        if(!await confirmDiscard(_linkFields?.HasUnsavedChanges==true))return;
                        RequireSavedTrip();if(Read()!=draft)throw new ArgumentException("The release fields changed. Review them again before approval.");
                        await Task.Run(()=>_book.Release(owner,draft,preview.Watermark,request,phrase));
                        if(version==_version && selection==_reviewVersion)await Reload();_status.Text="Trip payment attribution released. Existing books and cash are unchanged.";
                    }));
            }));detail.Add(fields);
    }
}
