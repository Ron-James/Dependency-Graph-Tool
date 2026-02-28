using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

public class CommandListExecutor : SerializedMonoBehaviour, ICommandQueue, IValueAsset<ICommandQueue>, IInit, IEnumerable<ICommand>
{
    [OdinSerialize] private ICommandQueue _commandQueue = new CommandQueue();
    [OdinSerialize] private List<ICommand> _commands = new List<ICommand>();
    [SerializeField] private bool initOnStart = true;

    public IReadOnlyList<ICommand> Commands => _commands;

    private void Start()
    {
        if (initOnStart)
        {
            _ = Init();
        }
    }

    public async Task Init()
    {
        foreach (var command in _commands)
        {
            if (command != null)
            {
                await command.Init();
            }
        }
    }

    public void QueueAllCommands()
    {
        _commandQueue.QueueCommandsAsync(_commands);
    }


    public IEnumerator<ICommand> GetEnumerator()
    {
        return _commands.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public Task<bool> QueueCommandAsync(ICommand command)
    {
        return _commandQueue.QueueCommandAsync(command);
    }

    public List<Task<bool>> QueueCommandsAsync(IEnumerable<ICommand> commands)
    {
        return _commandQueue.QueueCommandsAsync(commands);
    }

    public ICommandQueue Value
    {
        get => _commandQueue;
        set
        {
            if (value != null)
            {
                _commandQueue = value;
            }
        }
    }

    public void Cancel()
    {
        _commandQueue.Cancel();
    }

    public void Stop()
    {
        _commandQueue.Stop();
    }
    
    public void Dispose()
    {
        foreach (var command in _commands)
        {
            command.Dispose();
        }
    }
}


