namespace EvmCompiler.Core.AST;

public class ProgramNode : Node
{
    
    public List<Node> Body { get; } = new();
    
    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);
    
    public override string ToString() => string.Join(Environment.NewLine, Body);
}