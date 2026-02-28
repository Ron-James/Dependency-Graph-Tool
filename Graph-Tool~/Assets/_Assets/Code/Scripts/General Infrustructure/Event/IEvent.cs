using System;
using UnityEngine;

public interface IRaise
{
  void Raise();   
}

public interface IRaise<in T>
{
    void Raise(T arg);
}
public interface IEvent : IRaise
{
    void RegisterListener(IListener listener);
    void DeregisterListener(IListener listener);
}
public interface IListener : INamedEntry
{
    void OnRaised();
}

public interface IEvent<in T> : IEvent, IRaise<T>
{
    
}


public interface IListener<in T> : IListener
{
    void OnRaised(T arg);
}


