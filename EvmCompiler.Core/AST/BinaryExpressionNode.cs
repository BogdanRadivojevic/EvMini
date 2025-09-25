namespace EvmCompiler.Core.AST;

public class BinaryExpressionNode : Node
{
    public string Operator { get; }
    public Node Left { get; }
    public Node Right { get; }

    public BinaryExpressionNode(string op, Node left, Node right)
    {
        Operator = op;
        Left = left;
        Right = right;
    }

    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);
    
    public override string ToString() => $"({Left} {Operator} {Right})";
}