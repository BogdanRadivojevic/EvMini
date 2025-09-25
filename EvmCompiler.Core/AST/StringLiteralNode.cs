namespace EvmCompiler.Core.AST;

public class StringLiteralNode : Node
{
    public string Value { get; }
    
    public StringLiteralNode(string value) => Value = value;
    
    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);
    
    public override string ToString()
        => $"\"{Value}\"";
}