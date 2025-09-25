namespace EvmCompiler.Core.AST;

public class ArrayAccessNode : Node
{
    public Node Array   { get; }
    public Node Index   { get; }
    public ArrayAccessNode(Node array, Node index)
    {
        Array = array;
        Index = index;
    }
    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);
    public override string ToString()
        => $"{Array}[{Index}]";
}