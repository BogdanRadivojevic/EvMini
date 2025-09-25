namespace EvmCompiler.Core.AST;

/// <summary>
///     Visitor interface.
/// </summary>
public interface IAstVisitor
{
    void Visit(ProgramNode node);
    void Visit(VariableDeclarationNode node);
    void Visit(NumberLiteralNode node);
    void Visit(StringLiteralNode node);
    void Visit(BooleanLiteralNode node);
    void Visit(IdentifierNode node);
    void Visit(BinaryExpressionNode node);
    void Visit(IfStatementNode node);
    void Visit(WhileStatementNode node);
    void Visit(AssignmentNode node);
    void Visit(ArrayLiteralNode node);
    void Visit(ArrayAccessNode node);
}