using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SarahBeauty.Business.Banking;

namespace SarahBeauty_Desktop.Views;

internal sealed class StatementReviewRows(StatementRowReview[] reviews)
{
    private const int PageSize=25;
    private readonly Dictionary<int,StatementRowChoice> _choices=[];
    private readonly List<StatementRowFields> _fields=[];
    private readonly StackPanel _page=Form.Panel();
    private readonly TextBlock _position=Form.Text();
    private int _offset;

    internal UIElement Build()
    {
        if(reviews.Length==0)return Form.Text("No transaction rows. The statement is being confirmed as having no activity.");
        var panel=Form.Panel();
        var status=Form.Text();
        panel.Add(Form.Action("Use matching unique bank references",status,()=>
        {
            SavePage();
            foreach(var review in reviews.Where(r=>r.Row.ReliableReference))
            {
                var matches=review.Candidates.Where(c=>c.ReliableReference && c.Reference==review.Row.Reference && c.Date==review.Row.Date && c.SignedCents==review.Row.SignedCents).ToArray();
                if(matches.Length==1)_choices[review.Row.SourceRow]=new(review.Row.SourceRow,matches[0].Id,"Same unique bank reference in the reviewed overlapping statement");
            }
            Show();return Task.CompletedTask;
        }),Form.Action("Previous source rows",status,()=>{Move(-PageSize);return Task.CompletedTask;}),
            Form.Action("Next source rows",status,()=>{Move(PageSize);return Task.CompletedTask;}),_position,status,_page);
        Show();
        return panel;
    }

    private void Move(int change)
    {
        SavePage();
        _offset=Math.Clamp(_offset+change,0,((reviews.Length-1)/PageSize)*PageSize);
        Show();
    }

    private void Show()
    {
        _page.Children.Clear();_fields.Clear();
        _position.Text=$"Showing rows {_offset+1}–{Math.Min(_offset+PageSize,reviews.Length)} of {reviews.Length}. Decisions stay saved while you change pages.";
        foreach(var row in reviews.Skip(_offset).Take(PageSize))
        {
            var fields=new StatementRowFields(row,_choices.GetValueOrDefault(row.Row.SourceRow));
            _fields.Add(fields);_page.Add(fields.Build());
        }
    }

    private void SavePage()
    {
        foreach(var fields in _fields)
            if(fields.Read() is { } choice)_choices[choice.SourceRow]=choice;
    }

    internal StatementRowChoice[] Read(){SavePage();return _choices.Values.OrderBy(c=>c.SourceRow).ToArray();}
}
