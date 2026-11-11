using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SarahBeauty_Desktop.Views;

internal static class ReportTable
{
    internal static UIElement Build(string[] headers,IEnumerable<string[]> rows)
    {
        var grid=new Grid{ColumnSpacing=12,RowSpacing=8,MinWidth=headers.Length*105};
        for(int i=0;i<headers.Length;i++)grid.ColumnDefinitions.Add(new(){Width=new GridLength(i==0?2:1,GridUnitType.Star)});
        AddRow(grid,headers,true);
        foreach(var row in rows)AddRow(grid,row,false);
        return new ScrollViewer{HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollMode=ScrollMode.Enabled,Content=grid};
    }

    private static void AddRow(Grid grid,string[] cells,bool heading)
    {
        int row=grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        for(int column=0;column<cells.Length;column++)
        {
            var text=Form.Text(cells[column]);
            if(heading)text.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;
            if(column>0)text.TextAlignment=TextAlignment.Right;
            Grid.SetRow(text,row);
            Grid.SetColumn(text,column);
            grid.Children.Add(text);
        }
    }
}
