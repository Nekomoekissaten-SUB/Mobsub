using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Mobsub.Native.FFMS2Binding;
using Mobsub.RainCurtain.Models.Video;
using Mobsub.RainCurtain.ViewModels;

namespace Mobsub.RainCurtain.Views;

public partial class MainWindow : Window
{
    private VsVideoInfo _sourceVideoInfo;
    private VsVideoInfo _currentVideoInfo;
    private VsVideo? _currentVideo;
    private int _currentFrameIndex;
    private bool _isPlaying;
    private CancellationTokenSource? _cancellationTokenSource; // 用于控制播放任务
    private Task? _playbackTask; // 播放任务
    private DateTime _lastFpsUpdateTime = DateTime.Now; // 用于更新 FPS
    private int _frameCount = 0; // 当前播放帧数

    private Ffms2VideoSource _ffms2Source;
    
    public MainWindow()
    {
        InitializeComponent();
        // AddHandler(DragDrop.DropEvent, OnFileDrop);
        // AddHandler(KeyDownEvent, OnKeyDown2);
    }
    
    // private async Task OnFileDrop(object? sender, DragEventArgs e)
    // {
    //     var file = e.Data.GetFiles()!.First().Path.LocalPath;
    //     
    //     // (_currentVideo, _sourceVideoInfo) = await OpenVideoAsync(file);
    //     // _currentFrameIndex = 0;
    //     //
    //     // _currentVideoInfo = _currentVideo.Info;
    //     
    //     var ffms2 = new FFmpegSource2();
    //     _ffms2Source = ffms2.ReadVideo(file);
    //     // var frame = video.GetFrame(_currentFrameIndex);
    //     // var image = Ffms2Video.ConvertToSkImage(frame);
    //     //
    //     // CanvasControl.InvalidateVisual();
    //     // CanvasControl.Draw += (_, evt) =>
    //     // {
    //     //     evt.Canvas.DrawImage(image, new SKPoint(0, 0));
    //     // };
    //     
    //     await DrawFrame();
    // }
    //
    // private async Task<(VsVideo, VsVideoInfo)> OpenVideoAsync(string filename)
    // {
    //     var vs = new VsInit(4);
    //     var lwlibav = new VsLwLibav(vs);
    //
    //     var vNode = await Task.Run(() => lwlibav.OpenVideo(filename));
    //     var vid = new VsVideo(vNode, 25);
    //
    //     return (vid, vNode.GetVideoInfo());
    // }
    //
    // private async Task DrawFrame()
    // {
    //     // if (_currentVideo == null)
    //     //     return;
    //     
    //     // var frame = await _currentVideo.GetNextFrameAsync();
    //     // var image = _currentVideo.ConvertToSkImage(frame);
    //     //
    //     // CanvasControl.InvalidateVisual();
    //     // CanvasControl.Draw += (_, evt) =>
    //     // {
    //     //     evt.Canvas.DrawImage(image, new SKPoint(0, 0));
    //     // };
    //     //
    //     // UpdateFpsDisplay();
    //     
    //     for (; _currentFrameIndex < _ffms2Source.Props.NumFrames; _currentFrameIndex++)
    //     {
    //         var frame = await Task.Run(() => Ffms2Video.ConvertToSkImage(_ffms2Source.GetFrame(_currentFrameIndex)));
    //     
    //         // CanvasControl.InvalidateVisual();
    //         // CanvasControl.Draw += (_, evt) =>
    //         // {
    //         //     evt.Canvas.DrawImage(frame, new SKPoint(0, 0));
    //         // };
    //
    //         
    //         var bitmap = new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Opaque, frame, new PixelSize(1920, 1080),
    //             new Vector(96, 96), 1920 * 4);
    //         Image.Source = bitmap;
    //         // Image.InvalidateVisual();
    //         UpdateFpsDisplay();
    //     }
    // }
    //
    // private async Task OnKeyDown2(object? sender, KeyEventArgs e)
    // {
    //     try
    //     {
    //         System.Diagnostics.Debug.WriteLine(e.Key);
    //         if (_currentVideo is null)
    //         {
    //             return;
    //         }
    //     
    //         switch (e.Key)
    //         {
    //             case Key.Left:
    //                 if (_currentFrameIndex > 0)
    //                 {
    //                     _currentFrameIndex--;
    //                     await DrawFrame();
    //                 }
    //                 break;
    //             case Key.Right:
    //                 if (_currentFrameIndex < _currentVideoInfo.numFrames - 1)
    //                 {
    //                     _currentFrameIndex++;
    //                     await DrawFrame();
    //                 }
    //                 break;
    //             default:
    //                 break;
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         throw;
    //     }
    // }
    //
    // private int ProcessStderr(string message)
    // {
    //     var match = Regex.Match(message, @"Creating lwi index file\s*(\d+)%");
    //     if (match.Success && int.TryParse(match.Groups[1].Value, out int progress))
    //     {
    //         return progress;
    //     }
    //
    //     return -1;
    // }
    //
    // private void UpdateFpsDisplay()
    // {
    //     // 每秒更新一次帧率
    //     var elapsedTime = DateTime.Now - _lastFpsUpdateTime;
    //     if (elapsedTime.TotalSeconds >= 1)
    //     {
    //         double fps = _frameCount / elapsedTime.TotalSeconds;
    //         _frameCount = 0; // 重置帧计数
    //         _lastFpsUpdateTime = DateTime.Now;
    //
    //         // 更新 FPS 显示（假设有一个 `FpsLabel` 控件来显示 FPS）
    //         FpsLabel.Text = $"FPS: {fps:F2}";
    //     }
    //     else
    //     {
    //         _frameCount++;
    //     }
    // }
}






