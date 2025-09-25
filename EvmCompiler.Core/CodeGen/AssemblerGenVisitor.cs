using System.Text;
using EvmCompiler.Core.AST;
using EvmCompiler.Core.Semantics;

namespace EvmCompiler.Core.CodeGen;

/// <summary>
/// Visitor which generates EVM instructions.
/// </summary>
public class AssemblerGenVisitor : IAstVisitor
{
    private readonly Dictionary<string, (int Offset, TypeInfo Type)> _symbols;
    private readonly List<string> _instr = new();
    private int _labelCounter;

    // For each pending push, we need to patch it with the actual offset.
    private readonly List<(int InstrIndex, int LabelId, int ByteCount)> _pendingPushes
        = new();

    private readonly Dictionary<int, int> _labelOffsets = new();

    public AssemblerGenVisitor(IReadOnlyDictionary<string, (int Offset, TypeInfo Type)> symbols)
        => _symbols = symbols.ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>
    ///     Generates instructions and returns them as a list of strings.
    /// </summary>
    public List<string> Instructions => _instr;

    public void Visit(ProgramNode program)
    {
        foreach (var stmt in program.Body)
            stmt.Accept(this);
    }

    public void Visit(VariableDeclarationNode vd)
    {
        // inicializer
        vd.Initializer.Accept(this);

        // store in memory
        var sym = _symbols[vd.Identifier];
        int bytes = ComputeByteCount(sym.Offset);
        string offHex = sym.Offset.ToString("X").PadLeft(bytes * 2, '0');
        _instr.Add($"PUSH{bytes} 0x{offHex}");
        if (sym.Type is PrimitiveTypeInfo { Type: PrimitiveType.UInt8 })
            _instr.Add("MSTORE8");
        else
            _instr.Add("MSTORE");
    }

    public void Visit(NumberLiteralNode lit)
    {
        int n = lit.BitWidth / 8;
        string hex = lit.Value.ToString("X").PadLeft(n * 2, '0');
        _instr.Add($"PUSH{n} 0x{hex}");
    }

    public void Visit(StringLiteralNode str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str.Value);
        int length = bytes.Length;
        if (length > 32)
            throw new Exception($"String literal '{str.Value}' takes {length} bytes, max is 32.");

        // 2. Padding on 32 bytes
        byte[] padded = new byte[32];
        Array.Copy(bytes, 0, padded, 32 - length, length);

        // 3. Hex representation and PUSH32
        string hex = BitConverter.ToString(padded).Replace("-", "");
        _instr.Add($"PUSH32 0x{hex}");
    }

    /// <summary>
    ///     Method that generates instructions for boolean literals.
    /// </summary>
    /// <param name="bl">BooleanLiteralNode</param>
    public void Visit(BooleanLiteralNode bl)
    {
        _instr.Add(bl.Value ? "PUSH1 0x01" : "PUSH1 0x00");
    }

    /// <summary>
    ///     Method that generates instructions for identifier nodes.
    /// </summary>
    /// <param name="id">IdentifierNode</param>
    public void Visit(IdentifierNode id)
    {
        var sym = _symbols[id.Name];
        int b = ComputeByteCount(sym.Offset);
        string hx = sym.Offset.ToString("X").PadLeft(b * 2, '0');
        _instr.Add($"PUSH{b} 0x{hx}");
        _instr.Add("MLOAD");
    }

    /// <summary>
    ///     Method that generates instructions for binary operations.
    /// </summary>
    /// <param name="bin">BinaryExpressionNode</param>
    /// <exception cref="NotSupportedException"></exception>
    public void Visit(BinaryExpressionNode bin)
    {
        bin.Left.Accept(this);
        bin.Right.Accept(this);
        switch (bin.Operator)
        {
            case "+": _instr.Add("ADD"); break;
            case "-": _instr.Add("SUB"); break;
            case "*": _instr.Add("MUL"); break;
            case "/": _instr.Add("DIV"); break;
            case "<": _instr.Add("LT"); break;
            case ">": _instr.Add("GT"); break;
            case "==": _instr.Add("EQ"); break;
            case "!=":
                _instr.Add("EQ");
                _instr.Add("ISZERO");
                break;
            case "<=":
                _instr.Add("GT");
                _instr.Add("ISZERO");
                break;
            case ">=":
                _instr.Add("LT");
                _instr.Add("ISZERO");
                break;
            case "&&":
                _instr.Add("AND");
                break;
            case "||":
                _instr.Add("OR");
                break;
            default:
                throw new NotSupportedException($"Operator {bin.Operator}");
        }
    }


    public void Visit(IfStatementNode iff)
    {
        int elseId = _labelCounter++;
        int endId = _labelCounter++;

        // condition
        iff.Condition.Accept(this);
        _instr.Add($"PUSH1 0x{elseId:X2}");
        _instr.Add("JUMPI");

        // then-branch
        foreach (var s in iff.ThenBranch)
            s.Accept(this);
        _instr.Add($"PUSH1 0x{endId:X2}");
        _instr.Add("JUMP");

        // else-label
        _instr.Add($"// label {elseId}");
        _instr.Add("JUMPDEST");
        if (iff.ElseBranch != null)
            foreach (var s in iff.ElseBranch)
                s.Accept(this);

        // end-label
        _instr.Add($"// label {endId}");
        _instr.Add("JUMPDEST");
    }

    /// <summary>
    ///     While loop
    /// </summary>
    /// <param name="w">WhileStatementNode</param>
    public void Visit(WhileStatementNode w)
    {
        int startLbl = _labelCounter++;
        int endLbl = _labelCounter++;

        // label start
        _instr.Add($"// label {startLbl}");
        _instr.Add("JUMPDEST");

        // condition
        w.Condition.Accept(this);
        _instr.Add($"PUSH1  0x{endLbl:X2}");
        _instr.Add("JUMPI"); // if false, jump to end

        // body
        foreach (var s in w.Body)
            s.Accept(this);

        // jumping back on the start (loop)
        _instr.Add($"PUSH1  0x{startLbl:X2}");
        _instr.Add("JUMP");

        // end label
        _instr.Add($"// label {endLbl}");
        _instr.Add("JUMPDEST");
    }

    public void Visit(AssignmentNode an)
    {
        // 1) generate expression
        an.Expression.Accept(this);

        // 2) PUSH offset
        var sym = _symbols[an.Identifier];
        int offBytes = ComputeByteCount(sym.Offset);
        string offHex = sym.Offset.ToString("X").PadLeft(offBytes * 2, '0');
        _instr.Add($"PUSH{offBytes} 0x{offHex}");

        // 3) MSTORE or MSTORE8
        if (sym.Type is PrimitiveTypeInfo pti && pti.Type == PrimitiveType.UInt8)
            _instr.Add("MSTORE8");
        else
            _instr.Add("MSTORE");
    }

    /// <summary>
    ///     Method that generates instructions for array literals.
    /// </summary>
    /// <param name="node">ArrayLiteralNode</param>
    public void Visit(ArrayLiteralNode node)
    {
        int count = node.Elements.Count;
        int totalSize = count * 32; // in bytes

        // 1) Getting free memory pointer from 0x40
        // Stack currently: []
        _instr.Add("PUSH1 0x40");
        _instr.Add("MLOAD"); // [ freePtr ]

        // 2) Double the base pointer on the stack.
        // Stack currently: [ freePtr ]
        _instr.Add("DUP1"); // [ freePtr, freePtr ]

        // 3) Calculating new freePtr = freePtr + totalSize
        // Stack currently: [ freePtr, freePtr ]
        _instr.Add($"PUSH2 0x{totalSize:X4}");
        _instr.Add("ADD"); // [ freePtr, newFreePtr ]

        // Placing newFreePtr at address 0x40
        // Stack currently: [ newFreePtr ]
        _instr.Add("PUSH1 0x40");
        _instr.Add("SWAP1");
        _instr.Add("MSTORE"); // [ freePtr ]

        // 5) Saving each element in the array
        // Stack currently: [ basePtr ]
        for (int i = 0; i < count; i++)
        {
            _instr.Add($"PUSH2 0x{(i * 32):X4}");
            _instr.Add("ADD"); // [ basePtr + i*32 ]
            _instr.Add("DUP2"); // [ basePtr + i*32, basePtr ]
            node.Elements[i].Accept(this); // [ addr, basePtr, value ]
            _instr.Add("SWAP1"); // [ basePtr, value, addr ] → [ basePtr, addr, value ]
            _instr.Add("MSTORE"); // [ basePtr ]
        }
    }

    /// <summary>
    ///     Method that generates instructions for array access.
    /// </summary>
    /// <param name="node">ArrayAccessNode</param>
    public void Visit(ArrayAccessNode node)
    {
        node.Array.Accept(this); 
        node.Index.Accept(this); 
        
        _instr.Add("PUSH1 0x20");
        _instr.Add("MUL"); 
        _instr.Add("ADD"); 
        
        _instr.Add("MLOAD");
    }

    private int ComputeByteCount(int offset)
    {
        if (offset < (1 << 8)) return 1;
        if (offset < (1 << 16)) return 2;
        if (offset < (1 << 24)) return 3;
        return 32;
    }
}