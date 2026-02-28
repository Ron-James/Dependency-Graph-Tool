using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

public abstract class ListenerBase : IListener
{
    [SerializeField] string _name = "Listener";
    public abstract void OnRaised();
    public string Name { get => _name; set => _name = value; }
}

