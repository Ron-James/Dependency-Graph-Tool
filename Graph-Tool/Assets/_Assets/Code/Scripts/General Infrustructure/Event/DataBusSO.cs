using System;
using Sirenix.OdinInspector;

public class DataBusSO<T> : GuidScriptableObject, IValueAsset<T>, IEvent<T>, IValueAsset<IEvent<T>>
{
    [ShowInInspector] private IEvent<T> _eventReference = new EventBase<T>();
    [ShowInInspector, ReadOnly] private T _value;

    public void Raise(T arg)
    {
        _eventReference ??= new EventBase<T>();
        _eventReference.Raise(arg);
        _value = arg;
    }

    public void RegisterListener(IListener listener)
    {
        _eventReference ??= new EventBase<T>();
        _eventReference.RegisterListener(listener);
    }

    public void DeregisterListener(IListener listener)
    {
        _eventReference ??= new EventBase<T>();
        _eventReference.DeregisterListener(listener);
    }

    public void Raise()
    {
        _eventReference ??= new EventBase<T>();
        _eventReference.Raise();
    }


    protected virtual void OnDisable()
    {
        _value = default;
    }

    public T Value
    {
        get
        {
            return _value;
        }
        set => Raise(value);
    }

    IEvent<T> IValueAsset<IEvent<T>>.Value
    {
        get
        {
            return _eventReference;
        }
        set
        {
            if (value != null)
            {
                _eventReference = value;
            }
        }
    }
}