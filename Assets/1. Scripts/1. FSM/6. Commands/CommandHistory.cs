using System.Collections.Generic;

public class CommandHistory
{
    private readonly Stack<ICommand> _undo = new();
    private readonly Stack<ICommand> _redo = new();

    public void Push(ICommand cmd)
    {
        // Add to undo stack
    }

    public void Undo()
    {
        // Pop and undo
    }

    public void Redo()
    {
        // Pop and redo
    }
}
