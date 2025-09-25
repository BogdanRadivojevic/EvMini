namespace EvmCompiler.Core.AST;

/// <summary>
/// Node which represents:
/// while (Condition) { Body }
/// </summary>
public class WhileStatementNode : Node
{
    public Node Condition { get; }
    public List<Node> Body { get; }

    public WhileStatementNode(Node condition, List<Node> body)
    {
        Condition = condition;
        Body = body;
    }

    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);

    public override string ToString()
    {
        var bodyText = string.Join("; ", Body.Select(n => n.ToString()));
        return $"while ({Condition}) {{ {bodyText} }}";
    }
}