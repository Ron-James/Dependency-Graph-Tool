using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Events;


public abstract class Command : ICommand
{
    private enum CommandState
    {
        Idle,
        Executing,
        Completed,
        Canceled,
        RequestedCancel,
        RequestedParentCancel,
        RequestedCancelAndStop,
        RequestedParentCancelAndStop
        
    }
    
    
    [ShowInInspector, ReadOnly] CommandState _state = CommandState.Idle;
    [SerializeField] private string _name;
    

    public Command()
    {
        _name = GetType().Name;
    }


    public async Task Execute(CancellationToken token)
    {
        _state = CommandState.Executing;
        try
        {
            await ExecutionContent(token);
        }
        catch (OperationCanceledException)
        {
            _state = CommandState.Canceled;
            return;
        }
        _state = CommandState.Completed;
        
    }
    
    public async Task Undo(CancellationToken token)
    {
        await UndoContent(token);
    }

    protected virtual void RequestCancel()
    {
        Context?.Cancel();
        _state = CommandState.RequestedCancel;
    }

    protected virtual void RequestCancelAndStop()
    {
        Context?.Stop();
        Context?.Cancel();
        _state = CommandState.RequestedCancelAndStop;
    }
    
    
    protected virtual void RequestParentCancel()
    {
        if(Context is IContext<ICommandInvoker> parentContext)
        {
            parentContext.Context?.Cancel();
            _state = CommandState.RequestedParentCancel;
        }
    }
    protected virtual void RequestParentCancelAndStop()
    {
        if(Context is IContext<ICommandInvoker> parentContext)
        {
            parentContext.Context?.Stop();
            parentContext.Context?.Cancel();
            _state = CommandState.RequestedParentCancelAndStop;
        }
    }
    
    protected abstract Task UndoContent(CancellationToken token);
    protected abstract Task ExecutionContent(CancellationToken token);
    [ShowInInspector, ReadOnly]
    public ICommandInvoker Context { get; set; }

    public virtual Task Init()
    {
        _state = CommandState.Idle;
        return Task.CompletedTask;
    }

    public virtual void Dispose()
    {
        _state = CommandState.Idle;
    }

    public string Name { get => _name; set => _name = value; }
}


public class QueueCommand : Command, ICommandQueue
{
    [OdinSerialize] private List<ICommand> _commands = new List<ICommand>();
    private ICommandQueue _commandQueue = new CommandQueue();


    public override async Task Init()
    {
        // Create fresh command queue
        _commandQueue = new CommandQueue();
        
        // Initialize all commands in the list
        foreach (var command in _commands)
        {
            await command.Init();
        }
        
        await base.Init();
    }

    protected override Task UndoContent(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    protected override async Task ExecutionContent(CancellationToken token)
    {
        // Set the context for all commands to this queue
        foreach (var command in _commands)
        {
            command.Context = this;
        }

        // Queue all commands and get their tasks
        var commandTasks = _commandQueue.QueueCommandsAsync(_commands);

        // Register cancellation callback to stop the queue if this command is cancelled
        using var registration = token.Register(() =>
        {
            _commandQueue.Stop();
            _commandQueue.Cancel();
        });

        // Wait for all commands to complete
        await Task.WhenAll(commandTasks);
    }

    public void Cancel()
    {
        _commandQueue.Cancel();
    }

    public void Stop()
    {
        _commandQueue.Stop();
    }

    public Task<bool> QueueCommandAsync(ICommand command)
    {
        return _commandQueue.QueueCommandAsync(command);
    }

    public List<Task<bool>> QueueCommandsAsync(IEnumerable<ICommand> commands)
    {
        return _commandQueue.QueueCommandsAsync(commands);
    }

    public override void Dispose()
    {
        // Stop and clean up the command queue
        _commandQueue?.Stop();
        
        // Dispose all commands in the list
        foreach (var command in _commands)
        {
            command?.Dispose();
        }
        
        base.Dispose();
    }
}



public class UnityEventCommand : Command
{
    [SerializeField] private UnityEvent _event;

    protected override Task UndoContent(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    protected override Task ExecutionContent(CancellationToken token)
    {
        _event?.Invoke();
        return Task.CompletedTask;
    }
}
