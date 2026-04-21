using MessagePack;

namespace Mobsub.AutomationBridge.Protocol;

/// <summary>
/// Unified invoke request: <c>[schema_version, call]</c>.
///
/// Note: schema_version is intentionally kept at <c>1</c> (no bump).
/// <c>call</c> is a MessagePack union: <c>[kind, payload]</c> where payload shape depends on kind.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
internal sealed partial record BridgeRequest(
    [property: Key(0)] int SchemaVersion,
    [property: Key(1)] IBridgeCall Call
);
