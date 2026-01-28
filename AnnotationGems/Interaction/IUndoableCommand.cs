namespace AnnotationGems.Interaction;

public interface IUndoableCommand
{
    string Name { get; }
    void Do();
    void Undo();
}
