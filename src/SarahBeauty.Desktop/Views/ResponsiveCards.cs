using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SarahBeauty_Desktop.Views;

internal static class ResponsiveCards
{
    internal static UIElement Build(IEnumerable<UIElement> cards,int maximumColumns=3)
    {
        var grid=new Grid{ColumnSpacing=16,RowSpacing=16};
        foreach(var card in cards)grid.Children.Add(card);
        Arrange(grid,0,maximumColumns);
        grid.SizeChanged+=(_,args)=>Arrange(grid,args.NewSize.Width,maximumColumns);
        return grid;
    }

    private static void Arrange(Grid grid,double width,int maximumColumns)
    {
        int columns=Math.Clamp((int)((width+16)/276),1,maximumColumns);
        int rows=(grid.Children.Count+columns-1)/columns;
        if(grid.ColumnDefinitions.Count==columns&&grid.RowDefinitions.Count==rows)return;
        grid.ColumnDefinitions.Clear();grid.RowDefinitions.Clear();
        for(int i=0;i<columns;i++)grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        for(int i=0;i<rows;i++)grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        for(int i=0;i<grid.Children.Count;i++){var card=(FrameworkElement)grid.Children[i];Grid.SetRow(card,i/columns);Grid.SetColumn(card,i%columns);}
    }
}
