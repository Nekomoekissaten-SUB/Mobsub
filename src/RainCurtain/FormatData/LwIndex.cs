using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mobsub.RainCurtain.FormatData;

public class LwIndex
{
    public Version IndexVersion = new();
    public int IndexFileVersion;
    public string InputFilePath;
    public long FileSize;
    public string FileHash;
    private int FormatFlags; // FFmpeg AVInputFormat->flags
    private int RawCodecId; // FFmpeg AVInputFormat->raw_codec_id
    private string FormatName; // FFmpeg AVInputFormat->name
    internal int ActiveVideoStreamIndex;
    internal int ActiveAudioStreamIndex;
    public List<StreamInfo> StreamInfos = new(); 

    private const char TagStart = '<';
    private const char TagEnd = '>';
    private const char CloseTag = '/';
    private const char SetValue = '=';
    
    private bool _blockLibavReader = false;
    private bool _blockStreamInfo = false;
    private bool _blockIndexEntries = false;

    private StreamReader sr;
    
    public void ParseFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        sr = new StreamReader(fs);
        string? line;
        
        while ((line = sr.ReadLine()) != null)
        {
            Parse(line);
        }
        
        sr.Close();
    }

    // public void ParseIndexEntries(string filePath)
    // {
    //     using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
    //     using var sr = new StreamReader(fs);
    //     string? line;
    //     while ((line = sr.ReadLine()) != null)
    //     {
    //         var span = line.AsSpan();
    //
    //         if (span.StartsWith("<LibavReaderIndex="))
    //         {
    //             _blockLibavReader = true;
    //             continue;
    //         }
    //         
    //         if (span.StartsWith("<StreamInfo="))
    //         {
    //             _blockStreamInfo = true;
    //             continue;
    //         }
    //
    //         if (_blockLibavReader && _blockStreamInfo)
    //         {
    //             SetPropValue(span);
    //             continue;
    //         }
    //
    //         if (span.SequenceEqual("</StreamInfo>".AsSpan()))
    //         {
    //             _blockStreamInfo = false;
    //             continue;
    //         }
    //         if (span.SequenceEqual("</LibavReaderIndex>".AsSpan()))
    //         {
    //             _blockLibavReader = false;
    //             continue;
    //         }
    //     }
    // }

    public FrameIndexInfo[]? GetTimestamp(int streamIndex)
    {
        var frames = StreamInfos.Where(info => info.Index == streamIndex).ToArray();
        if (frames.Length == 0)
        {
            return null;
        }

        return frames.First().PacketInfos.OrderBy(pkt => pkt.Pts)
            .Select(pkt => new FrameIndexInfo
            {
                Pts = pkt.Pts,
                IsKeyFrame = pkt.Key == 1
            }).ToArray();
    }
    
    private void Parse(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty || span[0] != TagStart)
        {
            if (_blockLibavReader && !_blockStreamInfo)
            {
                ParsePacket(span);
            }
            else
            {
                SetPropValue(span);
            }
            return;
        }

        if (span[1] == CloseTag)
        {
            var endTag = span[2..^1];
            CloseTagBlock(endTag);
            return;
        }
        
        var closeTagStartIndex = span[1..].IndexOf(TagStart);
        Span<Range> dst = stackalloc Range[2];
        if (closeTagStartIndex == -1)
        {
            _ = SplitKeyValuePair(span[1..^1], dst, out var key, out var value);
            SetKeyValue(key, value);
        }
        else
        {
            var openTagEndIndex = span.IndexOf(TagEnd);
            var key = span[1..openTagEndIndex];
            var value = span[(openTagEndIndex + 1)..(closeTagStartIndex + 1)];
            SetKeyValue(key, value);
        }
        
    }

    private int SplitKeyValuePair(ReadOnlySpan<char> span, Span<Range> dst, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
    {
        var count = span.Split(dst, SetValue);
        key = span[dst[0]];
        value = span[dst[1]];
        return count;
    }

    private Span<Range> SplitMultiKeyValuePair(ReadOnlySpan<char> span, int count)
    {
        var dst = new Range[count];
        _ = span.Split(dst, ',');
        return dst;
    }

    private void SetKeyValue(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        if (key.SequenceEqual("LSMASHWorksIndexVersion".AsSpan()))
        {
            IndexVersion = Version.Parse(value);
        }
        else if (key.SequenceEqual("LibavReaderIndexFile".AsSpan()))
        {
            IndexFileVersion = int.Parse(value);
        }
        else if (key.SequenceEqual("InputFilePath".AsSpan()))
        {
            InputFilePath = value.ToString();
        }
        else if (key.SequenceEqual("FileSize".AsSpan()))
        {
            FileSize = long.Parse(value);
        }
        else if (key.SequenceEqual("FileHash".AsSpan()))
        {
            FileHash = value.ToString();
        }
        else if (key.SequenceEqual("LibavReaderIndex".AsSpan()))
        {
            _blockLibavReader = true;
            var dst = new Range[3];
            _ = value.Split(dst, ',');
            FormatFlags = int.Parse(value[dst[0]][2..]); // remove 0x
            RawCodecId = int.Parse(value[dst[1]]);
            FormatName = value[dst[2]].ToString();
        }
        else if (key.SequenceEqual("ActiveVideoStreamIndex".AsSpan()))
        {
            ActiveVideoStreamIndex = int.Parse(value);
        }
        else if (key.SequenceEqual("ActiveAudioStreamIndex".AsSpan()))
        {
            ActiveAudioStreamIndex = int.Parse(value);
        }
        else if (key.SequenceEqual("StreamInfo".AsSpan()))
        {
            var dst = new Range[2];
            _ = value.Split(dst, ',');
            
            StreamInfos.Add(new StreamInfo()
            {
                Index = int.Parse(value[dst[0]]),
                MediaType = (AvMediaType)int.Parse(value[dst[1]]),
            });
            
            _blockStreamInfo = true;
        }
        else if (_blockLibavReader && _blockStreamInfo)
        {
            if (key.SequenceEqual("Codec".AsSpan()))
            {
                StreamInfos.Last().CodecId = int.Parse(value);
            }
            else if (key.SequenceEqual("TimeBase".AsSpan()))
            {
                var sepIndex = value.IndexOf('/');
                StreamInfos.Last().TimeBase = new AvRational()
                {
                    Numerator = int.Parse(value[..sepIndex]),
                    Denominator = int.Parse(value[(sepIndex + 1)..]),
                };
            }
            else if (key.SequenceEqual("Width".AsSpan()))
            {
                StreamInfos.Last().Width = int.Parse(value);
            }
            else if (key.SequenceEqual("Height".AsSpan()))
            {
                StreamInfos.Last().Height = int.Parse(value);
            }
            else if (key.SequenceEqual("Format".AsSpan()))
            {
                StreamInfos.Last().FormatName = value.ToString();
            }
            else if (key.SequenceEqual("ColorSpace".AsSpan()))
            {
                StreamInfos.Last().ColorSpace = int.Parse(value);
            }
        }
    }
    
    private void SetPropValue(ReadOnlySpan<char> span)
    {
        if (!_blockLibavReader)
            return;

        var ranges = SplitMultiKeyValuePair(span, _blockStreamInfo ? 6 : 5);
        var dst = new Range[2];
        
        foreach (var range in ranges)
        {
            SplitKeyValuePair(span[range], dst, out var key, out var value);
            SetKeyValue(key, value);
        }
    }
    
    private void CloseTagBlock(ReadOnlySpan<char> span)
    {
        if (span.SequenceEqual("StreamInfo".AsSpan()))
        {
            _blockStreamInfo = false;
        }
        else if (span.SequenceEqual("LibavReaderIndex".AsSpan()))
        {
            _blockLibavReader = false;
        }
    }

    private void ParsePacket(ReadOnlySpan<char> span)
    {
        var packetInfo = new PacketInfo();
        
        var ranges = SplitMultiKeyValuePair(span, 5);
        var dst = new Range[2];

        foreach (var range in ranges)
        {
            SplitKeyValuePair(span[range], dst, out var key, out var value);
            if (key.SequenceEqual("Index".AsSpan()))
            {
                packetInfo.StreamIndex = int.Parse(value);
            }
            else if (key.SequenceEqual("POS".AsSpan()))
            {
                packetInfo.Pos = long.Parse(value);
            }
            else if (key.SequenceEqual("PTS".AsSpan()))
            {
                packetInfo.Pts = long.Parse(value);
            }
            else if (key.SequenceEqual("DTS".AsSpan()))
            {
                packetInfo.Dts = long.Parse(value);
            }
            else if (key.SequenceEqual("EDI".AsSpan()))
            {
                packetInfo.ExtraDataIndex = int.Parse(value);
            }
        }

        var line = sr.ReadLine().AsSpan();
        ranges = SplitMultiKeyValuePair(line, 5);
        foreach (var range in ranges)
        {
            SplitKeyValuePair(line[range], dst, out var key, out var value);

            if (key.SequenceEqual("Key".AsSpan()))
            {
                packetInfo.Key = int.Parse(value);
            }
            else if (key.SequenceEqual("Pic".AsSpan()))
            {
                packetInfo.PictureType = int.Parse(value);
            }
            else if (key.SequenceEqual("POC".AsSpan()))
            {
                packetInfo.Poc = int.Parse(value);
            }
            else if (key.SequenceEqual("Repeat".AsSpan()))
            {
                packetInfo.RepeatPicture = int.Parse(value);
            }
            else if (key.SequenceEqual("Field".AsSpan()))
            {
                packetInfo.FieldInfo = int.Parse(value);
            }
        }
        
        StreamInfos.Last().PacketInfos.Add(packetInfo);
    }
    
    public struct PacketInfo
    {
        public int StreamIndex;
        public long Pos;
        public long Pts;
        public long Dts;
        public int ExtraDataIndex;
        public int Key;
        public int PictureType;
        public int Poc;
        public int RepeatPicture;
        public int FieldInfo;
    }
    
    public class StreamInfo
    {
        public int Index;
        public AvMediaType MediaType;
        // FFmpeg AVCodecID
        public int CodecId;
        internal AvRational TimeBase;
        public int Width;
        public int Height;
        public string FormatName;
        public int ColorSpace;
        public List<PacketInfo> PacketInfos = [];
    }
}