using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

public class CompoundState : IState, IUpdatable, IFixedUpdatable
{
    // A collection-backed state that composes multiple IState instances into a single
    // logical state. When Enter() is called this compound state will call Enter() on
    // every child state and await them all using Task.WhenAll. Likewise Exit() will
    // call Exit() on every child and await completion.
    //
    // Notes / guarantees:
    // - Child Enter/Exit calls are initiated immediately and run concurrently; there
    //   is no ordering guarantee between children. If ordering or sequencing is
    //   required, wrap states in a sequencing state instead.
    // - Exceptions thrown by any child Enter/Exit will propagate to the caller of
    //   CompoundState.Enter/Exit (Task will be faulted). The caller should handle
    //   exceptions as appropriate for your game logic.
    // - The list is serialized with Odin ([OdinSerialize]) so it can be edited in the
    //   inspector. The field is private but mutable to allow runtime changes and to
    //   avoid Unity serialization pitfalls with readonly collections.
    [OdinSerialize] private List<IState> _states;

    public CompoundState(IEnumerable<IState> states)
    {
        _states = states.ToList();
    }

    public async Task Enter()
    {
        List<Task> enterTasks = new List<Task>();
        foreach (var state in _states)
        {
            enterTasks.Add(state.Enter());
        }
        await Task.WhenAll(enterTasks);
    }

    public async Task Exit()
    {
        List<Task> exitTasks = new List<Task>();
        foreach (var state in _states)
        {
            exitTasks.Add(state.Exit());
        }
        await Task.WhenAll(exitTasks);
    }

    public virtual void Update()
    {
        foreach (var state in _states)
        {
            if (state is IUpdatable updatable)
            {
                updatable.Update();
            }
        }
    }

    public virtual void FixedUpdate()
    {
        foreach (var state in _states)
        {
            if (state is IFixedUpdatable fixedUpdatable)
            {
                fixedUpdatable.FixedUpdate();
            }
        }
    }
}
public class CompoundState<T> : IState<T>, IUpdatable, IFixedUpdatable where T : class
{
    // Generic compound state variant. Behaves like CompoundState, but also supports
    // a typed Context which is injected into each child IState<T> before Enter()
    // is invoked.
    //
    // Additional notes:
    // - The Context property is shown in the inspector (ReadOnly) for debugging via
    //   [ShowInInspector] but should be set by the owning state machine at runtime
    //   before calling Enter().
    // - Before calling Enter() we assign the Context to each child state so they can
    //   access shared data. This is done per-call so children always receive the
    //   latest context value.
    // - As with the non-generic compound, child Enter/Exit calls are started in
    //   parallel and awaited together. Exceptions from children will fault the
    //   returned Task.
    [OdinSerialize] private List<IState<T>> _states;

    public CompoundState(IEnumerable<IState<T>> states)
    {
        _states = states.ToList();
    }

    [ShowInInspector, ReadOnly]
    public T Context { get; set; }

    public async Task Enter()
    {
        List<Task> enterTasks = new List<Task>();
        foreach (var state in _states)
        {
            // Ensure each child receives the current context before starting.
            state.Context = Context;
            enterTasks.Add(state.Enter());
        }

        await Task.WhenAll(enterTasks);
    }

    public async Task Exit()
    {
        List<Task> exitTasks = new List<Task>();
        foreach (var state in _states)
        {
            exitTasks.Add(state.Exit());
        }

        await Task.WhenAll(exitTasks);
    }

    public void Update()
    {
        foreach (var state in _states)
        {
            if (state is IUpdatable updatable)
            {
                updatable.Update();
            }
        }
    }

    public void FixedUpdate()
    {
        foreach (var state in _states)
        {
            if (state is IFixedUpdatable fixedUpdatable)
            {
                fixedUpdatable.FixedUpdate();
            }
        }
    }
}