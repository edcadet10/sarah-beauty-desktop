using System.Text.Json;
using SarahBeauty.Business.Exports;

namespace SarahBeauty.Business.Google;

internal static class GoogleSheetsTableSlices
{
    internal static IEnumerable<SheetsWriteRectangle> Build(ReadableExportTable table,int maximumValueBytes,CancellationToken cancellation)
    {
        var pending=new List<IReadOnlyList<string>>();int bytes=2,start=0,rowIndex=0;
        foreach(var row in new[]{table.Columns}.Concat(table.Rows))
        {
            cancellation.ThrowIfCancellationRequested();int length=JsonSerializer.SerializeToUtf8Bytes(row).Length;
            if(pending.Count>0&&bytes+length+1>maximumValueBytes){yield return Freeze(table.Name,start,0,pending);pending=[];bytes=2;}
            if(length+2>maximumValueBytes)
            {
                foreach(var slice in WideRow(table.Name,rowIndex,row,maximumValueBytes))yield return slice;
            }
            else
            {
                if(pending.Count==0)start=rowIndex;bytes+=length+(pending.Count==0?0:1);pending.Add(row);
            }
            rowIndex++;
        }
        if(pending.Count>0)yield return Freeze(table.Name,start,0,pending);
    }

    private static IEnumerable<SheetsWriteRectangle> WideRow(string name,int row,IReadOnlyList<string> values,int maximumValueBytes)
    {
        var cells=new List<string>();int bytes=4,start=0;
        for(int i=0;i<values.Count;i++)
        {
            int length=JsonSerializer.SerializeToUtf8Bytes(values[i]).Length;
            if(length+4>maximumValueBytes)throw new InvalidDataException("A literal Sheets cell exceeds the supported serialized request budget.");
            if(cells.Count>0&&bytes+length+1>maximumValueBytes){yield return Freeze(name,row,start,[Array.AsReadOnly(cells.ToArray())]);cells=[];bytes=4;start=i;}
            bytes+=length+(cells.Count==0?0:1);cells.Add(values[i]);
        }
        if(cells.Count>0)yield return Freeze(name,row,start,[Array.AsReadOnly(cells.ToArray())]);
    }

    private static SheetsWriteRectangle Freeze(string name,int row,int column,List<IReadOnlyList<string>> values)=>new(name,row,column,Array.AsReadOnly(values.ToArray()));
}
