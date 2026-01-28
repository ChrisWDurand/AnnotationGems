using System.Collections.Generic;

namespace AnnotationGems.Interaction;

public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Execute(IUndoableCommand cmd)
    {
        cmd.Do();
        _undo.Push(cmd);
        _redo.Clear();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var cmd = _undo.Pop();
        cmd.Undo();
        _redo.Push(cmd);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var cmd = _redo.Pop();
        cmd.Do();
        _undo.Push(cmd);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
