using System.Collections.Generic;
using UnityEngine;

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
        cmd.Redo();
        _undo.Push(cmd);
    }

    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    // ---------------------------------------------------------
    // WALL SHAPE-SWAP RELOCATION
    // ---------------------------------------------------------
    /// <summary>
    /// Called by WallConnectivityManager immediately after it destroys a wall-family GameObject
    /// and replaces it with a new one (Wall/Corner/T-Wall auto-swap). Walks every command still
    /// reachable from the undo stack, the redo stack, and any in-progress batch, and gives each
    /// one that implements IWallInstanceRelocatable a chance to swap its own cached reference —
    /// otherwise an EARLIER command's later Undo()/Redo() would silently no-op on a GameObject
    /// that's since been destroyed. See IWallInstanceRelocatable for the full story.
    /// </summary>
    public void RelocateWallInstance(GameObject oldInstance, GameObject newInstance)
    {
        if (oldInstance == null || newInstance == null || ReferenceEquals(oldInstance, newInstance))
            return;

        foreach (var cmd in _undo) RelocateOne(cmd, oldInstance, newInstance);
        foreach (var cmd in _redo) RelocateOne(cmd, oldInstance, newInstance);
        if (_currentBatch != null)
            foreach (var cmd in _currentBatch) RelocateOne(cmd, oldInstance, newInstance);
    }

    private static void RelocateOne(ICommand cmd, GameObject oldInstance, GameObject newInstance)
    {
        if (cmd is IWallInstanceRelocatable relocatable)
            relocatable.RelocateWallInstance(oldInstance, newInstance);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
