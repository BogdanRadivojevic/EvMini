using System.Numerics;

namespace EvmCompiler.Core.AST;

public class NumberLiteralNode : Node
{
    public BigInteger Value { get; }  // supported values: 0 <= Value < 2^BitWidth
    public int BitWidth   { get; }    // for example 8, 16, 32, 256

    public NumberLiteralNode(BigInteger value, int bitWidth)
    {
        Value     = value;
        BitWidth  = bitWidth;
    }
    public override void Accept(IAstVisitor visitor)
        => visitor.Visit(this);
    public override string ToString() => Value.ToString();
}