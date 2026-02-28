using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.Serialization;
using UnityEngine;

public class StateMachine : IStateMachine
{
    [OdinSerialize] protected List<IState> internalStates = new List<IState>();
    public Task SetState(IState newState)
    {
        if (newState == CurrentState)
        {
            // If already in the requested state, return a completed task immediately.
            return Task.CompletedTask;
        }

        if(CurrentState != null)
        {
            // Synchronously wait for Exit() to finish. This blocks the caller thread.
            CurrentState.Exit().Wait();
        }

        // Synchronously wait for Enter() on the new state.
        newState.Enter().Wait();
        CurrentState = newState;
        return Task.CompletedTask;
    }
    

    // The currently active state.
    public IState CurrentState { get; protected set; }
    public Task SetState(int index)
    {
        //check if index is valid
        if (index < 0 || index >= internalStates.Count)
        {
            Debug.LogError("Invalid state index: " + index);
            return Task.CompletedTask;
        }
        return SetState(internalStates[index]);
    }

    public void Update()
    {
        if(CurrentState is IUpdatable updatable)
        {
            updatable.Update();
        }
    }

    public void FixedUpdate()
    {
        if(CurrentState is IFixedUpdatable fixedUpdatable)
        {
            fixedUpdatable.FixedUpdate();
        }
    }

    public void Dispose()
    {
        foreach (var state in internalStates)
        {
            if(state is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public async Task Init()
    {
        foreach (var state in internalStates)
        {
            if(state is IInit init)
            {
                await init.Init();
            }
        }
    }
}

// Base implementation for a typed state machine. It handles the common pattern of
// exiting the current state, injecting context into the next state (if it requires
// it), and entering the new state.
// Note: This is a plain, testable class designed to run on Unity's main thread.
public abstract class StateMachine<T> : StateMachine, IStateMachine<T> where T : class
{
    [OdinSerialize] protected new List<IState<T>> internalStates = new List<IState<T>>();
    // The context object available to states. Implementations provide the concrete
    // context instance (for example, a reference to a controller or data object).
    public abstract T Context { get; }

    // Transition to a new state. If the new state equals the current one, nothing happens.
    // The method awaits Exit() on the old state and Enter() on the new state so both can
    // run asynchronous operations safely.
    public virtual async Task SetState(IState newState)
    {
        if (newState == CurrentState)
        {
            return; // No-op when the requested state is already active.
        }

        if(CurrentState != null)
        {
            // Give the currently-active state a chance to clean up.
            await CurrentState.Exit();
        }

        // If the incoming state expects a typed context, provide it before Enter().
        if(newState is IContextual<T> contextualState)
        {
            contextualState.Context = Context;
        }

        // Enter the new state and mark it as current after successful Enter().
        await newState.Enter();
        CurrentState = newState;
    }

    // The currently active state. Protected setter so subclasses can control assignment
    // while consumers can read it.
    public IState CurrentState { get; protected set; }
    public void Update()
    {
        if(CurrentState is IUpdatable updatable)
        {
            updatable.Update();
        }
    }

    public void FixedUpdate()
    {
        if(CurrentState is IFixedUpdatable fixedUpdatable)
        {
            fixedUpdatable.FixedUpdate();
        }
    }
}