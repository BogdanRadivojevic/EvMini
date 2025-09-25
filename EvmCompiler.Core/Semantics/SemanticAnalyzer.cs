using System.Numerics;
using EvmCompiler.Core.AST;

namespace EvmCompiler.Core.Semantics;

/// <summary>
///     Seminatic analyzer for EVM.
/// </summary>
public class SemanticAnalyzer
{
    // identifier -> (offset in , TypeInfo)
    private readonly Dictionary<string, (int Offset, TypeInfo Type)> _symbols
        = new();

    // offset next variable (in bytes)
    private int _nextOffset = 0;

    /// <summary>
    /// After analysis, contains all variables and arrays with their TypeInfo.
    /// </summary> 
    public IReadOnlyDictionary<string, (int Offset, TypeInfo Type)> Symbols
        => _symbols;

    /// <summary>
    /// Main entry point for semantic analysis.
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
            case IfStatementNode iff:
                AnalyzeIf(iff);
                break;
            case WhileStatementNode w:
                AnalyzeWhile(w);
                break;
            default:
                AnalyzeExpression(stmt);
                break;
        }
    }

    private void AnalyzeVariableDeclaration(VariableDeclarationNode vd)
    {
        // 1) Analising initializer
        AnalyzeExpression(vd.Initializer);

        // 2) Inferring type
        TypeInfo ti;
        if (vd.Initializer is ArrayLiteralNode arrLit)
        {
            if (arrLit.Elements.Count == 0)
                throw new Exception("Cannot infer the type of an empty array.");

            // Infering element type
            var elemType = InferTypeInfo(arrLit.Elements[0]);
            foreach (var e in arrLit.Elements)
            {
                AnalyzeExpression(e);
                var t2 = InferTypeInfo(e);
                if (t2.GetType() != elemType.GetType() ||
                    (t2 is PrimitiveTypeInfo p2 && ((PrimitiveTypeInfo)elemType).Type != p2.Type))
                {
                    throw new Exception("All elements of an array must be of the same type.");
                }
            }

            ti = new ArrayTypeInfo(elemType, arrLit.Elements.Count);
        }
        else
        {
            // basic primitive
            ti = new PrimitiveTypeInfo(vd.Type);
        }

        // 3) Register in symbol table
        if (_symbols.ContainsKey(vd.Identifier))
            throw new Exception($"Variable '{vd.Identifier}' is already declared.");

        _symbols[vd.Identifier] = (_nextOffset, ti);
        _nextOffset += ti.BitWidth;
    }

    private void AnalyzeAssignment(AssignmentNode an)
    {
        if (!_symbols.TryGetValue(an.Identifier, out var sym))
            throw new Exception($"Variable '{an.Identifier}' is not declared.");

        AnalyzeExpression(an.Expression);

        // Check if types are compatible
        var exprType = InferTypeInfo(an.Expression);
        if (sym.Type is PrimitiveTypeInfo pti && exprType is PrimitiveTypeInfo pti2)
        {
            if (pti.Type != pti2.Type)
                throw new Exception($"Type {pti2.Type} isn't compatible with {pti.Type}.");
        }
        else if (sym.Type is ArrayTypeInfo || exprType is ArrayTypeInfo)
        {
            throw new Exception("Isn't supported assignment of arrays.");
        }
    }

    private void AnalyzeIf(IfStatementNode iff)
    {
        AnalyzeExpression(iff.Condition);
        foreach (var s in iff.ThenBranch) AnalyzeStatement(s);
        if (iff.ElseBranch != null)
            foreach (var s in iff.ElseBranch)
                AnalyzeStatement(s);
    }

    private void AnalyzeWhile(WhileStatementNode w)
    {
        AnalyzeExpression(w.Condition);
        foreach (var s in w.Body) AnalyzeStatement(s);
    }

    
    /// <summary>
    ///     Infers the type of the node.
    /// </summary>
    /// <param name="node">Node</param>
    /// <exception cref="Exception"></exception>
    /// <exception cref="NotSupportedException"></exception>
    private void AnalyzeExpression(Node node)
    {
        switch (node)
        {
            case NumberLiteralNode lit:
            {
                BigInteger max = (BigInteger.One << lit.BitWidth) - 1;
                if (lit.Value < 0 || lit.Value > max)
                    throw new Exception($"Literal {lit.Value} does not fit in {lit.BitWidth}-bit.");
                break;
            }
            case StringLiteralNode str:
            {
                if (str.Value.Length > 32)
                    throw new Exception($"String literal \"{str.Value}\" longer than 32 bytes.");
                break;
            }
            case BooleanLiteralNode _:
                break;
            case IdentifierNode id:
                if (!_symbols.ContainsKey(id.Name))
                    throw new Exception($"Indentifier '{id.Name}' is not declared.");
                break;
            case BinaryExpressionNode bin:
                AnalyzeExpression(bin.Left);
                AnalyzeExpression(bin.Right);
                break;
            case ArrayLiteralNode arr:
                foreach (var e in arr.Elements)
                    AnalyzeExpression(e);
                break;
            case ArrayAccessNode acc:
                AnalyzeExpression(acc.Array);
                AnalyzeExpression(acc.Index);
                var at = InferTypeInfo(acc.Array);
                if (!(at is ArrayTypeInfo))
                    throw new Exception("Operator [] can only be applied to arrays.");
                break;
            default:
                throw new NotSupportedException(
                    $"Unknown AST node for semantic analysis: {node.GetType().Name}");
        }
    }

    /// <summary>
    ///     Infers the type of the node.
    /// </summary> 
    private TypeInfo InferTypeInfo(Node node)
    {
        switch (node)
        {
            case NumberLiteralNode lit:
                return new PrimitiveTypeInfo((PrimitiveType)lit.BitWidth);
            case BooleanLiteralNode _:
                return new PrimitiveTypeInfo(PrimitiveType.Bool);
            case StringLiteralNode _:
                return new PrimitiveTypeInfo(PrimitiveType.UInt256);
            case IdentifierNode id:
                return _symbols[id.Name].Type;
            case ArrayLiteralNode arr:
                var elemType = InferTypeInfo(arr.Elements[0]);
                return new ArrayTypeInfo(elemType, arr.Elements.Count);
            case ArrayAccessNode acc:
                var arrayType = (ArrayTypeInfo)InferTypeInfo(acc.Array);
                return arrayType.ElementType;
            case BinaryExpressionNode bin:
                return InferTypeInfo(bin.Left);
            default:
                throw new Exception($"Can not infer type for {node.GetType().Name}");
        }
    }
}