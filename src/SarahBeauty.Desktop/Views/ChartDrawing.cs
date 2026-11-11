using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace SarahBeauty_Desktop.Views;

internal sealed record ChartSeries(string Name,long?[] Values,Brush Brush);

internal static class ChartDrawing
{
    internal const double Top=20,Height=220,Left=95;
    internal static Brush Foreground=>Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush??new SolidColorBrush(Microsoft.UI.Colors.Gray);
    internal static Brush Blue=>new SolidColorBrush(Microsoft.UI.Colors.SteelBlue);
    internal static Brush Orange=>new SolidColorBrush(Microsoft.UI.Colors.DarkOrange);
    internal static Brush Green=>new SolidColorBrush(Microsoft.UI.Colors.SeaGreen);
    internal static Brush Purple=>new SolidColorBrush(Microsoft.UI.Colors.MediumPurple);
    internal static string Usd(double cents)=>(cents/100d).ToString("C0",CultureInfo.GetCultureInfo("en-US"));

    internal static (double Min,double Max) Range(IEnumerable<long?> values)
    {
        var known=values.Where(v=>v.HasValue).Select(v=>(double)v!.Value).Append(0).ToArray();double min=known.Min(),max=known.Max();
        return min==max?(0,100):(min,max);
    }
    internal static double Y(double value,(double Min,double Max) range)=>Top+(range.Max-value)/(range.Max-range.Min)*Height;
    internal static void Label(Canvas canvas,string text,double x,double y,double width=80)
    {
        var label=Form.Text(text);label.FontSize=12;label.Width=width;Canvas.SetLeft(label,x);Canvas.SetTop(label,y);canvas.Children.Add(label);
    }
    internal static void Line(Canvas canvas,double x1,double y1,double x2,double y2,Brush brush,double thickness=1)=>
        canvas.Children.Add(new Line{X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=brush,StrokeThickness=thickness});
    internal static void Rectangle(Canvas canvas,double x,double y,double width,double height,Brush brush)
    {
        var shape=new Rectangle{Width=width,Height=Math.Max(1,height),Fill=brush};Canvas.SetLeft(shape,x);Canvas.SetTop(shape,y);canvas.Children.Add(shape);
    }
    internal static void Axes(Canvas canvas,(double Min,double Max) range)
    {
        Line(canvas,Left,Y(0,range),canvas.Width-15,Y(0,range),Foreground);
        foreach(double value in new[]{range.Max,range.Min}.Distinct())Label(canvas,Usd(value),0,Y(value,range)-8,90);
        if(range.Min<0 && range.Max>0)Label(canvas,Usd(0),0,Y(0,range)-8,90);
    }
    internal static UIElement Scroll(Canvas canvas)=>new ScrollViewer{HorizontalScrollMode=ScrollMode.Enabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
        VerticalScrollMode=ScrollMode.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=canvas};
    internal static UIElement Legend(IEnumerable<ChartSeries> series)
    {
        var panel=Form.Panel();foreach(var item in series){var label=Form.Text(item.Name);label.Foreground=item.Brush;panel.Add(label);}return panel;
    }
}
