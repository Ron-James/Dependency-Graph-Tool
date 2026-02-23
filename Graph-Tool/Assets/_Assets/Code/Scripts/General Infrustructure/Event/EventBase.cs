using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Events;

public class EventBase : IEvent
{
    [OdinSerialize] protected List<IListener> _listeners = new List<IListener>();

    public void RegisterListener(IListener listener)
    {
        if (!_listeners.Contains(listener))
        {
            _listeners.Add(listener);
        }
    }

    public void DeregisterListener(IListener listener)
    {
        if (_listeners.Contains(listener))
        {
            _listeners.Remove(listener);
        }
    }

    public virtual void Raise()
    {
        for (int i = _listeners.Count - 1; i >= 0; i--)
        {
            try
            {
                _listeners[i].OnRaised();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error raising event to listener {_listeners[i].Name}: {e} - stacktrace: {e.StackTrace}");
            }
            
        }
    }
}

public class EventBase<T> : EventBase, IEvent<T>
{
    public void Raise(T arg)
    {
        for (int i = _listeners.Count - 1; i >= 0; i--)
        {
            try
            {
                IListener listener = _listeners[i];
                if (listener is IListener<T> typedListener)
                {
                    typedListener.OnRaised(arg);
                }
                else
                {
                    listener.OnRaised();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error raising event to listener {_listeners[i].Name}: {e} - stacktrace: {e.StackTrace}");
            }
        }
    }
}