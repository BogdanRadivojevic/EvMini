namespace EvmCompiler.Core.AST;

public class ArrayLiteralNode : Node
{
    public IReadOnlyList<Node> Elements { get; }
    public ArrayLiteralNode(IReadOnlyList<Node> elements)
        => Elements = elements;
    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);
    public override string ToString()
        => $"[{string.Join(", ", Elements)}]";
}