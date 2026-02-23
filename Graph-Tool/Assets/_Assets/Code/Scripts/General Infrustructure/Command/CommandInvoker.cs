using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

public class CommandInvoker : SerializedMonoBehaviour, ICommandQueue, IValueAsset<ICommandQueue>
{
    [OdinSerialize] private ICommandQueue _commandQueue = new CommandQueue();

    public Task<bool> QueueCommandAsync(ICommand command)
    {
        return _commandQueue.QueueCommandAsync(command);
    }

    public List<Task<bool>> QueueCommandsAsync(IEnumerable<ICommand> commands)
    {
        return _commandQueue.QueueCommandsAsync(commands);
    }
    


    public void InvokeCommandObject(Object collection)
    {
        if (collection is ICommand command)
        {
            _ = QueueCommandAsync(command);
        }
        else if (collection is IEnumerable<ICommand> commandList)
        {
            QueueCommandsAsync(commandList);
        }
        else
        {
            Debug.LogError("Provided collection is neither an ICommand nor an IEnumerable<ICommand>.");
        }
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
}