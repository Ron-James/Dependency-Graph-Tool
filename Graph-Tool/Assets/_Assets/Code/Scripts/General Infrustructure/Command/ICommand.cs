using System.Threading;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.VisualScripting;
using UnityEngine;
using CancellationToken = System.Threading.CancellationToken;
using Task = System.Threading.Tasks.Task;

public interface ICommand : INamedEntry, IContextual<ICommandInvoker>, IInit
{
    Task Execute(CancellationToken token);
    Task Undo(CancellationToken token);
}




// --------------------------------------------------------------------------------------
// Example Command: Wait for an external event a number of times
// --------------------------------------------------------------------------------------
// This shows how a command can rely on some external event bus and complete only after
// receiving a certain number of notifications.

public class MessageWaitCommand : Command, IListener
{
    [OdinSerialize] private IEvent _event; // Provided via inspector (Odin-serialized interface)
    [SerializeField] private int _requiredCallCount = 1; // How many event hits required
    [ShowInInspector, ReadOnly] private int _callCount;
    

    protected override Task UndoContent(CancellationToken token)
    {
        throw new System.NotImplementedException();
    }

    protected override async Task ExecutionContent(CancellationToken token)
    {
        // Reset state for safety if this command is reused.
        _callCount = 0;

        if (_event == null)
        {
            Debug.LogWarning("MessageWaitCommand skipped: eventBus not set.");
            return;
        }

        SubscribeEvent();
        // Yield until the required number of events have been received.
        while (_callCount < _requiredCallCount)
        {
            await Awaitable.NextFrameAsync(token);
        }

        // Always unsubscribe, even if an exception happens inside the loop.
        UnsubscribeEvent();
    }

    public override void Dispose()
    {
        UnsubscribeEvent();
    }


    private void SubscribeEvent() => _event.RegisterListener(this);
    private void UnsubscribeEvent() => _event.DeregisterListener(this);

    // Called by the event bus when the relevant event is raised.
    public void OnEventRaised()
    {
        _callCount++;
    }

    public void OnRaised()
    {
        throw new System.NotImplementedException();
    }
}
