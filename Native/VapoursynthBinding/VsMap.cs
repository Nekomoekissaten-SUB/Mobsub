using System.Text;
using Mobsub.Native.VapoursynthBinding.Native.API;

namespace Mobsub.Native.VapoursynthBinding;

public unsafe class VsMap : IDisposable
{
    public VsMap(Mobsub.Native.VapoursynthBinding.Vapoursynth vsapi, VSMap* mapPtr)
    {
        _api = vsapi;
        Map = mapPtr;
    }

    public VsMap(Mobsub.Native.VapoursynthBinding.Vapoursynth vsapi)
    {
        _api = vsapi;
        Map = vsapi.CreateMapPtr();
    }

    private readonly Mobsub.Native.VapoursynthBinding.Vapoursynth _api;
    internal VSMap* Map;
    private bool _disposed = false;

    public void Dispose()
    {
        if (!_disposed)
        {
            _api.FreeMapPtr(Map);
            _disposed = true;
        }
    }

    internal void Clear()
    {
        switch (_api.ApiVersion)
        {
            case 3:
                _api.Api3->clearMap(Map);
                break;
            case 4:
                _api.Api4->clearMap(Map);
                break;
        }
    }

    internal void SetError(string error)
    {
        switch (_api.ApiVersion)
        {
            case 3:
                _api.Api3->setError(Map, ConvertNative.StringToPtr(error));
                break;
            case 4:
                _api.Api4->mapSetError(Map, ConvertNative.StringToPtr(error));
                break;
        }
    }

    internal string? GetErrorString() => ConvertNative.StringFromPtr(_api.ApiVersion == 3 ? _api.Api3->getError(Map) : _api.Api4->mapGetError(Map));
    internal int KeysCount() => _api.ApiVersion == 3 ? _api.Api3->propNumKeys(Map) : _api.Api4->mapNumKeys(Map);
    internal sbyte* GetKey(int index) => _api.ApiVersion == 3 ? _api.Api3->propGetKey(Map, index) : _api.Api4->mapGetKey(Map, index);
    internal string? GetKeyString(int index) => ConvertNative.StringFromPtr(GetKey(index));
    /// <summary>
    /// Returns 0 if the key isn’t in the map. Otherwise it returns 1.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    internal int DeleteKey(sbyte* key) => _api.ApiVersion == 3 ? _api.Api3->propDeleteKey(Map, key) : _api.Api4->mapDeleteKey(Map, key);
    /// <summary>
    /// If there is no such key in the map, the returned value is ptUnset.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    internal VSPropertyType GetType(sbyte* key) => _api.ApiVersion == 3 ? (VSPropertyType)_api.Api3->propGetType(Map, key) : _api.Api4->mapGetType(Map, key);
    internal int ElementsCount(sbyte* key)=>_api.ApiVersion == 3 ? _api.Api3->propNumElements(Map, key) : _api.Api4->mapNumElements(Map, key);
    internal long GetInt(sbyte* key, int index)
    {
        int err;
        var i = _api.ApiVersion == 3 ? _api.Api3->propGetInt(Map, key, index, &err) : _api.Api4->mapGetInt(Map, key, index, &err);
        CheckError(err);
        return i;
    }
    internal long[] GetIntArray(sbyte* key)
    {
        int err;
        var arr = _api.ApiVersion == 3 ? _api.Api3->propGetIntArray(Map, key, &err) : _api.Api4->mapGetIntArray(Map, key, &err);
        return ConvertNative.CopyToManaged<long>((IntPtr)arr, ElementsCount(key));
    }
    internal double GetFloat(sbyte* key, int index)
    {
        int err;
        var i = _api.ApiVersion == 3 ? _api.Api3->propGetFloat(Map, key, index, &err) : _api.Api4->mapGetFloat(Map, key, index, &err);
        CheckError(err);
        return i;
    }
    internal float[] GetFloatArray(sbyte* key)
    {
        int err;
        var arr = _api.ApiVersion == 3 ? _api.Api3->propGetFloatArray(Map, key, &err) : _api.Api4->mapGetFloatArray(Map, key, &err);
        return ConvertNative.CopyToManaged<float>((IntPtr)arr, ElementsCount(key));
    }
    internal byte[] GetData(sbyte* key, int index)
    {
        int err;
        var b = _api.ApiVersion == 3 ? _api.Api3->propGetData(Map, key, index, &err) : _api.Api4->mapGetData(Map, key, index, &err);
        CheckError(err);
        return ConvertNative.CopyToManaged<byte>((IntPtr)b, GetDataSize(key, index));
    }
    internal int GetDataSize(sbyte* key, int index)
    {
        int err;
        var size = _api.ApiVersion == 3 ? _api.Api3->propGetDataSize(Map, key, index, &err) : _api.Api4->mapGetDataSize(Map, key, index, &err);
        CheckError(err);
        return size;
    }
    internal string GetString(sbyte* key, int index) => Encoding.UTF8.GetString(GetData(key, index));
    internal VSNode* GetNode(sbyte* key, int index)
    {
        int err;
        var node = _api.ApiVersion == 3 ? _api.Api3->propGetNode(Map, key, index, &err) : (VSNode*)_api.Api4->mapGetNode(Map, key, index, &err);
        CheckError(err);
        return node;
    }
    internal VSNode* GetNode(string key, int index) => GetNode(ConvertNative.StringToPtr(key), index);
    internal VSFrame* GetFrame(sbyte* key, int index)
    {
        int err;
        var frame = _api.ApiVersion == 3 ? _api.Api3->propGetFrame(Map, key, index, &err) : (VSFrame*)_api.Api4->mapGetFrame(Map, key, index, &err);
        CheckError(err);
        return frame;
    }
    internal VSFunction* GetFunc(sbyte* key, int index)
    {
        int err;
        var func = _api.ApiVersion == 3 ? _api.Api3->propGetFunc(Map, key, index, &err) : (VSFunction*)_api.Api4->mapGetFunction(Map, key, index, &err);
        CheckError(err);
        return func;
    }

    internal void SetInt(string key, long? value, VSMapAppendMode append = VSMapAppendMode.maAppend)
    {
        if (value is null) { return; }
        var status = _api.ApiVersion == 3 ? _api.Api3->propSetInt(Map, ConvertNative.StringToPtr(key), (long)value, (int)append)
            : _api.Api4->mapSetInt(Map, ConvertNative.StringToPtr(key), (long)value, (int)append);

        if (status != 0)
        {
            throw new Exception($"Please check, {key} should be long, but value is {value}");
        }
    }
    internal void SetIntArray(string key, long[]? value)
    {
        if (value is null) { return; }
        var size = value.Length;
        var status = _api.ApiVersion == 3 ? _api.Api3->propSetIntArray(Map, ConvertNative.StringToPtr(key), (long*)ConvertNative.CopyToNative(value), size)
            : _api.Api4->mapSetIntArray(Map, ConvertNative.StringToPtr(key), (long*)ConvertNative.CopyToNative(value), size);

        if (status != 0)
        {
            throw new Exception($"Please check, {key} should be long array, but value is [{string.Concat(',', value)}]");
        }
    }
    internal void SetBool(string key, bool? value, VSMapAppendMode append = VSMapAppendMode.maAppend) => SetInt(key, value is null ? null : value is true ? 1 : 0, append);
    internal void SetFloat(string key, double? value, VSMapAppendMode append = VSMapAppendMode.maAppend)
    {
        if (value is null) { return; }
        var status = _api.ApiVersion == 3 ? _api.Api3->propSetFloat(Map, ConvertNative.StringToPtr(key), (double)value, (int)append)
            : _api.Api4->mapSetFloat(Map, ConvertNative.StringToPtr(key), (double)value, (int)append);

        if (status != 0)
        {
            throw new Exception($"Please check, {key} should be double, but value is {value}");
        }
    }
    internal void SetFloatArray(string key, double[]? value)
    {
        if (value is null) { return; }
        var size = value.Length;
        var status = _api.ApiVersion == 3 ? _api.Api3->propSetFloatArray(Map, ConvertNative.StringToPtr(key), (double*)ConvertNative.CopyToNative(value), size)
            : _api.Api4->mapSetFloatArray(Map, ConvertNative.StringToPtr(key), (double*)ConvertNative.CopyToNative(value), size);

        if (status != 0)
        {
            throw new Exception($"Please check, {key} should be float array, but value is [{string.Concat(',', value)}]");
        }
    }
    internal void SetData(string key, string? value, VSDataTypeHint type = VSDataTypeHint.dtUtf8, VSMapAppendMode append = VSMapAppendMode.maAppend)
    {
        if (value is null) { return; }
        var status = _api.ApiVersion == 3 ? _api.Api3->propSetData(Map, ConvertNative.StringToPtr(key), ConvertNative.StringToPtr(value), -1, (int)append)
            : _api.Api4->mapSetData(Map, ConvertNative.StringToPtr(key), ConvertNative.StringToPtr(value), -1, (int)type, (int)append);

        if (status != 0)
        {
            throw new Exception($"Please check, {key} should be string, but value is {value}");
        }
    }
    internal void SetDataArray(string key, string[]? value, VSDataTypeHint type = VSDataTypeHint.dtUtf8, VSMapAppendMode append = VSMapAppendMode.maAppend)
    {
        if (value is null) { return; }
        var len = value.Length;
        for (var i = 0; i < len; i++)
        {
            try
            {
                SetData(key, value[i], type, append);
            }
            catch
            {
                throw new Exception($"Please check, {key} should be string array, but value {i} is {value[i]}");
            }
        }
    }
    internal void SetNode(string key, VSNode* value, VSMapAppendMode append = VSMapAppendMode.maAppend)
    {
        if (value == (VSNode*)IntPtr.Zero) { return; }
        var status = _api.ApiVersion == 3 ? _api.Api3->propSetNode(Map, ConvertNative.StringToPtr(key), value, (int)append)
            : _api.Api4->mapSetNode(Map, ConvertNative.StringToPtr(key), (VSNode*)value, (int)append);
        
        if (status != 0)
        {
            throw new Exception($"Please check, {key} should be node");
        }
    }
    internal void SetFrame(string key, VSFrame* value, VSMapAppendMode append = VSMapAppendMode.maAppend)
    {
        if (value == (VSFrame*)IntPtr.Zero) { return; }
        var status = _api.ApiVersion == 3 ? _api.Api3->propSetFrame(Map, ConvertNative.StringToPtr(key), value, (int)append)
            : _api.Api4->mapSetFrame(Map, ConvertNative.StringToPtr(key), (VSFrame*)value, (int)append);

        if (status != 0)
        {
            throw new Exception($"Please check, {key} should be frame");
        }
    }
    internal void SetFunc(string key, VSFunction* value, VSMapAppendMode append = VSMapAppendMode.maAppend)
    {
        if (value == (VSFrame*)IntPtr.Zero) { return; }
        var status = _api.ApiVersion == 3 ? _api.Api3->propSetFunc(Map, ConvertNative.StringToPtr(key), value, (int)append)
            : _api.Api4->mapSetFunction(Map, ConvertNative.StringToPtr(key), (VSFunction*)value, (int)append);

        if (status != 0)
        {
            throw new Exception($"Please check, {key} should be function");
        }
    }


    private void CheckError(int err)
    {
        if (err > 0)
        {
            throw new Exception(ConvertNative.StringFromPtr(_api.ApiVersion == 3 ? _api.Api3->getError(Map) : _api.Api4->mapGetError(Map)));
        }
    }
}
