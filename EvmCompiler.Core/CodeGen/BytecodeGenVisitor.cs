using System.Globalization;
using System.Numerics;
using System.Text;
using EvmCompiler.Core.AST;
using EvmCompiler.Core.Semantics;

namespace EvmCompiler.Core.CodeGen;

/// <summary>
/// Visitor which generates EVM instructions.
/// </summary>
public class BytecodeGenVisitor : IAstVisitor
{
    private readonly Dictionary<string, (int Offset, TypeInfo Type)> _symbols;
    private readonly List<byte> _bytes = new();
    private int _labelCounter;

    // for placeholder
    private readonly List<(int Position, int LabelId, int Size)> _pendingPushes = new();
    private readonly Dictionary<int, int> _labelOffsets = new();

    public BytecodeGenVisitor(IReadOnlyDictionary<string, (int Offset, TypeInfo Type)> symbols)
    {
        // Using LINQ to convert IReadOnlyDictionary to Dictionary
        _symbols = symbols.ToDictionary(kv => kv.Key, kv => kv.Value);
    }


    public byte[] Bytes
    {
        get
        {
            PatchPlaceholders();
            return _bytes.ToArray();
        }
    }

    public void Visit(ProgramNode program)
    {
        foreach (var stmt in program.Body)
            stmt.Accept(this);
    }

    public void Visit(VariableDeclarationNode vd)
    {
        vd.Initializer.Accept(this);
        var sym = _symbols[vd.Identifier];
        int n = ComputeByteCount(sym.Offset);
        AddPush(n, new BigInteger(sym.Offset));

        // 3) store: if is u8 -> MSTORE8, else MSTORE
        if (sym.Type is PrimitiveTypeInfo pti && pti.Type == PrimitiveType.UInt8)
            _bytes.Add(0x53); // MSTORE8
        else
            _bytes.Add(0x52); // MSTORE
    }

    public void Visit(NumberLiteralNode lit)
    {
        int n = lit.BitWidth / 8;
        AddPush(n, lit.Value);
    }

    public void Visit(StringLiteralNode str)
    {
        var raw = Encoding.UTF8.GetBytes(str.Value);
        if (raw.Length > 32)
            throw new Exception($"String literal '{str.Value}' is longer than 32 bytes.");
        var padded = new byte[32];
        Array.Copy(raw, 0, padded, 32 - raw.Length, raw.Length);
        _bytes.Add(0x7f); // PUSH32
        _bytes.AddRange(padded);
    }

    public void Visit(BooleanLiteralNode bl)
    {
        _bytes.Add(0x60); // PUSH1
        _bytes.Add(bl.Value ? (byte)1 : (byte)0);
    }

    public void Visit(IdentifierNode id)
    {
        var sym = _symbols[id.Name];
        int n = ComputeByteCount(sym.Offset);
        AddPush(n, new BigInteger(sym.Offset));
        _bytes.Add(0x51); // MLOAD
    }

    public void Visit(BinaryExpressionNode bin)
    {
        bin.Left.Accept(this);
        bin.Right.Accept(this);
        switch (bin.Operator)
        {
            case "+": _bytes.Add(0x01); break; // ADD
            case "-": _bytes.Add(0x03); break; // SUB
            case "*": _bytes.Add(0x02); break; // MUL
            case "/": _bytes.Add(0x04); break; // DIV
            case "<": _bytes.Add(0x10); break; // LT
            case ">": _bytes.Add(0x11); break; // GT
            case "==": _bytes.Add(0x14); break; // EQ
            case "!=":
                _bytes.Add(0x14);
                _bytes.Add(0x15); // EQ; ISZERO
                break;
            case "<=":
                _bytes.Add(0x11);
                _bytes.Add(0x15); // GT; ISZERO
                break;
            case ">=":
                _bytes.Add(0x10);
                _bytes.Add(0x15); // LT; ISZERO
                break;
            case "&&":
                _bytes.Add(0x16);
                break;
            case "||":
                _bytes.Add(0x17);
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
        AddPushPlaceholder(1, elseId);
        _bytes.Add(0x57); // JUMPI

        // then branch
        foreach (var s in iff.ThenBranch)
            s.Accept(this);
        AddPushPlaceholder(1, endId);
        _bytes.Add(0x56); // JUMP

        // JUMPDEST for else
        _labelOffsets[elseId] = _bytes.Count;
        _bytes.Add(0x5B); // JUMPDEST (elseLbl)

        // else branch
        if (iff.ElseBranch != null)
            foreach (var s in iff.ElseBranch)
                s.Accept(this);

        // JUMPDEST for end
        _labelOffsets[endId] = _bytes.Count;
        _bytes.Add(0x5B); // JUMPDEST (endLbl)
    }

    public void Visit(WhileStatementNode w)
    {
        int startId = _labelCounter++;
        int endId = _labelCounter++;

        // JUMPDEST for start:
        _labelOffsets[startId] = _bytes.Count;
        _bytes.Add(0x5B); // JUMPDEST for startLbl

        // cond
        w.Condition.Accept(this);
        AddPushPlaceholder(1, endId);
        _bytes.Add(0x57); // JUMPI

        // body
        foreach (var s in w.Body)
            s.Accept(this);

        // jump back to start
        AddPushPlaceholder(1, startId);
        _bytes.Add(0x56); // JUMP

        // end:
        _labelOffsets[endId] = _bytes.Count;
        _bytes.Add(0x5B); // JUMPDEST for endLbl
    }

    public void Visit(AssignmentNode an)
    {
        // 1) generate value
        an.Expression.Accept(this);

        // 2) PUSH offset
        var sym = _symbols[an.Identifier];
        int offBytes = ComputeByteCount(sym.Offset);
        AddPush(offBytes, new BigInteger(sym.Offset));

        // 3) MSTORE8 or MSTORE
        if (sym.Type is PrimitiveTypeInfo pti && pti.Type == PrimitiveType.UInt8)
            _bytes.Add(0x53);
        else
            _bytes.Add(0x52);
    }

    public void Visit(ArrayLiteralNode node)
    {
        int count = node.Elements.Count;
        int totalSize = count * 32;

        // 1) PUSH1 0x40; MLOAD
        _bytes.Add(0x60);
        _bytes.Add(0x40);
        _bytes.Add(0x51);

        // 2) DUP1
        _bytes.Add(0x80);

        // 3) PUSH2 totalSize; ADD
        AddPush(2, new BigInteger(totalSize));
        _bytes.Add(0x01);

        // 4) PUSH1 0x40; SWAP1; MSTORE
        _bytes.Add(0x60);
        _bytes.Add(0x40);
        _bytes.Add(0x90);
        _bytes.Add(0x52);

        // 5) write elements
        for (int i = 0; i < count; i++)
        {
            // 5.1) PUSH2 i*32; ADD
            AddPush(2, new BigInteger(i * 32));
            _bytes.Add(0x01);

            // 5.2) generate value
            node.Elements[i].Accept(this);

            // 5.3) MSTORE
            _bytes.Add(0x52);
        }
        // In the end, in the stack: basePtr
    }

    public void Visit(ArrayAccessNode node)
    {
        // basePtr
        node.Array.Accept(this);
        // idx
        node.Index.Accept(this);
        // *32
        _bytes.Add(0x60);
        _bytes.Add(0x20);
        _bytes.Add(0x02); // MUL
        // ADD
        _bytes.Add(0x01);
        // MLOAD
        _bytes.Add(0x51);
    }

    // ————— helper methods —————
    
    /// <summary>
    /// Subsequently replaces placeholders with actual offsets.
    /// </summary>
    private void PatchPlaceholders()
    {
        foreach (var (pos, labelId, size) in _pendingPushes)
        {
            if (!_labelOffsets.TryGetValue(labelId, out var dest))
                throw new Exception($"Unknown labelId {labelId} when patching.");

            string hex = dest.ToString("X").PadLeft(size * 2, '0');
            for (int i = 0; i < size; i++)
            {
                _bytes[pos + i] = byte.Parse(
                    hex.Substring(i * 2, 2),
                    NumberStyles.HexNumber);
            }
        }

        _pendingPushes.Clear();
    }

    private void AddPush(int n, BigInteger val)
    {
        _bytes.Add((byte)(0x5f + n)); // PUSHn
        string hex = val.ToString("X").PadLeft(n * 2, '0');
        for (int i = 0; i < n; i++)
            _bytes.Add(byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber));
    }

    /// <summary>
    /// Adds PUSHn placeholder: writes opcode and 'size' null-bytes,
    /// and stores label to be patched in _pendingPushes.
    /// Returns position of first immediate byte.
    /// </summary>
    private int AddPushPlaceholder(int size, int labelId)
    {
        // PUSH1 = 0x60, PUSH2 = 0x61, … op = 0x5f + size
        _bytes.Add((byte)(0x5f + size));
        int pos = _bytes.Count;
        for (int i = 0; i < size; i++)
            _bytes.Add(0x00);
        _pendingPushes.Add((pos, labelId, size));
        return pos;
    }

    private int ComputeByteCount(int offset)
    {
        if (offset < (1 << 8)) return 1;
        if (offset < (1 << 16)) return 2;
        if (offset < (1 << 24)) return 3;
        return 32;
    }
}