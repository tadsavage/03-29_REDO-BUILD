using System.Collections.Generic;

public class CommandHistory
{
    private readonly Stack<ICommand> _undo = new();
    private readonly Stack<ICommand> _redo = new();

    // For batching (drag delete, multi-place, etc.)
    private List<ICommand> _currentBatch = null;

    // ---------------------------------------------------------
    // BASIC PUSH (single command)
    // ---------------------------------------------------------
    public void Push(ICommand cmd)
    {
        // Execute immediately
        cmd.Execute();

        // Add to undo stack
        _undo.Push(cmd);

        // New action invalidates redo history
        _redo.Clear();
    }

    // ---------------------------------------------------------
    // BATCHING SUPPORT
    // ---------------------------------------------------------
    public void BeginBatch()
    {
        if (_currentBatch == null)
            _currentBatch = new List<ICommand>();
    }

    public void AddToBatch(ICommand cmd)
    {
        if (_currentBatch == null)
        {
            // No batch started → treat as normal push
            Push(cmd);
            return;
        }

        cmd.Execute();
        _currentBatch.Add(cmd);
    }

    public void EndBatch()
    {
        if (_currentBatch == null || _currentBatch.Count == 0)
        {
            _currentBatch = null;
            return;
        }

        // Wrap batch into a single command
        var batch = new CommandBatch(_currentBatch.ToArray());

        _undo.Push(batch);
        _redo.Clear();

        _currentBatch = null;
    }

    // ---------------------------------------------------------
    // UNDO / REDO
    // ---------------------------------------------------------
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
