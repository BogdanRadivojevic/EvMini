using EvmCompiler.Core.AST;

namespace EvmCompiler.Core.Semantics;

/// <summary>
/// Wrapper around PrimitiveType (u8/u16/u32/u256/bool).
/// </summary>
public class PrimitiveTypeInfo : TypeInfo
{
    public PrimitiveType Type { get; }
    public PrimitiveTypeInfo(PrimitiveType type) 
        : base((int)type) 
        => Type = type;
}