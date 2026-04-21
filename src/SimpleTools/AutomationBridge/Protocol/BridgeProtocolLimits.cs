namespace Mobsub.AutomationBridge.Protocol;

internal static class BridgeProtocolLimits
{
    // Upper bound for MSB1 payload size (MessagePack bytes), to avoid accidental or malicious OOM.
    // This is intended to be generous for normal Aegisub use while still protecting the host.
    public const int MaxPayloadBytes = 16 * 1024 * 1024;
}

