using System.Buffers;
using System.Text;
using FluentAssertions;
using MessagePack;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;

namespace Mobsub.Test;

[TestClass]
public sealed class AutomationBridgeProtocolTests
{
    [TestMethod]
    public void Envelope_NoEnvelope_ReturnsFalse()
    {
        byte[] payload = [0x01, 0x02, 0x03];

        BridgeEnvelope.TryUnwrap(payload, out var unwrapped, out var error)
            .Should().BeFalse();
        error.Should().Be("Missing MSB1 envelope.");
        unwrapped.IsEmpty.Should().BeTrue();
    }

    [TestMethod]
    public void Envelope_WrapRoundtrip()
    {
        byte[] payload = [0x10, 0x20, 0x30, 0x40];
        byte[] wrapped = BridgeEnvelope.Wrap(payload);

        BridgeEnvelope.TryUnwrap(wrapped, out var unwrapped, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        unwrapped.SequenceEqual(payload).Should().BeTrue();
    }

    [TestMethod]
    public void MessagePack_Decode_PingRequest()
    {
        byte[] req = BridgeMessagePack.SerializeRequest(new BridgeRequest(BridgeMessagePack.SchemaVersion, new BridgePingCall()));

        BridgeMessagePack.TryDeserializeRequest(req, out var request, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        request.Should().NotBeNull();
        request!.SchemaVersion.Should().Be(BridgeMessagePack.SchemaVersion);
        request.Call.Should().BeOfType<BridgePingCall>();
    }

    [TestMethod]
    public void MessagePack_Decode_LineTextUtf8_AsStr()
    {
        byte[] req = BuildInvokeRequest(static (ref MessagePackWriter w) =>
        {
            // MotionAmoApplyCall union: [kind=2, payload=[context, lines, args]]
            w.WriteArrayHeader(2);
            w.WriteInt32(2);

            w.WriteArrayHeader(3);

            // context: [script_resolution=[w,h]]
            w.WriteArrayHeader(1);
            w.WriteArrayHeader(2);
            w.WriteInt32(1920);
            w.WriteInt32(1080);

            // lines: [[index, class, text_utf8, ...]]
            w.WriteArrayHeader(1);
            w.WriteArrayHeader(3);
            w.WriteInt32(1);
            w.WriteString("dialogue"u8);
            w.WriteString("hello"u8); // as str (Lua MessagePack usually encodes strings as str)

            // args: [selection_start_frame, total_frames, frame_ms, main_data, clip_data, ...]
            w.WriteArrayHeader(5);
            w.WriteInt32(0);
            w.WriteInt32(1);
            w.WriteArrayHeader(2);
            w.WriteInt32(0);
            w.WriteInt32(40);
            w.WriteString(""u8);
            w.WriteNil();
        });

        BridgeMessagePack.TryDeserializeRequest(req, out var request, out var error)
            .Should().BeTrue();
        error.Should().BeNull();

        var typed = request!.Call.Should().BeOfType<MotionAmoApplyCall>().Subject;
        typed.Lines.Should().HaveCount(1);
        typed.Lines[0].TextUtf8.Should().NotBeNull();
        typed.Lines[0].TextUtf8!.Value.Span.SequenceEqual("hello"u8).Should().BeTrue();
    }

    [TestMethod]
    public void MessagePack_Decode_MainDataUtf8_AsStr()
    {
        byte[] mainDataUtf8 = "\uFEFF  Adobe After Effects 6.0 Keyframe Data"u8.ToArray();
        byte[] req = BuildInvokeRequest((ref MessagePackWriter w) =>
        {
            w.WriteArrayHeader(2);
            w.WriteInt32(2);

            w.WriteArrayHeader(3);

            // context: [script_resolution=[w,h]]
            w.WriteArrayHeader(1);
            w.WriteArrayHeader(2);
            w.WriteInt32(1920);
            w.WriteInt32(1080);

            // lines: []
            w.WriteArrayHeader(0);

            // args: [selection_start_frame, total_frames, frame_ms, main_data, clip_data, ...]
            w.WriteArrayHeader(5);
            w.WriteInt32(0);
            w.WriteInt32(1);
            w.WriteArrayHeader(2);
            w.WriteInt32(0);
            w.WriteInt32(40);
            w.WriteString(mainDataUtf8); // as str
            w.WriteNil();
        });

        BridgeMessagePack.TryDeserializeRequest(req, out var request, out var error)
            .Should().BeTrue();
        error.Should().BeNull();

        var typed = request!.Call.Should().BeOfType<MotionAmoApplyCall>().Subject;
        typed.Args.MainDataUtf8.Should().NotBeNull();
        typed.Args.MainDataUtf8!.Value.Span.SequenceEqual(mainDataUtf8).Should().BeTrue();
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_Ping_WithEnvelope_ReturnsPong()
    {
        byte[] req = BuildInvokeRequest(static (ref MessagePackWriter w) =>
        {
            w.WriteArrayHeader(2);
            w.WriteInt32(0);
            w.WriteArrayHeader(0);
        });
        byte[] wrapped = BridgeEnvelope.Wrap(req);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.Ok);

        BridgeEnvelope.TryUnwrap(result.ResponseBytes, out var payload, out var error)
            .Should().BeTrue();
        error.Should().BeNull();

        BridgeMessagePack.TryDeserializeResponse(payload, out var resp, out var decodeError)
            .Should().BeTrue();
        decodeError.Should().BeNull();
        resp.Should().NotBeNull();
        resp!.Ok.Should().BeTrue();
        resp.Logs.Should().Equal(["pong"]);
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_ListMethods_ReturnsMethods()
    {
        byte[] req = BuildInvokeRequest(static (ref MessagePackWriter w) =>
        {
            w.WriteArrayHeader(2);
            w.WriteInt32(1);
            w.WriteArrayHeader(0);
        });
        byte[] wrapped = BridgeEnvelope.Wrap(req);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.Ok);

        BridgeEnvelope.TryUnwrap(result.ResponseBytes, out var payload, out var error)
            .Should().BeTrue();
        error.Should().BeNull();

        BridgeMessagePack.TryDeserializeResponse(payload, out var resp, out var decodeError)
            .Should().BeTrue();
        decodeError.Should().BeNull();

        resp!.Ok.Should().BeTrue();
        resp.Methods.Should().NotBeNull();
        resp.Methods!.Select(m => m.Name).Should().Contain("ping");
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_UnknownCallKind_ReturnsErrDecode()
    {
        byte[] req = BuildInvokeRequest(static (ref MessagePackWriter w) =>
        {
            w.WriteArrayHeader(2);
            w.WriteInt32(999);
            w.WriteArrayHeader(0);
        });
        byte[] wrapped = BridgeEnvelope.Wrap(req);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.ErrDecode);

        BridgeEnvelope.TryUnwrap(result.ResponseBytes, out var payload, out var error)
            .Should().BeTrue();
        error.Should().BeNull();

        BridgeMessagePack.TryDeserializeResponse(payload, out var resp, out var decodeError)
            .Should().BeTrue();
        decodeError.Should().BeNull();

        resp!.Ok.Should().BeFalse();
        resp.Error.Should().Contain("call");
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_MotionAmoApply_ReturnsBadArgs()
    {
        byte[] req = BuildInvokeRequest(static (ref MessagePackWriter w) =>
        {
            w.WriteArrayHeader(2);
            w.WriteInt32(2);

            w.WriteArrayHeader(3);

            // context: [script_resolution=[w,h]]
            w.WriteArrayHeader(1);
            w.WriteArrayHeader(2);
            w.WriteInt32(1920);
            w.WriteInt32(1080);

            // lines: [[index, class, text_utf8, ...]]
            w.WriteArrayHeader(1);
            w.WriteArrayHeader(3);
            w.WriteInt32(1);
            w.WriteString("dialogue"u8);
            w.WriteString("hello"u8);

            // args: [selection_start_frame, total_frames, frame_ms, main_data, clip_data, ...]
            w.WriteArrayHeader(5);
            w.WriteInt32(0);
            w.WriteInt32(1);
            w.WriteArrayHeader(2);
            w.WriteInt32(0);
            w.WriteInt32(40);
            w.WriteString(""u8);
            w.WriteNil();
        });

        byte[] wrapped = BridgeEnvelope.Wrap(req);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.ErrBadArgs);

        BridgeEnvelope.TryUnwrap(result.ResponseBytes, out var payload, out var error)
            .Should().BeTrue();
        error.Should().BeNull();

        BridgeMessagePack.TryDeserializeResponse(payload, out var resp, out var decodeError)
            .Should().BeTrue();
        decodeError.Should().BeNull();

        resp!.Ok.Should().BeFalse();
        resp.Error.Should().Contain("No motion data provided");
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_DrawingOptimizeLines_ReturnsSetTextPatch()
    {
        var line = new BridgeLine(
            Index: 1,
            Class: "dialogue",
            TextUtf8: "{\\p1}m 0 0 b 0 0 10 0 10 10"u8.ToArray(),
            Raw: null,
            StartTime: null,
            EndTime: null,
            StartFrame: null,
            EndFrame: null,
            Layer: null,
            Comment: null,
            Style: null,
            Actor: null,
            Effect: null,
            MarginL: null,
            MarginR: null,
            MarginT: null,
            Extra: null,
            Width: null,
            Height: null,
            Align: null);

        var call = new DrawingOptimizeLinesCall(
            Lines: [line],
            Args: new DrawingOptimizeLinesArgs(CurveTolerance: 0.25, SimplifyTolerance: 0.1, PrecisionDecimals: 0));

        byte[] payload = BridgeMessagePack.SerializeRequest(new BridgeRequest(BridgeMessagePack.SchemaVersion, call));
        byte[] wrapped = BridgeEnvelope.Wrap(payload);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.Ok);

        BridgeEnvelope.TryUnwrap(result.ResponseBytes, out var respPayload, out var unwrapError)
            .Should().BeTrue();
        unwrapError.Should().BeNull();

        BridgeMessagePack.TryDeserializeResponse(respPayload, out var resp, out var decodeError)
            .Should().BeTrue();
        decodeError.Should().BeNull();

        resp!.Ok.Should().BeTrue();
        resp.Patch.Should().NotBeNull();
        resp.Patch!.Ops.Should().ContainSingle();

        var op = resp.Patch!.Ops[0].Should().BeOfType<BridgeSetTextPatchOp>().Subject;
        op.Index.Should().Be(1);
        op.TextUtf8.Should().NotBeNull();

        string outText = Encoding.UTF8.GetString(op.TextUtf8!.Value.Span);
        outText.Should().NotContain(" b ");
        outText.Should().Contain(" l ");
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_MotionAmoApply_ReturnsSpliceTemplatePatch()
    {
        const string ae = """
Adobe After Effects 6.0 Keyframe Data

	Source Width	1920
	Source Height	1080

Position
	Frame	X pixels	Y pixels
	0	960	540
	1	970	540
""";

        var call = new MotionAmoApplyCall(
            Context: new BridgeContext(new BridgeScriptResolution(W: 1920, H: 1080)),
            Lines:
            [
                new BridgeLine(
                    Index: 1,
                    Class: "dialogue",
                    TextUtf8: "hello"u8.ToArray(),
                    Raw: null,
                    StartTime: 0,
                    EndTime: 80,
                    StartFrame: 0,
                    EndFrame: 2,
                    Layer: null,
                    Comment: null,
                    Style: null,
                    Actor: null,
                    Effect: null,
                    MarginL: null,
                    MarginR: null,
                    MarginT: null,
                    Extra: null,
                    Width: null,
                    Height: null,
                    Align: null),
            ],
            Args: new MotionAmoApplyArgs(
                SelectionStartFrame: 0,
                TotalFrames: 2,
                FrameMs: [0, 40, 80],
                MainDataUtf8: Encoding.UTF8.GetBytes(ae),
                ClipDataUtf8: null,
                Styles: null,
                Fix: new BridgeAmoFixOptions(Enabled: false, ApplyMain: true, ApplyClip: true, Diff: 0.2, RoundDecimals: 2),
                Main: new BridgeAmoMainOptions(
                    XPosition: true,
                    YPosition: true,
                    Origin: false,
                    AbsPos: false,
                    XScale: false,
                    Border: false,
                    Shadow: false,
                    Blur: false,
                    BlurScale: 1.0,
                    ZRotation: false,
                    ClipOnly: false,
                    RectClip: false,
                    VectClip: false,
                    RcToVc: false,
                    KillTrans: false,
                    Relative: true,
                    StartFrame: 1,
                    LinearMode: BridgeAmoLinearMode.ForceNonlinear,
                    SegmentPosEps: 0.0,
                    PosErrorMode: BridgeAmoPosErrorMode.Full),
                Clip: new BridgeAmoClipOptions(
                    XPosition: false,
                    YPosition: false,
                    XScale: false,
                    ZRotation: false,
                    RectClip: false,
                    VectClip: false,
                    RcToVc: false,
                    StartFrame: 1)));

        byte[] payload = BridgeMessagePack.SerializeRequest(new BridgeRequest(BridgeMessagePack.SchemaVersion, call));
        byte[] wrapped = BridgeEnvelope.Wrap(payload);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.Ok);

        BridgeEnvelope.TryUnwrap(result.ResponseBytes, out var respPayload, out var unwrapError)
            .Should().BeTrue();
        unwrapError.Should().BeNull();

        BridgeMessagePack.TryDeserializeResponse(respPayload, out var resp, out var decodeError)
            .Should().BeTrue();
        decodeError.Should().BeNull();

        resp!.Ok.Should().BeTrue();
        resp.Patch.Should().NotBeNull();
        resp.Patch!.Ops.Should().ContainSingle();

        var op = resp.Patch!.Ops[0].Should().BeOfType<BridgeSpliceTemplatePatchOp>().Subject;
        op.Index.Should().Be(1);
        op.DeleteCount.Should().Be(1);
        op.Templates.Should().BeNull();
        op.Inserts.Should().HaveCount(2);
        op.Inserts.Select(i => i.TemplateId).Should().AllBeEquivalentTo(0);
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_MotionAmoApply_AutoLinearPos_ReturnsSetText()
    {
        const string ae = """
Adobe After Effects 6.0 Keyframe Data

	Source Width	1920
	Source Height	1080

Position
	Frame	X pixels	Y pixels
	0	960	540
	1	970	540
""";

        var call = new MotionAmoApplyCall(
            Context: new BridgeContext(new BridgeScriptResolution(W: 1920, H: 1080)),
            Lines:
            [
                new BridgeLine(
                    Index: 1,
                    Class: "dialogue",
                    TextUtf8: "hello"u8.ToArray(),
                    Raw: null,
                    StartTime: 0,
                    EndTime: 80,
                    StartFrame: 0,
                    EndFrame: 2,
                    Layer: null,
                    Comment: null,
                    Style: null,
                    Actor: null,
                    Effect: null,
                    MarginL: null,
                    MarginR: null,
                    MarginT: null,
                    Extra: null,
                    Width: null,
                    Height: null,
                    Align: null),
            ],
            Args: new MotionAmoApplyArgs(
                SelectionStartFrame: 0,
                TotalFrames: 2,
                FrameMs: [0, 40, 80],
                MainDataUtf8: Encoding.UTF8.GetBytes(ae),
                ClipDataUtf8: null,
                Styles: null,
                Fix: new BridgeAmoFixOptions(Enabled: false, ApplyMain: true, ApplyClip: true, Diff: 0.2, RoundDecimals: 2),
                Main: new BridgeAmoMainOptions(
                    XPosition: true,
                    YPosition: true,
                    Origin: false,
                    AbsPos: false,
                    XScale: false,
                    Border: false,
                    Shadow: false,
                    Blur: false,
                    BlurScale: 1.0,
                    ZRotation: false,
                    ClipOnly: false,
                    RectClip: false,
                    VectClip: false,
                    RcToVc: false,
                    KillTrans: false,
                    Relative: true,
                    StartFrame: 1,
                    LinearMode: BridgeAmoLinearMode.AutoLinearPos,
                    SegmentPosEps: 0.0,
                    PosErrorMode: BridgeAmoPosErrorMode.Full),
                Clip: new BridgeAmoClipOptions(
                    XPosition: false,
                    YPosition: false,
                    XScale: false,
                    ZRotation: false,
                    RectClip: false,
                    VectClip: false,
                    RcToVc: false,
                    StartFrame: 1)));

        byte[] payload = BridgeMessagePack.SerializeRequest(new BridgeRequest(BridgeMessagePack.SchemaVersion, call));
        byte[] wrapped = BridgeEnvelope.Wrap(payload);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.Ok);

        BridgeEnvelope.TryUnwrap(result.ResponseBytes, out var respPayload, out var unwrapError)
            .Should().BeTrue();
        unwrapError.Should().BeNull();

        BridgeMessagePack.TryDeserializeResponse(respPayload, out var resp, out var decodeError)
            .Should().BeTrue();
        decodeError.Should().BeNull();

        resp!.Ok.Should().BeTrue();
        resp.Patch.Should().NotBeNull();
        resp.Patch!.Ops.Should().ContainSingle();

        var op = resp.Patch!.Ops[0].Should().BeOfType<BridgeSetTextPatchOp>().Subject;
        op.Index.Should().Be(1);
        op.TextUtf8.Should().NotBeNull();

        string outText = Encoding.UTF8.GetString(op.TextUtf8!.Value.Span);
        outText.Should().Contain("\\move(");
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_MotionAmoApply_AutoSegmentPos_ReducesLines()
    {
        const string ae = """
Adobe After Effects 6.0 Keyframe Data

	Source Width	1920
	Source Height	1080

Position
	Frame	X pixels	Y pixels
	0	0	0
	1	10	0
	2	20	0
	3	30	0
	4	30	10
	5	30	20
	6	30	30
""";

        var call = new MotionAmoApplyCall(
            Context: new BridgeContext(new BridgeScriptResolution(W: 1920, H: 1080)),
            Lines:
            [
                new BridgeLine(
                    Index: 1,
                    Class: "dialogue",
                    TextUtf8: "hello"u8.ToArray(),
                    Raw: null,
                    StartTime: 0,
                    EndTime: 280,
                    StartFrame: 0,
                    EndFrame: 7,
                    Layer: null,
                    Comment: null,
                    Style: null,
                    Actor: null,
                    Effect: null,
                    MarginL: null,
                    MarginR: null,
                    MarginT: null,
                    Extra: null,
                    Width: null,
                    Height: null,
                    Align: null),
            ],
            Args: new MotionAmoApplyArgs(
                SelectionStartFrame: 0,
                TotalFrames: 7,
                FrameMs: [0, 40, 80, 120, 160, 200, 240, 280],
                MainDataUtf8: Encoding.UTF8.GetBytes(ae),
                ClipDataUtf8: null,
                Styles: null,
                Fix: new BridgeAmoFixOptions(Enabled: false, ApplyMain: true, ApplyClip: true, Diff: 0.2, RoundDecimals: 2),
                Main: new BridgeAmoMainOptions(
                    XPosition: true,
                    YPosition: true,
                    Origin: false,
                    AbsPos: false,
                    XScale: false,
                    Border: false,
                    Shadow: false,
                    Blur: false,
                    BlurScale: 1.0,
                    ZRotation: false,
                    ClipOnly: false,
                    RectClip: false,
                    VectClip: false,
                    RcToVc: false,
                    KillTrans: false,
                    Relative: true,
                    StartFrame: 1,
                    LinearMode: BridgeAmoLinearMode.AutoSegmentPos,
                    SegmentPosEps: 0.0,
                    PosErrorMode: BridgeAmoPosErrorMode.Full),
                Clip: new BridgeAmoClipOptions(
                    XPosition: false,
                    YPosition: false,
                    XScale: false,
                    ZRotation: false,
                    RectClip: false,
                    VectClip: false,
                    RcToVc: false,
                    StartFrame: 1)));

        byte[] payload = BridgeMessagePack.SerializeRequest(new BridgeRequest(BridgeMessagePack.SchemaVersion, call));
        byte[] wrapped = BridgeEnvelope.Wrap(payload);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.Ok);

        BridgeEnvelope.TryUnwrap(result.ResponseBytes, out var respPayload, out var unwrapError)
            .Should().BeTrue();
        unwrapError.Should().BeNull();

        BridgeMessagePack.TryDeserializeResponse(respPayload, out var resp, out var decodeError)
            .Should().BeTrue();
        decodeError.Should().BeNull();

        resp!.Ok.Should().BeTrue();
        resp.Patch.Should().NotBeNull();
        resp.Patch!.Ops.Should().ContainSingle();

        var op = resp.Patch!.Ops[0].Should().BeOfType<BridgeSpliceTemplatePatchOp>().Subject;
        op.Inserts.Should().HaveCount(2);
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_RequestMissingCall_ReturnsErrDecode()
    {
        byte[] req = BuildMessagePack(static (ref MessagePackWriter w) =>
        {
            // BridgeRequest: [schema_version] (call missing)
            w.WriteArrayHeader(1);
            w.WriteInt32(BridgeMessagePack.SchemaVersion);
        });
        byte[] wrapped = BridgeEnvelope.Wrap(req);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.ErrDecode);
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_CallNotArray_ReturnsErrDecode()
    {
        byte[] req = BuildMessagePack(static (ref MessagePackWriter w) =>
        {
            w.WriteArrayHeader(2);
            w.WriteInt32(BridgeMessagePack.SchemaVersion);
            w.WriteString("not_a_call"u8);
        });
        byte[] wrapped = BridgeEnvelope.Wrap(req);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.ErrDecode);
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_CallMissingPayload_ReturnsErrDecode()
    {
        byte[] req = BuildInvokeRequest(static (ref MessagePackWriter w) =>
        {
            // call union: [kind] (payload missing)
            w.WriteArrayHeader(1);
            w.WriteInt32(0);
        });
        byte[] wrapped = BridgeEnvelope.Wrap(req);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.ErrDecode);
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_CallPayloadWrongType_ReturnsErrDecode()
    {
        byte[] req = BuildInvokeRequest(static (ref MessagePackWriter w) =>
        {
            // DrawingOptimizeLinesCall union: [kind=6, payload=[lines, args]]
            w.WriteArrayHeader(2);
            w.WriteInt32(6);

            w.WriteArrayHeader(2);
            w.WriteInt32(123); // lines should be an array
            w.WriteArrayHeader(3); // args
            w.Write(0.25);
            w.Write(0.1);
            w.WriteInt32(0);
        });
        byte[] wrapped = BridgeEnvelope.Wrap(req);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.ErrDecode);
    }

    [TestMethod]
    public void BridgeDispatcher_Invoke_OverlongPayload_ReturnsErrDecode()
    {
        byte[] payload = new byte[BridgeProtocolLimits.MaxPayloadBytes + 1];
        byte[] wrapped = BridgeEnvelope.Wrap(payload);

        var result = BridgeDispatcher.Invoke(wrapped);
        result.Code.Should().Be(BridgeErrorCodes.ErrDecode);
    }

    private delegate void WriteMessagePack(ref MessagePackWriter writer);

    private static byte[] BuildInvokeRequest(WriteMessagePack call)
    {
        return BuildMessagePack((ref MessagePackWriter writer) =>
        {
            // BridgeRequest: [schema_version, call]
            writer.WriteArrayHeader(2);
            writer.WriteInt32(BridgeMessagePack.SchemaVersion);
            call(ref writer);
        });
    }

    private static byte[] BuildMessagePack(WriteMessagePack write)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        var writer = new MessagePackWriter(buffer);
        write(ref writer);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}
