using System.Runtime.InteropServices;
using System.Text;
using Mobsub.Native.VapoursynthBinding.Native.API;

namespace Mobsub.Native.VapoursynthBinding;

public unsafe class VsCore : IDisposable
{
    public VsCore(Mobsub.Native.VapoursynthBinding.Vapoursynth vsapi, int threads = 0, VSCoreCreationFlags flags = 0)
    {
        Api = vsapi;
        _core = Api.CreateCorePtr(threads, flags);
    }

    public VsCore(Mobsub.Native.VapoursynthBinding.Vapoursynth vsapi, VSCore* corePtr)
    {
        Api = vsapi;
        _core = corePtr;
    }
    
    public readonly Mobsub.Native.VapoursynthBinding.Vapoursynth Api;
    private VSCore* _core;
    private bool disposed = false;

    public void Dispose()
    {
        if (!disposed)
        {
            Api.FreeCorePtr(_core);
            disposed = true;
        }
    }
    
    public VSCoreInfo GetCoreInfo()
    {
        var infoPtr = Marshal.AllocHGlobal(sizeof(VSCoreInfo));
        switch (Api.ApiVersion)
        {
            case 3:
                Api.Api3->getCoreInfo2(_core, (VSCoreInfo*)infoPtr);
                break;
            case 4:
                Api.Api4->getCoreInfo(_core, (VSCoreInfo*)infoPtr);
                break;
            default:
                throw new Exception();
        }
        var info = (VSCoreInfo)Marshal.PtrToStructure(infoPtr, typeof(VSCoreInfo))!;
        Marshal.FreeHGlobal(infoPtr);
        return info;
    }
    public static ushort GetApiMajorVersion(int apiVersion) => (ushort)(apiVersion >> 16);
    public static ushort GetApiMinorVersion(int apiVersion) => (ushort)(apiVersion & 0xFFFF);
    public long SetMaxCacheSize(long size) => Api.ApiVersion == 3 ? Api.Api3->setMaxCacheSize(size, _core) : Api.Api4->setMaxCacheSize(size, _core);


    public VSPlugin* GetPluginByNamespace(sbyte* ns)
    {
        return Api.ApiVersion switch
        {
            3 => (VSPlugin*)Api.Api3->getPluginByNs(ns, _core),
            4 => Api.Api4->getPluginByNamespace(ns, _core),
            _ => throw new Exception()
        };
    }
    public VSPlugin* GetPluginByNamespace(string ns) => GetPluginByNamespace(ConvertNative.StringToPtr(ns));
    public PluginInfo[] GetPluginInfos()
    {
        if (Api.ApiVersion == 3)
        {
            var plugins = new VsMap(Api, Api.Api3->getPlugins(_core));
            var pluginCount = plugins.KeysCount();
            var arr = new PluginInfo[pluginCount];
            for ( var i = 0; i < pluginCount; i++ )
            {
                var key = plugins.GetKey(i);
                var value = plugins.GetData(key!, 0);
                var span = value.AsSpan();
                var pluginInfo = new PluginInfo();

                var start = 0;
                var p = 0;
                for (int j = 0; j < span.Length; j++)
                {
                    if (span[j] == ';' || j == span.Length - 1)
                    {
                        switch (p)
                        {
                            case 0:
                                pluginInfo.Namespace = Encoding.UTF8.GetString(span[start..j]);
                                break;
                            case 1:
                                pluginInfo.Identifier = Encoding.UTF8.GetString(span[start..j]);
                                break;
                            case 2:
                                pluginInfo.FullName = Encoding.UTF8.GetString(span[start..]);
                                break;
                        }
                        start = j + 1;
                        p++;
                    }
                }
                var _plugin = GetPluginByNamespace(pluginInfo.Namespace!);
                pluginInfo.Path = ConvertNative.StringFromPtr(Api.Api3->getPluginPath(_plugin))!;
                pluginInfo.Version = null;

                var functions = new VsMap(Api, Api.Api3->getFunctions(_plugin));
                var funcCount = functions.KeysCount();
                var fInfos = new FunctionInfo[funcCount];
                for (var j = 0; j < funcCount; j++)
                {
                    var fInfo = new FunctionInfo();
                    var v = functions.GetData(functions.GetKey(j), 0);
                    var _span = v.AsSpan();

                    for (var k = 0; k < _span.Length; k++)
                    {
                        if (_span[k] == ';')
                        {
                            fInfo.Name = Encoding.UTF8.GetString(_span[..k]);
                            fInfo.Arguments = Encoding.UTF8.GetString(_span[(k+1)..]);
                            break;
                        }
                    }
                    fInfos[j] = fInfo;
                }
                pluginInfo.Functions = fInfos;

                functions.Dispose();
                arr[i] = pluginInfo;
            }

            plugins.Dispose();
            return arr;
        }
        
        if (Api.ApiVersion == 4)
        {
            var nullPtr = IntPtr.Zero;
            var plugin = Api.Api4->getNextPlugin((VSPlugin*)nullPtr, _core);
            List<PluginInfo> pInfos = [];

            while (plugin != (VSPlugin*)nullPtr)
            {
                var pInfo = new PluginInfo();
                pInfo.Namespace =ConvertNative.StringFromPtr(Api.Api4->getPluginNamespace(plugin))!;
                pInfo.Identifier =ConvertNative.StringFromPtr(Api.Api4->getPluginID(plugin))!;
                pInfo.FullName =ConvertNative.StringFromPtr(Api.Api4->getPluginName(plugin))!;
                pInfo.Path =ConvertNative.StringFromPtr(Api.Api4->getPluginPath(plugin))!;
                pInfo.Version = Api.Api4->getPluginVersion(plugin);

                List<FunctionInfo> fInfos = [];
                var function = Api.Api4->getNextPluginFunction((VSPluginFunction*)nullPtr, plugin);
                while (function != (VSPluginFunction*)nullPtr)
                {
                    var fInfo = new FunctionInfo();
                    fInfo.Name =ConvertNative.StringFromPtr(Api.Api4->getPluginFunctionName(function))!;
                    fInfo.Arguments =ConvertNative.StringFromPtr(Api.Api4->getPluginFunctionArguments(function))!;
                    fInfo.ReturnType =ConvertNative.StringFromPtr(Api.Api4->getPluginFunctionReturnType(function))!;
                    
                    fInfos.Add(fInfo);
                    function = Api.Api4->getNextPluginFunction(function, plugin);
                }

                pInfo.Functions = fInfos.ToArray();
                pInfos.Add(pInfo);
                plugin = Api.Api4->getNextPlugin(plugin, _core);
            }

            return pInfos.ToArray();
        }
        else
        {
            throw new Exception();
        }
    }
}

public struct PluginInfo
{
    public string Namespace;
    public string Identifier;
    public string FullName;
    public string Path;
    public int? Version;
    public FunctionInfo[]? Functions;
}

public struct FunctionInfo
{
    public string Name;
    public string Arguments;
    public string? ReturnType;
}