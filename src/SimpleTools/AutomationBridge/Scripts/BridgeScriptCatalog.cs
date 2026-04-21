using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.AutomationBridge.Scripts.Abstractions;

namespace Mobsub.AutomationBridge.Scripts;

internal static partial class BridgeScriptCatalog
{
    public static BridgeHandlerResult Dispatch(IBridgeCall call, List<string> logs)
    {
        if (HandlersByCallType.TryGetValue(call.GetType(), out var handler))
            return handler.Handle(call, logs);

        return new BridgeHandlerResult(
            BridgeErrorCodes.ErrUnknownMethod,
            new BridgeResponse(false, "Unknown call.", logs.ToArray(), Patch: null, Result: null, Methods: null));
    }

    private static partial IBridgeCallHandler[] CreateHandlers();

    private static readonly IBridgeCallHandler[] Handlers = CreateHandlers();
    private static readonly Dictionary<Type, IBridgeCallHandler> HandlersByCallType = CreateHandlerByCallType(Handlers);
    private static readonly BridgeMethodInfo[] Methods = CreateMethods(Handlers);

    private static Dictionary<Type, IBridgeCallHandler> CreateHandlerByCallType(IBridgeCallHandler[] handlers)
    {
        var dict = new Dictionary<Type, IBridgeCallHandler>(capacity: handlers.Length);
        for (int i = 0; i < handlers.Length; i++)
        {
            var h = handlers[i];
            dict.Add(h.CallType, h);
        }
        return dict;
    }

    private static BridgeMethodInfo[] CreateMethods(IBridgeCallHandler[] handlers)
    {
        var arr = new BridgeMethodInfo[handlers.Length];
        for (int i = 0; i < handlers.Length; i++)
            arr[i] = handlers[i].MethodInfo;
        return arr;
    }
}
