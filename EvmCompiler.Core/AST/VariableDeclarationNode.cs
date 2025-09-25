namespace EvmCompiler.Core.AST;

public class VariableDeclarationNode: Node
{
    public PrimitiveType Type { get; }
    public string Identifier { get; }
    public Node   Initializer { get; }

    public VariableDeclarationNode(PrimitiveType type, string id, Node init)
    {
        Type       = type;
        Identifier = id;
        Initializer = init;
    }

    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);
    public override string ToString() 
        => $"let {Identifier}:{Type} = {Initializer}";
}