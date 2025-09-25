namespace EvmCompiler.Core.AST;

public class AssignmentNode : Node
{
    public string Identifier { get; }
    public Node Expression { get; }

    public AssignmentNode(string identifier, Node expression)
    {
        Identifier = identifier;
        Expression = expression;
    }

    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);

    public override string ToString()
        => $"{Identifier} = {Expression}";
}