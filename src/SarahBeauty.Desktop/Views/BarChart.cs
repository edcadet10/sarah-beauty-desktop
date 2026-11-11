using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SarahBeauty.Business.Rules;

namespace SarahBeauty_Desktop.Views;

internal static class BarChart
{
    internal static UIElement Build(string[] labels,long[] values,Brush brush)
    {
        var canvas=new Canvas{Width=900,Height=Math.Max(80,labels.Length*54+20),IsHitTestVisible=false};
        var range=ChartDrawing.Range(values.Select(v=>(long?)v));double X(double value)=>230+(value-range.Min)/(range.Max-range.Min)*500;
        ChartDrawing.Line(canvas,X(0),0,X(0),canvas.Height,ChartDrawing.Foreground);
        for(int i=0;i<labels.Length;i++)
        {
            double y=i*54+8,x=X(values[i]),zero=X(0);ChartDrawing.Label(canvas,labels[i],0,y,220);
            ChartDrawing.Rectangle(canvas,Math.Min(x,zero),y,Math.Max(1,Math.Abs(x-zero)),24,brush);ChartDrawing.Label(canvas,Money.Display(values[i]),750,y,145);
        }
        return ChartDrawing.Scroll(canvas);
    }
}
