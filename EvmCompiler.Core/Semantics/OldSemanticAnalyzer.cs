using System.Numerics;
using EvmCompiler.Core.AST;

namespace EvmCompiler.Core.Semantics;

[Obsolete("BytecodeGenerator is deprecated, use SemanticAnalyzer instead.")]
public class OldSemanticAnalyzer
{
    // symbol table: identifier -> (offset у меморији, TypeInfo)
    private readonly Dictionary<string, (int Offset, TypeInfo Type)> _symbols
        = new();

    private int _nextOffset = 0;

    // Exposes so that codegen can see where each variable is and what its type is.
    public IReadOnlyDictionary<string, (int Offset, TypeInfo Type)> Symbols
        => _symbols;

    /// <summary>
    /// Entry point for semantic analysis.
    /// </summary>
    public void Analyze(ProgramNode program)
    {
        foreach (var stmt in program.Body)
            AnalyzeStatement(stmt);
    }

    private void AnalyzeStatement(Node stmt)
    {
        switch (stmt)
        {
            case VariableDeclarationNode vd:
                AnalyzeVariableDeclaration(vd);
                break;

            case AssignmentNode an:
                AnalyzeAssignment(an);
                break;

            case IfStatementNode ifs:
                AnalyzeIf(ifs);
                break;

            case WhileStatementNode ws:
                AnalyzeWhile(ws);
                break;

            default:
                AnalyzeExpression(stmt);
                break;
        }
    }

    private void AnalyzeVariableDeclaration(VariableDeclarationNode vd)
    {
        // First check initializer
        AnalyzeExpression(vd.Initializer);

        // Declaration: convert to TypeInfo
        var typeInfo = new PrimitiveTypeInfo(vd.Type);
        Declare(vd.Identifier, typeInfo);
    }

    private void AnalyzeAssignment(AssignmentNode an)
    {
        if (!_symbols.TryGetValue(an.Identifier, out var sym))
            throw new Exception($"Variable '{an.Identifier}' is not declared.");

        AnalyzeExpression(an.Expression);
    }

    private void AnalyzeIf(IfStatementNode node)
    {
        // Condition must be bool
        AnalyzeExpression(node.Condition);

        // Then-branch
        foreach (var s in node.ThenBranch)
            AnalyzeStatement(s);

        // Else-branch
        if (node.ElseBranch != null)
            foreach (var s in node.ElseBranch)
                AnalyzeStatement(s);
    }

    private void AnalyzeWhile(WhileStatementNode node)
    {
        // Condition
        AnalyzeExpression(node.Condition);
        // Body
        foreach (var s in node.Body)
            AnalyzeStatement(s);
    }

    private void Declare(string name, TypeInfo type)
    {
        if (_symbols.ContainsKey(name))
            throw new Exception($"Variable '{name}' is already declared.");

        // counts how many bytes the variable takes
        _symbols[name] = (_nextOffset, type);
        _nextOffset += type.BitWidth;
    }

    private void AnalyzeExpression(Node node)
    {
        switch (node)
        {
            case NumberLiteralNode lit:
                
                // check if value fits in BitWidth
                BigInteger max = (BigInteger.One << lit.BitWidth) - 1;
                if (lit.Value < 0 || lit.Value > max)
                    throw new Exception($"Literal {lit.Value} does not fit in {lit.BitWidth}-bit.");
                break;

            case StringLiteralNode str:
                // max length 32 bytes
                if (str.Value.Length > 32)
                    throw new Exception($"String literal \"{str.Value}\" is longer than 32 bytes.");
                break;

            case BooleanLiteralNode _:
                // always 1 byte
                break;

            case IdentifierNode id:
                if (!_symbols.ContainsKey(id.Name))
                    throw new Exception($"Variable '{id.Name}' is not declared.");
                break;

            case BinaryExpressionNode bin:
                AnalyzeExpression(bin.Left);
                AnalyzeExpression(bin.Right);
                break;

            default:
                throw new NotSupportedException($"Unknown AST node for semantic analysis: {node.GetType().Name}");
        }
    }
}