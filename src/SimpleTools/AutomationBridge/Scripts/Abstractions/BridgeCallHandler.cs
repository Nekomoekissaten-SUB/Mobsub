using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;

namespace Mobsub.AutomationBridge.Scripts.Abstractions;

internal interface IBridgeCallHandler
{
    BridgeMethodInfo MethodInfo { get; }
    Type CallType { get; }
    BridgeHandlerResult Handle(IBridgeCall call, List<string> logs);
}

internal sealed class BridgeCallHandler<TCall> : IBridgeCallHandler
    where TCall : IBridgeCall
{
    public BridgeMethodInfo MethodInfo { get; }
    public Type CallType { get; } = typeof(TCall);

    private readonly Func<TCall, List<string>, BridgeHandlerResult> _handler;

    public BridgeCallHandler(BridgeMethodInfo methodInfo, Func<TCall, List<string>, BridgeHandlerResult> handler)
    {
        MethodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public BridgeHandlerResult Handle(IBridgeCall call, List<string> logs)
    {
        if (call is not TCall typed)
        {
            return new BridgeHandlerResult(
                BridgeErrorCodes.ErrUnknownMethod,
                new BridgeResponse(
                    Ok: false,
                    Error: $"Handler call type mismatch: expected {typeof(TCall).Name}, got {call.GetType().Name}.",
                    Logs: logs.ToArray(),
                    Patch: null,
                    Result: null,
                    Methods: null));
        }

        return _handler(typed, logs);
    }
}
