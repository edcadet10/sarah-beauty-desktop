using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal static class GoogleSheetsReadSlices
{
    internal const int MaximumEstimatedBytes=1_000_000,EnvelopeBytes=4096;
    internal static IEnumerable<SheetsWriteRectangle> Build(ReadableExportTable table,CancellationToken cancellation)
    {
        var pending=new List<IReadOnlyList<string>>();int bytes=EnvelopeBytes,start=0,index=0;
        foreach(var row in new[]{table.Columns}.Concat(table.Rows))
        {
            cancellation.ThrowIfCancellationRequested();int cost=RowBytes(row);
            if(pending.Count>0&&bytes+cost>MaximumEstimatedBytes){yield return Freeze(table.Name,start,0,pending);pending=[];bytes=EnvelopeBytes;}
            if(cost+EnvelopeBytes>MaximumEstimatedBytes)
            {
                foreach(var rectangle in WideRow(table.Name,index,row))yield return rectangle;
            }
            else{if(pending.Count==0)start=index;pending.Add(row);bytes+=cost;}
            index++;
        }
        if(pending.Count>0)yield return Freeze(table.Name,start,0,pending);
    }

    internal static int Estimate(SheetsWriteRectangle rectangle)=>checked(EnvelopeBytes+rectangle.Values.Sum(RowBytes));
    private static int RowBytes(IReadOnlyList<string> row)=>checked(32+row.Sum(value=>checked(12*value.Length+84)));
    private static IEnumerable<SheetsWriteRectangle> WideRow(string name,int row,IReadOnlyList<string> values)
    {
        var cells=new List<string>();int bytes=EnvelopeBytes+32,start=0;
        for(int i=0;i<values.Count;i++)
        {
            int cost=checked(12*values[i].Length+84);
            if(cost+EnvelopeBytes+32>MaximumEstimatedBytes)throw new InvalidDataException("A literal cell exceeds the supported typed readback budget.");
            if(cells.Count>0&&bytes+cost>MaximumEstimatedBytes){yield return Freeze(name,row,start,[Array.AsReadOnly(cells.ToArray())]);cells=[];bytes=EnvelopeBytes+32;start=i;}
            cells.Add(values[i]);bytes+=cost;
        }
        if(cells.Count>0)yield return Freeze(name,row,start,[Array.AsReadOnly(cells.ToArray())]);
    }
    private static SheetsWriteRectangle Freeze(string name,int row,int column,List<IReadOnlyList<string>> values)=>new(name,row,column,Array.AsReadOnly(values.ToArray()));
}
