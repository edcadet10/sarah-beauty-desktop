using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SarahBeauty_Desktop.Views;

internal static class ColumnChart
{
    internal static UIElement Build(string[] labels,ChartSeries[] series)
    {
        var canvas=new Canvas{Width=Math.Max(800,ChartDrawing.Left+labels.Length*100),Height=310,IsHitTestVisible=false};
        var range=ChartDrawing.Range(series.SelectMany(s=>s.Values));ChartDrawing.Axes(canvas,range);double group=(canvas.Width-ChartDrawing.Left-15)/Math.Max(1,labels.Length);
        double bar=Math.Min(22,(group-20)/Math.Max(1,series.Length));
        for(int i=0;i<labels.Length;i++)
        {
            double center=ChartDrawing.Left+group*(i+0.5);ChartDrawing.Label(canvas,labels[i],center-42,ChartDrawing.Top+ChartDrawing.Height+12,84);
            for(int j=0;j<series.Length;j++)
            {
                if(series[j].Values[i] is not long cents)continue;double y=ChartDrawing.Y(cents,range),zero=ChartDrawing.Y(0,range);
                ChartDrawing.Rectangle(canvas,center-series.Length*bar/2+j*bar,Math.Min(y,zero),bar-2,Math.Abs(y-zero),series[j].Brush);
            }
        }
        var panel=Form.Panel();panel.Add(ChartDrawing.Legend(series),ChartDrawing.Scroll(canvas));return panel;
    }
}
