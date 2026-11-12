using SarahBeauty.Business.Recovery;

namespace SarahBeauty.Business.Exports;

internal sealed class ReadableGenerationWorkspace:IDisposable
{
    private readonly ReadableGenerationPaths _paths;
    private readonly HashSet<int> _created=[];
    private bool _published;
    internal string Root {get;}
    internal ReadableGenerationWorkspace(ReadableGenerationPaths paths){_paths=paths;Root=paths.CreateWorkspace();}
    internal FileStream Create(int index)
    {
        string path=_paths.FilePath(Root,index);var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);_created.Add(index);return stream;
    }
    internal void Publish(string generationId)
    {
        _=_paths.FilePath(Root,-1);string destination=_paths.Generation(generationId);Directory.Move(Root,destination);_published=true;
    }
    public void Dispose()
    {
        if(_published)return;
        foreach(int index in _created)try{File.Delete(_paths.FilePath(Root,index));}catch(IOException){}catch(UnauthorizedAccessException){}catch(InvalidDataException){}
        try{ArchivePaths.RejectLink(Root);Directory.Delete(Root,recursive:false);}catch(IOException){}catch(UnauthorizedAccessException){}catch(InvalidDataException){}
    }
}
