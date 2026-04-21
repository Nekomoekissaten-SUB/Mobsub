using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;

namespace Mobsub.AutomationBridge.Scripts;

internal static partial class BridgeScriptCatalog
{
    private static BridgeHandlerResult HandlePing(BridgePingCall _, List<string> logs)
    {
        logs.Add("pong");
        return new BridgeHandlerResult(
            BridgeErrorCodes.Ok,
            new BridgeResponse(true, null, logs.ToArray(), Patch: null, Result: null, Methods: null));
    }

    private static BridgeHandlerResult HandleListMethods(BridgeListMethodsCall _, List<string> logs)
        => new(BridgeErrorCodes.Ok, new BridgeResponse(true, null, logs.ToArray(), Patch: null, Result: null, Methods));
}

