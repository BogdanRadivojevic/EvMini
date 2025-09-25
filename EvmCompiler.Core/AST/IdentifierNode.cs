namespace EvmCompiler.Core.AST;

public class IdentifierNode : Node
{
    public string Name { get; }

    public IdentifierNode(string name)
    {
        Name = name;
    }
    
    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);

    public override string ToString() => Name;
}