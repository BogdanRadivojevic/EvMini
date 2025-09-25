using System.Numerics;
using System.Text;
using EvmCompiler.Core.AST;

namespace EvmCompiler.Core.CodeGen;

[Obsolete("BytecodeGenerator is deprecated, use CodeGenVisitor instead.")]
public class BytecodeGenerator
{
    private readonly Dictionary<string, (int Offset, PrimitiveType Type)> _symbols;
    private int _labelCounter = 0;

    public BytecodeGenerator(IReadOnlyDictionary<string, (int Offset, PrimitiveType Type)> symbols)
    {
        // Copy to local variable for easy access
        _symbols = new Dictionary<string, (int, PrimitiveType)>(symbols);
    }

    /// <summary>
    /// Generates list of EVM instructions for given program.
    /// </summary>
    public List<string> Generate(ProgramNode program)
    {
        var instr = new List<string>();

        foreach (var node in program.Body)
        {
            if (node is IfStatementNode ifStmt)
            {
                GenerateIf(ifStmt, instr);
            }
            else if (node is VariableDeclarationNode vd)
            {
                // 1) Generate code for initializer
                GenerateNode(vd.Initializer, instr);
                
                // 2) Pushing memory address of variable
                var sym = _symbols[vd.Identifier];
                int offset = sym.Offset;
                int offsetBytes = ComputeByteCount(offset);
                string offsetHex = offset.ToString("X").PadLeft(offsetBytes * 2, '0');
                instr.Add($"PUSH{offsetBytes} 0x{offsetHex}");

                // 3) Write to memory (MSTORE or MSTORE8)
                if (sym.Type == PrimitiveType.UInt8)
                    instr.Add("MSTORE8");
                else
                    instr.Add("MSTORE");
            }
            else
            {
                // for exprassion as statement, just generate and ignore result
                GenerateNode(node, instr);
            }
        }

        return instr;
    }

    private void GenerateNode(Node node, List<string> instr)
    {
        switch (node)
        {
            case NumberLiteralNode lit:
                EmitPushLiteral(lit, instr);
                break;

            case StringLiteralNode str:
                GenerateStringLiteral(str, instr);
                break;

            case BooleanLiteralNode bl:
                // true -> 1, false -> 0, always 1 byte
                instr.Add(bl.Value ? "PUSH1 0x01" : "PUSH1 0x00");
                break;


            case IdentifierNode id:
            {
                var sym = _symbols[id.Name];
                int offset = sym.Offset;
                int offsetBytes = ComputeByteCount(offset);
                string offHex = offset.ToString("X").PadLeft(offsetBytes * 2, '0');
                instr.Add($"PUSH{offsetBytes} 0x{offHex}");
                // For read, use MLOAD (loads 32 bytes)
                instr.Add("MLOAD");
                break;
            }

            case BinaryExpressionNode bin:
                // generate first left, then right operand
                GenerateNode(bin.Left, instr);
                GenerateNode(bin.Right, instr);
                switch (bin.Operator)
                {
                    // aritmethic operators
                    case "+": instr.Add("ADD"); break;
                    case "-": instr.Add("SUB"); break;
                    case "*": instr.Add("MUL"); break;
                    case "/": instr.Add("DIV"); break;

                    // logic operators
                    case "<": instr.Add("LT"); break;
                    case ">": instr.Add("GT"); break;
                    case "<=":
                        instr.Add("GT");
                        instr.Add("ISZERO");
                        break;
                    case ">=":
                        instr.Add("LT");
                        instr.Add("ISZERO");
                        break;
                    case "==": instr.Add("EQ"); break;
                    case "!=":
                        instr.Add("EQ");
                        instr.Add("ISZERO");
                        break;
                    default:
                        throw new NotSupportedException($"Operator {bin.Operator} is not supported.");
                }

                break;

            default:
                throw new NotSupportedException($"Can't generate for AST node: {node.GetType().Name}");
        }
    }

    /// <summary>
    /// Emits PUSH instruction for given number based on its BitWidth.
    /// </summary>
    private void EmitPushLiteral(NumberLiteralNode lit, List<string> instr)
    {
        int n = lit.BitWidth / 8;
        BigInteger value = lit.Value;
        string hex = value.ToString("X");
        hex = hex.PadLeft(n * 2, '0');
        instr.Add($"PUSH{n} 0x{hex}");
    }

    /// <summary>
    /// Generates PUSH32 instruction for string literal (padded to 32 bytes)
    /// </summary>
    private void GenerateStringLiteral(StringLiteralNode str, List<string> instr)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str.Value);
        int length = bytes.Length;
        if (length > 32)
            throw new Exception($"String literal '{str.Value}' takes {length} bytes, max is 32.");

        byte[] padded = new byte[32];
        Array.Copy(bytes, 0, padded, 32 - length, length);
        string hex = BitConverter.ToString(padded).Replace("-", "");
        instr.Add($"PUSH32 0x{hex}");
    }

    /// <summary>
    /// Calculates minimal number of bytes to represent offset.
    /// </summary>
    private int ComputeByteCount(int offset)
    {
        if (offset < (1 << 8)) return 1;
        if (offset < (1 << 16)) return 2;
        if (offset < (1 << 24)) return 3;
        if (offset < (1 << 32)) return 4;
        // za vrlo velike offset-e, koristi 32 bajta
        return 32;
    }
    
    private void GenerateIf(IfStatementNode node, List<string> instr)
    {
        int elseLabel = _labelCounter++;
        int endLabel  = _labelCounter++;

        // 1) condition
        GenerateNode(node.Condition, instr);
        // if false, jump to else
        instr.Add($"PUSH1 0x{elseLabel:X2}");
        instr.Add("JUMPI");

        // 2) then-branch
        foreach (var s in node.ThenBranch)
        {
            if (s is VariableDeclarationNode vd)
                GenerateNode(vd.Initializer, instr);
            else
                GenerateNode(s, instr);

            // write variable if it is declaration
            if (s is VariableDeclarationNode vdd)
            {
                var sym = _symbols[vdd.Identifier];
                int sz = ComputeByteCount(sym.Offset);
                instr.Add($"PUSH{sz} 0x{sym.Offset:X2}");
                instr.Add(vdd.Type == PrimitiveType.UInt8 ? "MSTORE8" : "MSTORE");
            }
        }

        // after then, jump to else
        instr.Add($"PUSH1 0x{endLabel:X2}");
        instr.Add("JUMP");

        // 3) else-label
        instr.Add($"// label {elseLabel}");
        instr.Add("JUMPDEST");

        if (node.ElseBranch != null)
        {
            foreach (var s in node.ElseBranch)
            {
                // same as then-branch
                if (s is VariableDeclarationNode vd)
                    GenerateNode(vd.Initializer, instr);
                else
                    GenerateNode(s, instr);

                if (s is VariableDeclarationNode vdd)
                {
                    var sym = _symbols[vdd.Identifier];
                    int sz = ComputeByteCount(sym.Offset);
                    instr.Add($"PUSH{sz} 0x{sym.Offset:X2}");
                    instr.Add(vdd.Type == PrimitiveType.UInt8 ? "MSTORE8" : "MSTORE");
                }
            }
        }

        // 4) end-label
        instr.Add($"// label {endLabel}");
        instr.Add("JUMPDEST");
    }

}