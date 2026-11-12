namespace SarahBeauty.Business.Exports;

internal sealed class ReadableJsonLines(Stream source)
{
    private readonly byte[] _buffer=new byte[8192];
    private int _offset,_count;

    internal byte[]? Read(int maximumBytes,CancellationToken cancellation)
    {
        using var line=new MemoryStream();
        while(true)
        {
            cancellation.ThrowIfCancellationRequested();
            if(_offset==_count){_count=source.Read(_buffer);_offset=0;if(_count==0)return line.Length==0?null:line.ToArray();}
            int newline=_buffer.AsSpan(_offset,_count-_offset).IndexOf((byte)'\n');
            int length=newline<0?_count-_offset:newline;
            if(line.Length+length>maximumBytes)throw new InvalidDataException("A staged readable row exceeds its supported serialized size.");
            line.Write(_buffer,_offset,length);_offset+=length;
            if(newline>=0){_offset++;return line.ToArray();}
        }
    }
}
