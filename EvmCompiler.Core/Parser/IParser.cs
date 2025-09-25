using EvmCompiler.Core.AST;

namespace EvmCompiler.Core.Parser;

public interface IParser
{
    ProgramNode ParseProgram();
    
}