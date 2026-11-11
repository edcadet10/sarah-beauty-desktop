using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal sealed class StatementRowFields(StatementRowReview review,StatementRowChoice? saved)
{
    private readonly ComboBox _choice=new(){Header=$"Source row {review.Row.SourceRow} decision",MinWidth=200};
    private readonly TextBox _search=Form.Input($"Search existing matches for source row {review.Row.SourceRow}");
    private readonly TextBox _reason=Form.Input($"Source row {review.Row.SourceRow} review reason",saved?.Reason??"");
    private readonly TextBlock _matches=Form.Text();

    internal UIElement Build()
    {
        var panel=Form.Panel();
        panel.Add(Form.Text($"Source row {review.Row.SourceRow} • {review.Row.Date:yyyy-MM-dd} • {Money.Display(review.Row.SignedCents)} • {review.Row.Description} • {review.Row.Reference}"));
        if(!NeedsDecision)return panel;
        Update(saved?.ExistingLineId??"");
        _search.TextChanged+=(_,_)=>Update((_choice.SelectedItem as Choice)?.Id??"");
        panel.Add(Form.Text($"Existing candidates: {review.Candidates.Length}. Similar source rows: {(review.SimilarInSource?"Yes":"No")}. Choose the actual transaction identity and explain the decision."),_search,_choice,_matches,_reason);
        return panel;
    }

    private bool NeedsDecision=>review.Candidates.Length>0 || review.SimilarInSource;

    private void Update(string selected)
    {
        var matches=review.Candidates.Where(c=>c.Description.Contains(_search.Text,StringComparison.OrdinalIgnoreCase)
            || c.Reference.Contains(_search.Text,StringComparison.OrdinalIgnoreCase) || c.Id.Contains(_search.Text,StringComparison.OrdinalIgnoreCase)).Take(30).ToList();
        var kept=review.Candidates.SingleOrDefault(c=>c.Id==selected);
        if(kept is not null && matches.All(c=>c.Id!=kept.Id))matches.Add(kept);
        var choices=new[]{new Choice("","New distinct real transaction")}.Concat(matches.Select(c=>new Choice(c.Id,$"Existing: {c.Description} • {c.Reference} • {c.Id[..8]}"))).ToArray();
        _choice.ItemsSource=choices;
        _choice.SelectedItem=choices.SingleOrDefault(c=>c.Id==selected)??choices[0];
        _matches.Text="Showing up to 30 matching candidates and the current selection. Search by description, reference, or record code.";
    }

    internal StatementRowChoice? Read()=>NeedsDecision?new(review.Row.SourceRow,(_choice.SelectedItem as Choice)?.Id??"",_reason.Text):null;
    private sealed record Choice(string Id,string Label){public override string ToString()=>Label;}
}
