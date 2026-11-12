using SarahBeauty.Business.Storage;

namespace SarahBeauty.Business.Exports;

internal static class ReadableSqlTables
{
    internal static ReadableExportTable Read(DbWork db,ReadableExportSource source,ReadableExportBudget budget,string name,string[] columns,string sql)
    {
        var table=new ReadableTableBuilder(source,budget,name,columns);using var command=db.Command(sql,("$business",source.BusinessId));using var reader=command.ExecuteReader();if(reader.FieldCount!=columns.Length)throw new InvalidDataException("A readable SQL projection does not match its explicit column schema.");
        while(reader.Read()){object?[] values=new object?[columns.Length];for(int i=0;i<values.Length;i++)values[i]=reader.IsDBNull(i)?null:reader.GetValue(i);table.Add(values);}return table.Freeze();
    }
}
