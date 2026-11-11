using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace SarahBeauty_Desktop.Views;

internal static class LineChart
{
    internal static UIElement Build(string[] labels,long?[] values)
    {
        var canvas=new Canvas{Width=Math.Max(800,ChartDrawing.Left+labels.Length*100),Height=310,IsHitTestVisible=false};
        var range=ChartDrawing.Range(values);ChartDrawing.Axes(canvas,range);double step=(canvas.Width-ChartDrawing.Left-15)/Math.Max(1,labels.Length);
        (double X,double Y)? previous=null;
        for(int i=0;i<labels.Length;i++)
        {
            double x=ChartDrawing.Left+step*(i+0.5);ChartDrawing.Label(canvas,labels[i],x-42,ChartDrawing.Top+ChartDrawing.Height+12,84);
            if(values[i] is not long cents){previous=null;continue;}double y=ChartDrawing.Y(cents,range);
            if(previous.HasValue)ChartDrawing.Line(canvas,previous.Value.X,previous.Value.Y,x,y,ChartDrawing.Green,3);
            var point=new Ellipse{Width=8,Height=8,Fill=ChartDrawing.Green};Canvas.SetLeft(point,x-4);Canvas.SetTop(point,y-4);canvas.Children.Add(point);previous=(x,y);
        }
        return ChartDrawing.Scroll(canvas);
    }
}
