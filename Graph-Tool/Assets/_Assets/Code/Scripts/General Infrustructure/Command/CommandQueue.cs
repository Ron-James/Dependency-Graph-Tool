using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sirenix.OdinInspector;

public interface ICommandInvoker : ICancelable, IStoppable
{
}
public interface ICancelable
{
    void Cancel();
}
public interface IStoppable
{
    void Stop();
}
public interface ICommandQueue : ICommandInvoker
{
    Task<bool> QueueCommandAsync(ICommand command);
    List<Task<bool>> QueueCommandsAsync(IEnumerable<ICommand> commands);
}

/// <summary>
/// Queues and executes ICommand instances sequentially, with cancellation, timeouts, and status reporting.
/// </summary>
public sealed class CommandQueue : ICommandQueue
{
    private Queue<(ICommand command, TaskCompletionSource<bool> tcs)> _queue =
        new Queue<(ICommand, TaskCompletionSource<bool>)>();

    private Stack<ICommand> _completedCommands = new Stack<ICommand>();
    private List<ICommand> _completedHistory = new List<ICommand>();
    private CancellationTokenSource _cancellationTokenSource;
    private Task _runnerTask;
    private bool _isRunning;

    private bool _shouldStop;

    public bool isRunning => _isRunning;
    public bool isIdle => !_isRunning || _queue.Count == 0;
    public int count => _queue.Count;
    public int completedCount => _completedCommands.Count;
    public List<ICommand> completed => new List<ICommand>(_completedHistory);

    [ShowInInspector]
    public List<ICommand> currentQueue
    {
        get
        {
            if(_queue == null) return new List<ICommand>();
            var list = new List<ICommand>();
            foreach (var (command, _) in _queue)
                list.Add(command);
            return list;
        }
    }
    
    public List<ICommand> completedCommands
    {
        get { return new List<ICommand>(_completedCommands); }
    }
    
    public List<ICommand> completedHistory
    {
        get { return new List<ICommand>(_completedHistory); }
    }

    /// <summary>
    /// Adds a command to the queue.
    /// </summary>
    public async Task<bool> QueueCommandAsync(ICommand command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        var tcs = new TaskCompletionSource<bool>();
        bool shouldStart = false;
        try
        {
            _queue.Enqueue((command, tcs));
            if (!_isRunning)
                shouldStart = true;
        }
        catch (OperationCanceledException)
        {
            tcs.SetResult(false);
        }

        if (shouldStart)
        {
            _ = StartAsync();
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Adds multiple commands to the queue.
    /// </summary>
    public List<Task<bool>> QueueCommandsAsync(IEnumerable<ICommand> commands)
    {
        if (commands == null) throw new ArgumentNullException(nameof(commands));
        var tasks = new List<Task<bool>>();
        bool shouldStart = false;
        foreach (var command in commands)
        {
            if (command == null) continue;
            var tcs = new TaskCompletionSource<bool>();
            _queue.Enqueue((command, tcs));
            tasks.Add(tcs.Task);
        }

        if (!_isRunning && _queue.Count > 0)
            shouldStart = true;
        if (shouldStart)
        {
            _ = StartAsync();
        }

        return tasks;
    }

    /// <summary>
    /// Starts processing the queue. Returns when all commands are processed or cancellation occurs.
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning) return;
        _shouldStop = false;
        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _runnerTask = RunQueue(_cancellationTokenSource.Token);
        await _runnerTask;
    }

    public ICommand CurrentCommand { get; set; }

    private async Task ExecuteCommand(ICommand command)
    {
        command.Context = this;
        await command.Execute(_cancellationTokenSource.Token);
    }

    private async Task RunQueue(CancellationToken token)
    {
        while (_queue.Count > 0)
        {
            // Check if we should stop at the very beginning of each command
            if (_shouldStop)
            {
                // Cancel all remaining commands in queue
                while (_queue.Count > 0)
                {
                    var (_, tcs) = _queue.Dequeue();
                    tcs.SetCanceled();
                }
                break;
            }

            var (command, taskSource) = _queue.Dequeue();
            CurrentCommand = command;

            try
            {
                command.Context = this;
                await ExecuteCommand(command);
                _completedCommands.Push(command);
                _completedHistory.Add(command);
                taskSource.SetResult(true);
            }
            catch (OperationCanceledException)
            {
                // Command was cancelled, skip to next command
                _completedCommands.Push(command);
                _completedHistory.Add(command);
                taskSource.SetResult(false);
                // Continue to next command in the queue (train behavior)
            }
            catch (Exception ex)
            {
                _completedCommands.Push(command);
                _completedHistory.Add(command);
                taskSource.SetException(ex);
            }
            finally
            {
                CurrentCommand = null;
            }
        }

        _isRunning = false;
    }

    public void Cancel()
    {
        if (!_isRunning)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log("CommandQueue: Cancel called but queue is not running.");
#endif
            return;
        }

        // Cancel current command but continue with next ones (train behavior)
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        // Create new token source for subsequent commands
        _cancellationTokenSource = new CancellationTokenSource();

        // Set flag to skip current command and move to next
        _shouldStop = false; // Ensure we don't stop the entire queue
    }
    

    /// <summary>
    /// Stops the queue gracefully after current command completes.
    /// </summary>
    public void Stop()
    {
        _shouldStop = true;
    }
    
}