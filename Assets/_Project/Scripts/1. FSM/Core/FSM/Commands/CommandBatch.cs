using UnityEngine;

public class CommandBatch : ICommand, IWallInstanceRelocatable
{
    private readonly ICommand[] _commands;

    public CommandBatch(ICommand[] commands)
    {
        _commands = commands;
    }

    public void Execute()
    {
        foreach (var cmd in _commands)
            cmd.Execute();
    }

    public void Undo()
    {
        // Undo in reverse order
        for (int i = _commands.Length - 1; i >= 0; i--)
            _commands[i].Undo();
    }

    public void Redo()
    {
        foreach (var cmd in _commands)
            cmd.Redo();
    }

    public void RelocateWallInstance(GameObject oldInstance, GameObject newInstance)
    {
        foreach (var cmd in _commands)
            if (cmd is IWallInstanceRelocatable relocatable)
                relocatable.RelocateWallInstance(oldInstance, newInstance);
    }
}
