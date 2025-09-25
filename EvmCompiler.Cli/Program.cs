using EvmCompiler.Core.AST;
using EvmCompiler.Core.CodeGen;
using EvmCompiler.Core.Parser;
using EvmCompiler.Core.Semantics;
using EvmCompiler.Core.Tokens;

namespace EvmCompiler.Cli;

class Program
{
    static void Main(string[] args)
    {
        // 1) Loading code from file or REPL
        string? path = args.Length > 0 ? args[0] : null;
        string code;

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            code = File.ReadAllText(path);
        }
        else
        {
            Console.WriteLine("Enter the code (end with empty line):");
            var lines = new List<string>();
            string? line;
            while (!string.IsNullOrEmpty(line = Console.ReadLine()))
            {
                lines.Add(line);
            }

            code = string.Join(Environment.NewLine, lines);
        }

        try
        {
            // 2) Tokenizer
            var tokenizer = new Tokenizer(code);
            var tokens = tokenizer.Tokenize();

            Console.WriteLine("\nTokens:");
            foreach (var token in tokens)
                Console.WriteLine(token);

            // 3) Partsing in AST
            var parser = new Parser(tokens);
            ProgramNode ast = parser.ParseProgram();

            Console.WriteLine("\nAST:");
            Console.WriteLine(ast);

            // 4) Semantic analysis
            var analyzer = new SemanticAnalyzer();
            analyzer.Analyze(ast);

            Console.WriteLine("\nSymbol Table:");
            foreach (var kv in analyzer.Symbols)
            {
                Console.WriteLine($"{kv.Key} -> offset: {kv.Value.Offset}, type: {kv.Value.Type}");
            }

            // 5) Generating EVM assembly
            var asmVisitor = new AssemblerGenVisitor(analyzer.Symbols);
            ast.Accept(asmVisitor);
            var asmInstr = asmVisitor.Instructions;

            Console.WriteLine("\nAssembly:");
            foreach (var line in asmInstr)
                Console.WriteLine(line);

            // 5b) Generating raw bytecode
            var byteVisitor = new BytecodeGenVisitor(analyzer.Symbols);
            ast.Accept(byteVisitor);
            var raw = byteVisitor.Bytes;
            string rawHex = BitConverter.ToString(raw).Replace("-", "");

            Console.WriteLine("\nRaw bytecode (hex):");
            Console.WriteLine(rawHex);

            // 6) Write to files if paths are provided
            if (args.Length > 1)
            {
                File.WriteAllLines(args[1], asmInstr);
                Console.WriteLine($"Assembly (.asm) saved in: {args[1]}");
            }

            if (args.Length > 2)
            {
                File.WriteAllBytes(args[2], raw);
                Console.WriteLine($"Raw bytecode (.bin) saved in: {args[2]}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}