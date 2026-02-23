using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.VisualScripting;
using UnityEngine;

public class CommandCollectionComponent : SerializedMonoBehaviour, IRuntimeSet<ICommand>, IInit

{
    [OdinSerialize] private List<ICommand> _commands = new List<ICommand>();
    [SerializeField] private bool initSelf = true;



    private void Start()
    {
        if (initSelf)
        {
            _ = Init();
        }
    }


    private void OnDestroy()
    {
        if (initSelf)
        {
            Dispose();
        }
    }

    public IEnumerator<ICommand> GetEnumerator()
    {
        return _commands.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        foreach (var item in _commands)
        {
           item.Dispose();
        }
    }

    public async Task Init()
    {
        foreach (var item in _commands)
        {
            item.Init();
        }
    }


    public void Add(ICommand item)
    {
        _commands.Add(item);
    }

    public void Remove(ICommand item)
    {
        _commands.Remove(item);
    }

    public void Add(object item)
    {
        if (item is ICommand command)
        {
            Add(command);
        }
        else if(item is IValueAsset<ICommand> commandAsset)
        {
            Add(commandAsset.Value);
        }
        
        else
        {
            throw new ArgumentException($"Item must be of type {typeof(ICommand).Name}", nameof(item));
        }
    }

    public void Remove(object item)
    {
        if (item is ICommand command)
        {
            Remove(command);
        }
        else if(item is IValueAsset<ICommand> commandAsset)
        {
            Remove(commandAsset.Value);
        }
        else
        {
            throw new ArgumentException($"Item must be of type {typeof(ICommand).Name}", nameof(item));
        }
    }

    public void Clear()
    {
        _commands.Clear();
    }
}

