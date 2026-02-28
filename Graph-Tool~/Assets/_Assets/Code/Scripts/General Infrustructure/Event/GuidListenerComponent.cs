using UnityEngine;
using UnityEngine.Events;

public class GuidListenerComponent : ListenerComponent<IGUIDObject>
{
    
}

public class GuidCastListener<T> : ListenerBase, IListener<IGUIDObject>
{
    [SerializeField] private UnityEvent<T> _listener;
    public void OnRaised(IGUIDObject arg)
    {
        if(arg is T casted)
        {
            _listener?.Invoke(casted);
        }
    }

    public override void OnRaised()
    {
        // Do nothing
    }
}

public class GuidCastFilter<T> : IFilter<IGUIDObject> where T : IGUIDObject
{
    public bool IsValid(IGUIDObject item)
    {
        return item is T;
    }

    public bool IsValid(object item)
    {
        if (item is IGUIDObject guidObject)
        {
            return IsValid(guidObject);
        }
        return false;
    }
}