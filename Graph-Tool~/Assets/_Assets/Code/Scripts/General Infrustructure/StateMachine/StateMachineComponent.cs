using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using TMPro;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Serialization;


// Core interface representing a state in a state machine.
// Implementations should perform their work in Enter() and clean up in Exit().
public interface IState 
{
    // Called when the state becomes active. Implement async work here as needed.
    Task Enter();

    // Called when the state is leaving. Use this to stop or cleanup resources.
    Task Exit();
}


// A small, lean interface that exposes state-machine operations needed by consumers.
// Kept minimal on purpose so it can be used by components or services that drive states.
public interface IStateMachine : IUpdatable, IFixedUpdatable, IInit
{
    // Request a transition to the provided state. Implementation may run the state's
    // Enter/Exit logic synchronously or asynchronously.
    Task SetState(IState newState);

    // The currently-active state (or null if none).
    IState CurrentState { get; }
    public Task SetState(int index);
}





// Generic variant combining IState with contextual access. Useful when a state needs
// strongly-typed context injected before Enter() is called.
public interface IState<T> : IState, IContextual<T> where T : class
{
}


// Generic state-machine interface that also exposes Context for typed state machines.
public interface IStateMachine<T> : IStateMachine, IContext<T> where T : class
{
}





// Simple, synchronous state machine implementation for quick use-cases. This class
// synchronously waits on Enter/Exit tasks using .Wait() — this is acceptable only
// when you are sure calls happen on the main thread and tasks are short.
// Prefer the async StateMachine<T> above for most scenarios.

// A Unity component wrapper that exposes a state list to the inspector (via Odin)
// and delegates SetState calls to an injected IStateMachine instance.
public class StateMachineComponent : SerializedMonoBehaviour, IStateMachine, IValueAsset<IStateMachine>
{
    // Serialized list of states shown in the Odin inspector. Not readonly so Unity/Odin
    // can serialize and mutate it in the editor and at runtime if needed.

    // The backing state-machine instance which does the actual transitions. This can be
    // assigned in inspector or wired at runtime by bootstrap code.
    [OdinSerialize] private IStateMachine _stateMachine;

    // Forwards the SetState request to the underlying state machine.
    public Task SetState(IState newState)
    {
        return _stateMachine.SetState(newState);
    }

    // Expose the current state from the backing state machine. Consumer reads only.
    public IState CurrentState => _stateMachine.CurrentState;
    public Task SetState(int index)
    {
        _stateMachine.SetState(index);
        return Task.CompletedTask;
    }

    // Implements IValueAsset to allow other code to get the held IStateMachine instance.
    public IStateMachine Value
    {
        get
        {
            return _stateMachine;
        }
        set
        {
            _stateMachine = value;
        }
    }


    public void Update()
    {
        if(CurrentState is null) return;
        if(CurrentState is IUpdatable updatable)
        {
            updatable.Update();
        }
    }
    

    public void FixedUpdate()
    {
        if(CurrentState is null) return;
        if (CurrentState is IFixedUpdatable fixedUpdatable)
        {
            fixedUpdatable.FixedUpdate();
        }
    }

    public void Dispose()
    {
        _stateMachine.Dispose();
    }

    public Task Init()
    {
        return _stateMachine.Init();
    }
}




