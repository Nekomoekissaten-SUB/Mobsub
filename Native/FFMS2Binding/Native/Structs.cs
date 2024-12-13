namespace Mobsub.Native.FFMS2Binding.Native;

// ReSharper disable InconsistentNaming
public unsafe partial struct FFMS_ErrorInfo
{
    public int ErrorType;

    public int SubType;

    public int BufferSize;

    [NativeTypeName("char *")]
    public sbyte* Buffer;
}

public partial struct FFMS_VideoSource
{
}

public partial struct FFMS_AudioSource
{
}

public partial struct FFMS_Indexer
{
}

public partial struct FFMS_Index
{
}

public partial struct FFMS_Track
{
}

public enum FFMS_Errors
{
    FFMS_ERROR_SUCCESS = 0,
    FFMS_ERROR_INDEX = 1,
    FFMS_ERROR_INDEXING,
    FFMS_ERROR_POSTPROCESSING,
    FFMS_ERROR_SCALING,
    FFMS_ERROR_DECODING,
    FFMS_ERROR_SEEKING,
    FFMS_ERROR_PARSER,
    FFMS_ERROR_TRACK,
    FFMS_ERROR_WAVE_WRITER,
    FFMS_ERROR_CANCELLED,
    FFMS_ERROR_RESAMPLING,
    FFMS_ERROR_UNKNOWN = 20,
    FFMS_ERROR_UNSUPPORTED,
    FFMS_ERROR_FILE_READ,
    FFMS_ERROR_FILE_WRITE,
    FFMS_ERROR_NO_FILE,
    FFMS_ERROR_VERSION,
    FFMS_ERROR_ALLOCATION_FAILED,
    FFMS_ERROR_INVALID_ARGUMENT,
    FFMS_ERROR_CODEC,
    FFMS_ERROR_NOT_AVAILABLE,
    FFMS_ERROR_FILE_MISMATCH,
    FFMS_ERROR_USER,
}

public enum FFMS_SeekMode
{
    FFMS_SEEK_LINEAR_NO_RW = -1,
    FFMS_SEEK_LINEAR = 0,
    FFMS_SEEK_NORMAL = 1,
    FFMS_SEEK_UNSAFE = 2,
    FFMS_SEEK_AGGRESSIVE = 3,
}

public enum FFMS_IndexErrorHandling
{
    FFMS_IEH_ABORT = 0,
    FFMS_IEH_CLEAR_TRACK = 1,
    FFMS_IEH_STOP_TRACK = 2,
    FFMS_IEH_IGNORE = 3,
}

public enum FFMS_TrackType
{
    FFMS_TYPE_UNKNOWN = -1,
    FFMS_TYPE_VIDEO,
    FFMS_TYPE_AUDIO,
    FFMS_TYPE_DATA,
    FFMS_TYPE_SUBTITLE,
    FFMS_TYPE_ATTACHMENT,
}

public enum FFMS_SampleFormat
{
    FFMS_FMT_U8 = 0,
    FFMS_FMT_S16,
    FFMS_FMT_S32,
    FFMS_FMT_FLT,
    FFMS_FMT_DBL,
}

public enum FFMS_AudioChannel
{
    FFMS_CH_FRONT_LEFT = 0x00000001,
    FFMS_CH_FRONT_RIGHT = 0x00000002,
    FFMS_CH_FRONT_CENTER = 0x00000004,
    FFMS_CH_LOW_FREQUENCY = 0x00000008,
    FFMS_CH_BACK_LEFT = 0x00000010,
    FFMS_CH_BACK_RIGHT = 0x00000020,
    FFMS_CH_FRONT_LEFT_OF_CENTER = 0x00000040,
    FFMS_CH_FRONT_RIGHT_OF_CENTER = 0x00000080,
    FFMS_CH_BACK_CENTER = 0x00000100,
    FFMS_CH_SIDE_LEFT = 0x00000200,
    FFMS_CH_SIDE_RIGHT = 0x00000400,
    FFMS_CH_TOP_CENTER = 0x00000800,
    FFMS_CH_TOP_FRONT_LEFT = 0x00001000,
    FFMS_CH_TOP_FRONT_CENTER = 0x00002000,
    FFMS_CH_TOP_FRONT_RIGHT = 0x00004000,
    FFMS_CH_TOP_BACK_LEFT = 0x00008000,
    FFMS_CH_TOP_BACK_CENTER = 0x00010000,
    FFMS_CH_TOP_BACK_RIGHT = 0x00020000,
    FFMS_CH_STEREO_LEFT = 0x20000000,
    FFMS_CH_STEREO_RIGHT = 0x40000000,
}

public enum FFMS_Resizers
{
    FFMS_RESIZER_FAST_BILINEAR = 0x0001,
    FFMS_RESIZER_BILINEAR = 0x0002,
    FFMS_RESIZER_BICUBIC = 0x0004,
    FFMS_RESIZER_X = 0x0008,
    FFMS_RESIZER_POINT = 0x0010,
    FFMS_RESIZER_AREA = 0x0020,
    FFMS_RESIZER_BICUBLIN = 0x0040,
    FFMS_RESIZER_GAUSS = 0x0080,
    FFMS_RESIZER_SINC = 0x0100,
    FFMS_RESIZER_LANCZOS = 0x0200,
    FFMS_RESIZER_SPLINE = 0x0400,
}

public enum FFMS_AudioDelayModes
{
    FFMS_DELAY_NO_SHIFT = -3,
    FFMS_DELAY_TIME_ZERO = -2,
    FFMS_DELAY_FIRST_VIDEO_TRACK = -1,
}

public enum FFMS_AudioGapFillModes
{
    FFMS_GAP_FILL_AUTO = -1,
    FFMS_GAP_FILL_DISABLED = 0,
    FFMS_GAP_FILL_ENABLED = -1,
}

public enum FFMS_ChromaLocations
{
    FFMS_LOC_UNSPECIFIED = 0,
    FFMS_LOC_LEFT = 1,
    FFMS_LOC_CENTER = 2,
    FFMS_LOC_TOPLEFT = 3,
    FFMS_LOC_TOP = 4,
    FFMS_LOC_BOTTOMLEFT = 5,
    FFMS_LOC_BOTTOM = 6,
}

public enum FFMS_ColorRanges
{
    FFMS_CR_UNSPECIFIED = 0,
    FFMS_CR_MPEG = 1,
    FFMS_CR_JPEG = 2,
}

public enum FFMS_Stereo3DType
{
    FFMS_S3D_TYPE_2D = 0,
    FFMS_S3D_TYPE_SIDEBYSIDE,
    FFMS_S3D_TYPE_TOPBOTTOM,
    FFMS_S3D_TYPE_FRAMESEQUENCE,
    FFMS_S3D_TYPE_CHECKERBOARD,
    FFMS_S3D_TYPE_SIDEBYSIDE_QUINCUNX,
    FFMS_S3D_TYPE_LINES,
    FFMS_S3D_TYPE_COLUMNS,
}

public enum FFMS_Stereo3DFlags
{
    FFMS_S3D_FLAGS_INVERT = 1,
}

public enum FFMS_MixingCoefficientType
{
    FFMS_MIXING_COEFFICIENT_Q8 = 0,
    FFMS_MIXING_COEFFICIENT_Q15 = 1,
    FFMS_MIXING_COEFFICIENT_FLT = 2,
}

public enum FFMS_MatrixEncoding
{
    FFMS_MATRIX_ENCODING_NONE = 0,
    FFMS_MATRIX_ENCODING_DOBLY = 1,
    FFMS_MATRIX_ENCODING_PRO_LOGIC_II = 2,
    FFMS_MATRIX_ENCODING_PRO_LOGIC_IIX = 3,
    FFMS_MATRIX_ENCODING_PRO_LOGIC_IIZ = 4,
    FFMS_MATRIX_ENCODING_DOLBY_EX = 5,
    FFMS_MATRIX_ENCODING_DOLBY_HEADPHONE = 6,
}

public enum FFMS_ResampleFilterType
{
    FFMS_RESAMPLE_FILTER_CUBIC = 0,
    FFMS_RESAMPLE_FILTER_SINC = 1,
    FFMS_RESAMPLE_FILTER_KAISER = 2,
}

public enum FFMS_AudioDitherMethod
{
    FFMS_RESAMPLE_DITHER_NONE = 0,
    FFMS_RESAMPLE_DITHER_RECTANGULAR = 1,
    FFMS_RESAMPLE_DITHER_TRIANGULAR = 2,
    FFMS_RESAMPLE_DITHER_TRIANGULAR_HIGHPASS = 3,
    FFMS_RESAMPLE_DITHER_TRIANGULAR_NOISESHAPING = 4,
}

public enum FFMS_LogLevels
{
    FFMS_LOG_QUIET = -8,
    FFMS_LOG_PANIC = 0,
    FFMS_LOG_FATAL = 8,
    FFMS_LOG_ERROR = 16,
    FFMS_LOG_WARNING = 24,
    FFMS_LOG_INFO = 32,
    FFMS_LOG_VERBOSE = 40,
    FFMS_LOG_DEBUG = 48,
    FFMS_LOG_TRACE = 56,
}

public partial struct FFMS_ResampleOptions
{
    [NativeTypeName("int64_t")]
    public long ChannelLayout;

    public FFMS_SampleFormat SampleFormat;

    public int SampleRate;

    public FFMS_MixingCoefficientType MixingCoefficientType;

    public double CenterMixLevel;

    public double SurroundMixLevel;

    public double LFEMixLevel;

    public int Normalize;

    public int ForceResample;

    public int ResampleFilterSize;

    public int ResamplePhaseShift;

    public int LinearInterpolation;

    public double CutoffFrequencyRatio;

    public FFMS_MatrixEncoding MatrixedStereoEncoding;

    public FFMS_ResampleFilterType FilterType;

    public int KaiserBeta;

    public FFMS_AudioDitherMethod DitherMethod;
}

public unsafe partial struct FFMS_Frame
{
    [NativeTypeName("const uint8_t *[4]")]
    public _Data_e__FixedBuffer Data;

    [NativeTypeName("int[4]")]
    public fixed int Linesize[4];

    public int EncodedWidth;

    public int EncodedHeight;

    public int EncodedPixelFormat;

    public int ScaledWidth;

    public int ScaledHeight;

    public int ConvertedPixelFormat;

    public int KeyFrame;

    public int RepeatPict;

    public int InterlacedFrame;

    public int TopFieldFirst;

    [NativeTypeName("char")]
    public sbyte PictType;

    public int ColorSpace;

    public int ColorRange;

    public int ColorPrimaries;

    public int TransferCharateristics;

    public int ChromaLocation;

    public int HasMasteringDisplayPrimaries;

    [NativeTypeName("double[3]")]
    public fixed double MasteringDisplayPrimariesX[3];

    [NativeTypeName("double[3]")]
    public fixed double MasteringDisplayPrimariesY[3];

    public double MasteringDisplayWhitePointX;

    public double MasteringDisplayWhitePointY;

    public int HasMasteringDisplayLuminance;

    public double MasteringDisplayMinLuminance;

    public double MasteringDisplayMaxLuminance;

    public int HasContentLightLevel;

    [NativeTypeName("unsigned int")]
    public uint ContentLightLevelMax;

    [NativeTypeName("unsigned int")]
    public uint ContentLightLevelAverage;

    [NativeTypeName("uint8_t *")]
    public byte* DolbyVisionRPU;

    public int DolbyVisionRPUSize;

    public unsafe partial struct _Data_e__FixedBuffer
    {
        public byte* e0;
        public byte* e1;
        public byte* e2;
        public byte* e3;

        public ref byte* this[int index]
        {
            get
            {
                fixed (byte** pThis = &e0)
                {
                    return ref pThis[index];
                }
            }
        }
    }
}

public partial struct FFMS_TrackTimeBase
{
    [NativeTypeName("int64_t")]
    public long Num;

    [NativeTypeName("int64_t")]
    public long Den;
}

public partial struct FFMS_FrameInfo
{
    [NativeTypeName("int64_t")]
    public long PTS;

    public int RepeatPict;

    public int KeyFrame;

    [NativeTypeName("int64_t")]
    public long OriginalPTS;
}

public unsafe partial struct FFMS_VideoProperties
{
    public int FPSDenominator;

    public int FPSNumerator;

    public int RFFDenominator;

    public int RFFNumerator;

    public int NumFrames;

    public int SARNum;

    public int SARDen;

    public int CropTop;

    public int CropBottom;

    public int CropLeft;

    public int CropRight;

    public int TopFieldFirst;

    [Obsolete]
    public int ColorSpace;

    [Obsolete]
    public int ColorRange;

    public double FirstTime;

    public double LastTime;

    public int Rotation;

    public int Stereo3DType;

    public int Stereo3DFlags;

    public double LastEndTime;

    public int HasMasteringDisplayPrimaries;

    [NativeTypeName("double[3]")]
    public fixed double MasteringDisplayPrimariesX[3];

    [NativeTypeName("double[3]")]
    public fixed double MasteringDisplayPrimariesY[3];

    public double MasteringDisplayWhitePointX;

    public double MasteringDisplayWhitePointY;

    public int HasMasteringDisplayLuminance;

    public double MasteringDisplayMinLuminance;

    public double MasteringDisplayMaxLuminance;

    public int HasContentLightLevel;

    [NativeTypeName("unsigned int")]
    public uint ContentLightLevelMax;

    [NativeTypeName("unsigned int")]
    public uint ContentLightLevelAverage;

    public int Flip;
}

public partial struct FFMS_AudioProperties
{
    public int SampleFormat;

    public int SampleRate;

    public int BitsPerSample;

    public int Channels;

    [NativeTypeName("int64_t")]
    public long ChannelLayout;

    [NativeTypeName("int64_t")]
    public long NumSamples;

    public double FirstTime;

    public double LastTime;

    public double LastEndTime;
}

public unsafe partial struct FFMS_KeyValuePair
{
    [NativeTypeName("const char *")]
    public sbyte* Key;

    [NativeTypeName("const char *")]
    public sbyte* Value;
}