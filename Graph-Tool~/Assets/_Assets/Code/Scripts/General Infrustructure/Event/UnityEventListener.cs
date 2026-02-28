using UnityEngine;
using UnityEngine.Events;

public class UnityEventListener : ListenerBase
{
    [SerializeField] private UnityEvent _onRaised = new UnityEvent();
    public override void OnRaised()
    {
        _onRaised?.Invoke();
    }
}


public class TypedUnityEventListener<T> : UnityEventListener, IListener<T>
{
    [SerializeField] private UnityEvent<T> _onRaised = new UnityEvent<T>();

    public virtual void OnRaised(T arg)
    {
        _onRaised?.Invoke(arg);
    }
}