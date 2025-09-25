namespace EvmCompiler.Core.Semantics;

/// <summary>
/// Fixed-length array of elements of the same type.
/// </summary>
public class ArrayTypeInfo : TypeInfo
{
    public TypeInfo ElementType { get; }
    public int Length { get; }

    public ArrayTypeInfo(TypeInfo elementType, int length)
        : base(elementType.BitWidth * length)
    {
        ElementType = elementType;
        Length = length;
    }
}