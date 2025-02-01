using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Mobsub.Native.VapoursynthBinding;
using SkiaSharp;

namespace Mobsub.RainCurtain.Models.Video;

public class VsVideo
{
    private readonly VideoNode _vnode;
    internal readonly VsVideoInfo Info;
    private int rowBytes;
    private IntPtr skImageData;
    private int length;

    private readonly SortedList<int, VsFrame> _frameBuffer = new();
    private readonly Dictionary<int, Task<VsFrame>> _decodeTasks = new();
    private readonly int _cacheSize;
    private int _currentFrame;
    
    
    public VsVideo(VideoNode vnode, int cacheSize)
    {
        _vnode = vnode;
        Info = vnode.GetVideoInfo();
        rowBytes = Info.width * 4;
        length = rowBytes * Info.height;
        skImageData = Marshal.AllocHGlobal(length);
        _cacheSize = cacheSize;
    }
    
    private VsFrame GetFrame(int frameNumber)
    {
        if (_vnode.TryGetFrame(frameNumber, out var frame))
        {
            return frame!;
        }
        throw new ArgumentException();
    }


    public async Task<VsFrame> GetNextFrameAsync()
    {
        await EnsureCacheAsync();
        lock (_frameBuffer)
        {
            if (_frameBuffer.Remove(_currentFrame, out var frame))
            {
                _currentFrame++;
                return frame;
            }
        }
        throw new InvalidOperationException("No frame available.");
    }

    private async Task EnsureCacheAsync()
    {
        var tasks = new List<Task>();
        lock (_frameBuffer)
        {
            while (_frameBuffer.Count + _decodeTasks.Count < _cacheSize)
            {
                var nextFrame = _currentFrame + _frameBuffer.Count + _decodeTasks.Count;
                if (_decodeTasks.ContainsKey(nextFrame)) continue;
                var decodeTask = _vnode.GetFrameAsync(nextFrame);
                _decodeTasks[nextFrame] = decodeTask;

                decodeTask.ContinueWith(task =>
                {
                    lock (_frameBuffer)
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            _frameBuffer[nextFrame] = task.Result;
                        }
                        _decodeTasks.Remove(nextFrame);
                    }
                });
                tasks.Add(decodeTask);
            }
        }
        
        await Task.WhenAll(tasks);
    }

    
    
    private SKImageInfo GetImageInfo() =>
        new SKImageInfo(Info.width, Info.height, SKColorType.Bgra8888, SKAlphaType.Opaque);

    internal unsafe SKImage ConvertToSkImage(VsFrame frame)
    {
        // if (skImageData != IntPtr.Zero)
        // {
        //     Marshal.Copy(zeroBytes, 0, skImageData, length);
        // }
        
        
        var skImagePtr = (byte*)skImageData.ToPointer();
        
        var stride = frame.GetStride(0);
        var ptrR = frame.GetReadPointer(0);
        var ptrG = frame.GetReadPointer(1);
        var ptrB = frame.GetReadPointer(2);
        
        for (var j = 0; j < Info.height; j++)
        {
            var srcR = ptrR + j * stride;
            var srcG = ptrG + j * stride;
            var srcB = ptrB + j * stride;
            var dst = skImagePtr + j * rowBytes;
        
            for (var i = 0; i < Info.width; i++)
            {
                dst[0] = srcB[i];
                dst[1] = srcG[i];
                dst[2] = srcR[i];
                dst[3] = 255;
                dst += 4;
            }
        }
        
        frame.Dispose();
        return SKImage.FromPixels(GetImageInfo(), skImageData, rowBytes);
    }

    public SKImage GetSkImage(int frameNumber) => ConvertToSkImage(GetFrame(frameNumber));

    // public async Task<SKImage[]> GetSkImages()
    // {
    //     var tasks = new List<Task<VsFrame>>();
    //
    //     for (var j = 0; j < 1000; j++)
    //     {
    //         // await semaphore.WaitAsync();
    //         tasks.Add(_vnode.Core.Api.GetFrameAsyncPtr(_vnode, j));
    //     }
    //     
    //     var results = await Task.WhenAll(tasks);
    //     var images = new SKImage[results.Length];
    //     for (var i = 0; i < results.Length; i++)
    //     {
    //         images[i] = ConvertToSkImage(results[i]);
    //     }
    //     
    //     return images;
    // }
}