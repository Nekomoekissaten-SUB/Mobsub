using Mobsub.AutomationBridge.Protocol;

namespace Mobsub.AutomationBridge.Dispatch;

internal readonly record struct BridgeHandlerResult(int Code, BridgeResponse Response);
