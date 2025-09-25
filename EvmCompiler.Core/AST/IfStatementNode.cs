namespace EvmCompiler.Core.AST;

public class IfStatementNode : Node
{
    public Node Condition  { get; }
    public List<Node> ThenBranch  { get; }
    public List<Node>? ElseBranch { get; }

    public IfStatementNode(Node condition, List<Node> thenBranch, List<Node>? elseBranch = null)
    {
        Condition  = condition;
        ThenBranch = thenBranch;
        ElseBranch = elseBranch;
    }
    
    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);

    public override string ToString()
    {
        // maps every Node to its .ToString()
        var thenText = string.Join("; ", ThenBranch.Select(n => n.ToString()));
        var s = $"if ({Condition}) {{ {thenText} }}";

        if (ElseBranch != null && ElseBranch.Count > 0)
        {
            var elseText = string.Join("; ", ElseBranch.Select(n => n.ToString()));
            s += $" else {{ {elseText} }}";
        }

        return s;
    }
}