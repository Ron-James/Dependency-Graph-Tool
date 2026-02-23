using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

public class ListenerComponent : SerializedMonoBehaviour, IListener
{
    [OdinSerialize] private IEvent[] _events = Array.Empty<IEvent>();
    [OdinSerialize] private IListener[] listeners = Array.Empty<IListener>();
    private void OnEnable()
    {
        foreach (var evt in _events)
        {
            evt.RegisterListener(this);
        }
    }
    
    private void OnDisable()
    {
        foreach (var evt in _events)
        {
            evt.DeregisterListener(this);
        }
    }

    public void OnRaised()
    {
        foreach (var listener in listeners)
        {
            listener.OnRaised();
        }
    }

    public string Name { get => name; set => name = value; }
}


public class ListenerComponent<T> : SerializedMonoBehaviour, IListener<T>, IFilter<T>, IEvent<T>, IDataBinding
{
    [Title("Filters")]
    [OdinSerialize] private List<IFilter<T>> _filters = new List<IFilter<T>>(); 
    [Title("Value Asset Settings")] [SerializeField]
    private bool checkValueAssetsOnStart;
    [Title("Events to Observe")]
    [OdinSerialize] private IEvent<T>[] _events = Array.Empty<IEvent<T>>();
    [OdinSerialize] private List<IListener<T>> typedListeners = new List<IListener<T>>();
    private void OnEnable()
    {
        foreach (var evt in _events)
        {
            evt.RegisterListener(this);
        }
    }
    
    private void OnDisable()
    {
        foreach (var evt in _events)
        {
            evt.DeregisterListener(this);
        }
    }

    public void OnRaised(T arg)
    {
        if(!IsValid(arg)) return;
        foreach (var listener in typedListeners)
        {
            listener.OnRaised(arg);
        }
    }
    

    public void OnRaised()
    {
        foreach (var item in typedListeners)
        {
            item.OnRaised();
        }
    }

    private void Start()
    {
        if (checkValueAssetsOnStart)
        {
            foreach (var item in _events)
            {
                if (item is IValueAsset<T> valueAsset && valueAsset.Value != null)
                {
                    OnRaised(valueAsset.Value);
                }
            }
        }
    }

    public bool IsValid(T item)
    {
        foreach (var filter in _filters)
        {
            if (!filter.IsValid(item)) return false;
        }
        return true;
    }

    public void Raise()
    {
        OnRaised();
    }

    public void RegisterListener(IListener listener)
    {
        if(listener is not IListener<T> typedListener) return;
        if (!typedListeners.Contains(typedListener))
        {
            typedListeners.Add(typedListener);
        }
    }

    public void DeregisterListener(IListener listener)
    {
        if(listener is not IListener<T> listener1) return;
        if (typedListeners.Contains(listener1))
        {
            typedListeners.Remove(listener1);
        }
    }

    public void Raise(T arg)
    {
        OnRaised(arg);
    }

    public T BoundData
    {
        set
        {
            IFilter<T> filter = new EqualityFilter<T>(value);
            _filters.Add(filter);
        }
    }

    public bool IsValid(object item)
    {
        if (item is T tItem)
        {
            return IsValid(tItem);
        }
        return false;
    }

    public void BindData(object data, int index)
    {
        if (data is T tData)
        {
            BoundData = tData;
        }
    }

    public string Name { get => name; set => name = value; }
}
