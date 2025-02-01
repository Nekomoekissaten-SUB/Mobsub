using Mobsub.Native.FFMS2Binding.Native;

namespace Mobsub.Native.FFMS2Binding;

public unsafe class Ffms2Indexer : IDisposable
{
    private bool _disposed = false;
    public FFMS_Indexer* Indexer;
    public FFMS_ErrorInfo* ErrorInfo;
    
    public Ffms2Indexer(string path)
    {
        Indexer = Methods.FFMS_CreateIndexer(ConvertNative.StringToPtr(path), ErrorInfo);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}