namespace EvmCompiler.Core.Semantics;


/// <summary>
/// Apstract base class for all types (scalar or arrays).
/// </summary>
public abstract class TypeInfo
{
    public int BitWidth { get; }
    protected TypeInfo(int bitWidth) => BitWidth = bitWidth;
}