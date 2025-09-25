namespace EvmCompiler.Core.AST;

public class BooleanLiteralNode : Node
{
    
    public bool Value { get; }

    public BooleanLiteralNode(bool value) => Value = value;

    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);
    public override string ToString()
        => Value.ToString().ToLower();
}