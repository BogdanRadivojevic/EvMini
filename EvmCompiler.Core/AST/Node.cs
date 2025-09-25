namespace EvmCompiler.Core.AST;

public abstract class Node
{
    
    public abstract void Accept(IAstVisitor visitor);
    public abstract override string ToString();
}