using Sirenix.OdinInspector;
using UnityEngine;


[CreateAssetMenu (fileName = "EventBus", menuName = "ScriptableObjects/Event/EventBus", order = 1)]
public class EventBusSO : GuidScriptableObject, IEvent, IValueAsset<IEvent>
{
    [ShowInInspector] private IEvent _eventReference = new EventBase();
    public void Raise()
    {
        _eventReference.Raise();
    }

    public void RegisterListener(IListener listener)
    {
        _eventReference.RegisterListener(listener);
    }

    public void DeregisterListener(IListener listener)
    {
        _eventReference.DeregisterListener(listener);
    }

    public IEvent Value
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