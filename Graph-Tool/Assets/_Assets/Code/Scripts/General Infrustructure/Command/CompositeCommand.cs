using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sirenix.Serialization;

public class CompositeCommand : Command
{
    [OdinSerialize] private List<ICommand> _subCommands = new List<ICommand>();

    public override async Task Init()
    {
        foreach (var command in _subCommands)
        {
            await command.Init();
        }
        await base.Init();
    }

    protected override async Task ExecutionContent(CancellationToken token)
    {
        // Set context for all sub-commands
        foreach (var command in _subCommands)
        {
            command.Context = Context;
        }

        // Run all commands in parallel with parent token
        var tasks = _subCommands.Select(cmd => cmd.Execute(token)).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested - handled internally
        }
        
    }

    protected override async Task UndoContent(CancellationToken token)
    {
        var tasks = _subCommands.Select(cmd => cmd.Undo(token)).ToList();
        await Task.WhenAll(tasks);
    }

    public override void Dispose()
    {
        foreach (var command in _subCommands)
        {
            command?.Dispose();
        }
        base.Dispose();
    }
}