#nullable enable

using MessagePack;
using Mobsub.AutomationBridge.Common;

namespace Mobsub.AutomationBridge.Protocol;

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgePingCall() : IBridgeCall;

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeListMethodsCall() : IBridgeCall;

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeContext(
    [property: Key(0)] BridgeScriptResolution? ScriptResolution
);

[LuaPackMode(LuaPackMode.Strict)]
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeScriptResolution(
    [property: Key(0), LuaMin(1)] int W,
    [property: Key(1), LuaMin(1)] int H
);

[LuaPackMode(LuaPackMode.Nilable)]
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeLine(
    [property: Key(0)] int Index,
    [property: Key(1)] string Class,
    [property: Key(2), MessagePackFormatter(typeof(Utf8BytesReadOnlyMemoryFormatter)), LuaAltKeys("text"), LuaDefault("")] ReadOnlyMemory<byte>? TextUtf8,
    [property: Key(3)] string? Raw,
    [property: Key(4)] int? StartTime,
    [property: Key(5)] int? EndTime,
    [property: Key(6)] int? StartFrame,
    [property: Key(7)] int? EndFrame,
    [property: Key(8)] int? Layer,
    [property: Key(9)] bool? Comment,
    [property: Key(10)] string? Style,
    [property: Key(11)] string? Actor,
    [property: Key(12)] string? Effect,
    [property: Key(13)] int? MarginL,
    [property: Key(14)] int? MarginR,
    [property: Key(15)] int? MarginT,
    [property: Key(16)] Dictionary<string, string>? Extra,
    [property: Key(17)] double? Width,
    [property: Key(18)] double? Height,
    [property: Key(19)] int? Align
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgePatch(
    [property: Key(0)] IBridgePatchOp[] Ops
);

[Union(0, typeof(BridgeSetTextPatchOp))]
[Union(1, typeof(BridgeSpliceTemplatePatchOp))]
internal interface IBridgePatchOp;

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeSetTextPatchOp(
    [property: Key(0)] int Index,
    [property: Key(1), MessagePackFormatter(typeof(Utf8BytesReadOnlyMemoryFormatter))] ReadOnlyMemory<byte>? TextUtf8
) : IBridgePatchOp;

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeSpliceTemplatePatchOp(
    [property: Key(0)] int Index,
    [property: Key(1)] int DeleteCount,
    [property: Key(2)] BridgeLineTemplate[]? Templates,
    [property: Key(3)] BridgeLineInsert[] Inserts
) : IBridgePatchOp;

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeLineTemplate(
    [property: Key(0)] string Class,
    [property: Key(1)] int? Layer,
    [property: Key(2)] bool? Comment,
    [property: Key(3)] string? Style,
    [property: Key(4)] string? Actor,
    [property: Key(5)] string? Effect,
    [property: Key(6)] int? MarginL,
    [property: Key(7)] int? MarginR,
    [property: Key(8)] int? MarginT,
    [property: Key(9)] Dictionary<string, string>? Extra
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeLineInsert(
    [property: Key(0)] int TemplateId,
    [property: Key(1)] int StartTime,
    [property: Key(2)] int EndTime,
    [property: Key(3), MessagePackFormatter(typeof(Utf8BytesReadOnlyMemoryFormatter))] ReadOnlyMemory<byte>? TextUtf8
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeResult(
    [property: Key(0), MessagePackFormatter(typeof(Utf8BytesReadOnlyMemoryFormatter))] ReadOnlyMemory<byte>? TextUtf8
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeMethodInfo(
    [property: Key(0)] string Name,
    [property: Key(1)] string Description
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeResponse(
    [property: Key(0)] bool Ok,
    [property: Key(1)] string? Error,
    [property: Key(2)] string[]? Logs,
    [property: Key(3)] BridgePatch? Patch,
    [property: Key(4)] BridgeResult? Result,
    [property: Key(5)] BridgeMethodInfo[]? Methods
);

internal enum BridgeAmoLinearMode : int
{
    ForceNonlinear = 0,
    ForceLinear = 1,
    AutoLinearPos = 2,
    AutoSegmentPos = 3,
}

internal enum BridgeAmoPosErrorMode : int
{
    Full = 0,
    IgnoreScaleRot = 1,
}

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal readonly partial record struct BridgeAmoFixOptions(
    [property: Key(0)] bool Enabled = false,
    [property: Key(1)] bool ApplyMain = true,
    [property: Key(2)] bool ApplyClip = true,
    [property: Key(3)] double Diff = 0.2,
    [property: Key(4)] int RoundDecimals = 2
);

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal readonly partial record struct BridgeAmoMainOptions(
    [property: Key(0)] bool XPosition = true,
    [property: Key(1)] bool YPosition = true,
    [property: Key(2)] bool Origin = false,
    [property: Key(3)] bool AbsPos = false,
    [property: Key(4)] bool XScale = true,
    [property: Key(5)] bool Border = true,
    [property: Key(6)] bool Shadow = true,
    [property: Key(7)] bool Blur = true,
    [property: Key(8)] double BlurScale = 1.0,
    [property: Key(9)] bool ZRotation = false,
    [property: Key(11)] bool ClipOnly = false,
    [property: Key(12)] bool RectClip = true,
    [property: Key(13)] bool VectClip = true,
    [property: Key(14)] bool RcToVc = false,
    [property: Key(15)] bool KillTrans = true,
    [property: Key(16)] bool Relative = true,
    [property: Key(17)] int StartFrame = 1,
    [property: Key(18)] BridgeAmoLinearMode LinearMode = BridgeAmoLinearMode.AutoLinearPos,
    [property: Key(19)] double SegmentPosEps = 0.0,
    [property: Key(20)] BridgeAmoPosErrorMode PosErrorMode = BridgeAmoPosErrorMode.Full
);

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal readonly partial record struct BridgeAmoClipOptions(
    [property: Key(0)] bool XPosition = true,
    [property: Key(1)] bool YPosition = true,
    [property: Key(2)] bool XScale = true,
    [property: Key(3)] bool ZRotation = false,
    [property: Key(4)] bool RectClip = true,
    [property: Key(5)] bool VectClip = true,
    [property: Key(6)] bool RcToVc = false,
    [property: Key(7)] int StartFrame = 1
);

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record MotionAmoApplyArgs(
    [property: Key(0)] int SelectionStartFrame,
    [property: Key(1)] int TotalFrames,
    [property: Key(2)] int[] FrameMs,
    [property: Key(3), MessagePackFormatter(typeof(Utf8BytesReadOnlyMemoryFormatter)), LuaKey("main_data"), LuaAltKeys("main_data_utf8"), LuaDefault("")] ReadOnlyMemory<byte>? MainDataUtf8,
    [property: Key(4), MessagePackFormatter(typeof(Utf8BytesReadOnlyMemoryFormatter)), LuaKey("clip_data"), LuaAltKeys("clip_data_utf8"), LuaEmptyStringAsNil] ReadOnlyMemory<byte>? ClipDataUtf8,
    [property: Key(5)] Dictionary<string, StyleInfo>? Styles,
    [property: Key(6)] BridgeAmoFixOptions Fix,
    [property: Key(7)] BridgeAmoMainOptions Main,
    [property: Key(8)] BridgeAmoClipOptions Clip
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record MotionAmoApplyCall(
    [property: Key(0)] BridgeContext Context,
    [property: Key(1)] BridgeLine[] Lines,
    [property: Key(2)] MotionAmoApplyArgs Args
) : IBridgeCall;

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record PerspectiveApplyClipQuadArgs(
    [property: Key(0)] string AeText = "",
    [property: Key(1)] string? EffectGroup = null,
    [property: Key(2)] int? Frame = null
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record PerspectiveApplyClipQuadCall(
    [property: Key(0)] BridgeContext Context,
    [property: Key(1)] BridgeLine[] Lines,
    [property: Key(2)] PerspectiveApplyClipQuadArgs Args
) : IBridgeCall;

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record PerspectiveApplyTagsFromQuadArgs(
    [property: Key(0)] string AeText = "",
    [property: Key(1)] string? EffectGroup = null,
    [property: Key(2)] int? Frame = null,
    [property: Key(3)] double? Width = null,
    [property: Key(4)] double? Height = null,
    [property: Key(5)] int? Align = null,
    [property: Key(6)] int OrgMode = 2,
    [property: Key(7)] double LayoutScale = 1.0,
    [property: Key(8)] int PrecisionDecimals = 3,
    [property: Key(9)] double OriginX = 0.0,
    [property: Key(10)] double OriginY = 0.0
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record PerspectiveApplyTagsFromQuadCall(
    [property: Key(0)] BridgeContext Context,
    [property: Key(1)] BridgeLine[] Lines,
    [property: Key(2)] PerspectiveApplyTagsFromQuadArgs Args
) : IBridgeCall;

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record PerspectiveApplyTagsFromClipQuadArgs(
    [property: Key(0)] double? Width = null,
    [property: Key(1)] double? Height = null,
    [property: Key(2)] int? Align = null,
    [property: Key(3)] int OrgMode = 2,
    [property: Key(4)] double LayoutScale = 1.0,
    [property: Key(5)] int PrecisionDecimals = 3,
    [property: Key(6)] double OriginX = 0.0,
    [property: Key(7)] double OriginY = 0.0
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record PerspectiveApplyTagsFromClipQuadCall(
    [property: Key(0)] BridgeLine[] Lines,
    [property: Key(1)] PerspectiveApplyTagsFromClipQuadArgs Args
) : IBridgeCall;

[LuaPackMode(LuaPackMode.Default)]
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record DrawingOptimizeLinesArgs(
    [property: Key(0)] double CurveTolerance = 0.25,
    [property: Key(1)] double SimplifyTolerance = 0.1,
    [property: Key(2)] int PrecisionDecimals = 0
);

[MessagePackObject(AllowPrivate = true)]
internal sealed partial record DrawingOptimizeLinesCall(
    [property: Key(0)] BridgeLine[] Lines,
    [property: Key(1)] DrawingOptimizeLinesArgs Args
) : IBridgeCall;

