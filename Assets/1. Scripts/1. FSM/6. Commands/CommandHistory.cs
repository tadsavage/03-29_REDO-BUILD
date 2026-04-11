using System.Collections.Generic;

public class CommandHistory
{
    private readonly Stack<ICommand> _undo = new();
    private readonly Stack<ICommand> _redo = new();

    public void Push(ICommand cmd)
    {
        // Execute immediately
        cmd.Execute();

        // Add to undo stack
        _undo.Push(cmd);

        // Clear redo stack (new action invalidates redo history)
        _redo.Clear();
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        ICommand cmd = _undo.Pop();
        cmd.Undo();
        _redo.Push(cmd);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        ICommand cmd = _redo.Pop();
        cmd.Execute();
        _undo.Push(cmd);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
