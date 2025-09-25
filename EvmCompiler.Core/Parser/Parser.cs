using System.Numerics;
using EvmCompiler.Core.AST;
using EvmCompiler.Core.Tokens;

namespace EvmCompiler.Core.Parser;

public class Parser : IParser
{
    private readonly List<Token> _tokens;
    private int _pos = 0;

    public Parser(List<Token> tokens)
        => _tokens = tokens;

    // Main entry-point method
    public ProgramNode ParseProgram()
    {
        var program = new ProgramNode();
        while (!IsAtEnd())
            program.Body.Add(ParseStatement());
        return program;
    }

    // Recognizes a single statement: declaration, assignment, or expression
    private Node ParseStatement()
    {
        if (Match(TokenType.Keyword, "if"))
            return ParseIfStatement();
        if (Match(TokenType.Keyword, "while"))
            return ParseWhileStatement();
        if (Match(TokenType.Keyword) && Previous().Value == "let")
            return ParseVariableDeclaration();
        if (Peek().Type == TokenType.Identifier && PeekNext().Type == TokenType.Operator && PeekNext().Value == "=")
            return ParseAssignment();

        return ParseExpressionStatement();
    }
    
    private IfStatementNode ParseIfStatement()
    {
        // 'if' is already consumed
        Consume(TokenType.Punctuation, "(");
        Node condition = ParseExpression();
        Consume(TokenType.Punctuation, ")");
    
        // then-branch
        Consume(TokenType.Punctuation, "{");
        var thenList = new List<Node>();
        while (!Match(TokenType.Punctuation, "}"))
            thenList.Add(ParseStatement());

        // optional else-branch
        List<Node>? elseList = null;
        if (Match(TokenType.Keyword, "else"))
        {
            Consume(TokenType.Punctuation, "{");
            elseList = new List<Node>();
            while (!Match(TokenType.Punctuation, "}"))
                elseList.Add(ParseStatement());
        }

        return new IfStatementNode(condition, thenList, elseList);
    }

    private WhileStatementNode ParseWhileStatement()
    {
        // 'while' is already consumed
        Consume(TokenType.Punctuation, "(");
        var cond = ParseExpression();
        Consume(TokenType.Punctuation, ")");
    
        Consume(TokenType.Punctuation, "{");
        var body = new List<Node>();
        while (!Match(TokenType.Punctuation, "}"))
            body.Add(ParseStatement());
    
        return new WhileStatementNode(cond, body);
    }


    private VariableDeclarationNode ParseVariableDeclaration()
    {
        string name = Consume(TokenType.Identifier, "Expected variable name").Value;
        PrimitiveType? annotated = ParseOptionalTypeAnnotation();

        Consume(TokenType.Operator, "=");
        Node init = ParseExpression();
        
        // infer bit width for numeric or boolean initializer
        PrimitiveType type = annotated ?? 
                             (init is BooleanLiteralNode
                                     ? PrimitiveType.Bool         // infer bool
                                     : PrimitiveType.UInt256      // instead uint256
                             );

        // adjust literal's bit width if necessary
        if (init is NumberLiteralNode numLit)
            init = new NumberLiteralNode(numLit.Value, (int)type);

        Consume(TokenType.Punctuation, ";");
        return new VariableDeclarationNode(type, name, init);
    }
    
    private PrimitiveType? ParseOptionalTypeAnnotation()
    {
        // if no ':', no annotation
        if (!Match(TokenType.Punctuation, ":"))
            return null;

        // expect type identifier
        string typeName = Consume(TokenType.Identifier, "Expected type annotation after ':'").Value;

        // Mapping string on to PrimitiveType
        return typeName switch
        {
            "u8"   => PrimitiveType.UInt8,
            "u16"  => PrimitiveType.UInt16,
            "u32"  => PrimitiveType.UInt32,
            "u256" => PrimitiveType.UInt256,
            _      => throw new Exception($"Unknown type '{typeName}'")
        };
    }

    private Node ParseExpressionStatement()
    {
        Node expr = ParseExpression();
        Consume(TokenType.Punctuation, ";");
        return expr;
    }

    // Expression parsing with precedence
    private Node ParseExpression()
        => ParseLogicalOr();

    private Node ParseLogicalOr()
    {
        // left side
        var node = ParseLogicalAnd();
        // while there is ||
        while (MatchOperator("||"))
        {
            string op = Previous().Value;
            var right = ParseLogicalAnd();
            node = new BinaryExpressionNode(op, node, right);
        }
        return node;
    }

    private Node ParseLogicalAnd()
    {
        // left side
        var node = ParseEquality();
        // while there is &&
        while (MatchOperator("&&"))
        {
            string op = Previous().Value;
            var right = ParseEquality();
            node = new BinaryExpressionNode(op, node, right);
        }
        return node;
    }

    private Node ParseEquality()
    {
        var node = ParseComparison();
        while (MatchOperator("==", "!="))
        {
            string op = Previous().Value;
            var right = ParseComparison();
            node = new BinaryExpressionNode(op, node, right);
        }
        return node;
    }

    private Node ParseComparison()
    {
        var node = ParseTerm();

        while (MatchOperator("<", ">", "<=", ">="))
        {
            string op = Previous().Value;           // "<", ">", "<=", ">="
            var right = ParseTerm();
            node = new BinaryExpressionNode(op, node, right);
        }

        return node;
    }

    private Node ParseTerm()
    {
        var node = ParseFactor();
        while (MatchOperator("+", "-"))
        {
            string op = Previous().Value;
            var right = ParseFactor();
            node = new BinaryExpressionNode(op, node, right);
        }

        return node;
    }

    private Node ParseFactor()
    {
        var node = ParseUnary();
        while (MatchOperator("*", "/"))
        {
            string op = Previous().Value;
            var right = ParseUnary();
            node = new BinaryExpressionNode(op, node, right);
        }

        return node;
    }

    private Node ParseUnary()
    {
        if (MatchOperator("!", "-"))
        {
            string op = Previous().Value;
            var right = ParseUnary();
            return new BinaryExpressionNode(op, new NumberLiteralNode(0, 256), right);
        }

        return ParsePrimary();
    }

    private Node ParsePrimary()
    {
        if (Match(TokenType.Punctuation, "["))   return ParseArrayLiteral();
        if (Peek().Type == TokenType.Identifier) return ParseIdentifierOrAccess();
        if (Peek().Type == TokenType.NumberLiteral 
            || Peek().Type == TokenType.StringLiteral
            || Peek().Type == TokenType.BooleanLiteral
            || (Peek().Type == TokenType.Keyword && (Peek().Value == "true" || Peek().Value == "false")))
            return ParseLiteral();
        if (Match(TokenType.Punctuation, "("))   return ParseGrouping();

        throw new Exception("Unexpected token while parsing expression: " + Peek());
    }
    
    // ———————— helper parsers ————————
    private Node ParseArrayLiteral()
    {
        // '[' is already consumed in token stream
        var elems = new List<Node>();
        if (!Match(TokenType.Punctuation, "]"))
        {
            do
            {
                elems.Add(ParseExpression());
            } while (Match(TokenType.Punctuation, ","));
            Consume(TokenType.Punctuation, "]");
        }
        return new ArrayLiteralNode(elems);
    }

    private Node ParseIdentifierOrAccess()
    {
        // Advance the identifier once
        string name = Consume(TokenType.Identifier, "Expected identifier").Value;
        var idNode = new IdentifierNode(name);

        // postfix: arr[expr]
        if (Match(TokenType.Punctuation, "["))
        {
            var idx = ParseExpression();
            Consume(TokenType.Punctuation, "]");
            return new ArrayAccessNode(idNode, idx);
        }

        return idNode;
    }

    private Node ParseLiteral()
    {
        if (Match(TokenType.NumberLiteral))
            return new NumberLiteralNode(
                BigInteger.Parse(Previous().Value),
                256 
            );

        if (Match(TokenType.StringLiteral))
            return new StringLiteralNode(Previous().Value);

        if (Match(TokenType.BooleanLiteral) 
            || (Peek().Type == TokenType.Keyword && (Peek().Value == "true" || Peek().Value == "false")))
        {
            // Advance if bool as keyword
            string val = Previous().Value;
            return new BooleanLiteralNode(val == "true");
        }

        throw new Exception("Unrecognized literal at " + Peek());
    }

    private Node ParseGrouping()
    {
        // '(' already consumed
        Node expr = ParseExpression();
        Consume(TokenType.Punctuation, ")");
        return expr;
    }

    // ———————— helper methods ————————

    private bool Match(TokenType type)
        => !IsAtEnd() && Peek().Type == type && Advance() != null;

    private bool Match(TokenType type, string value)
    {
        if (IsAtEnd()) return false;

        var t = Peek();
        if (t.Type == type && t.Value == value)
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool MatchOperator(params string[] ops)
    {
        if (Peek().Type == TokenType.Operator && ops.Contains(Peek().Value))
        {
            Advance();
            return true;
        }

        return false;
    }

    private Token Consume(TokenType type, string message)
    {
        if (Peek().Type == type)
            return Advance();
        throw new Exception(message);
    }

    private Token Advance()
        => _pos < _tokens.Count ? _tokens[_pos++] : _tokens.Last();

    private Token Peek()
        => _pos < _tokens.Count ? _tokens[_pos] : _tokens.Last();

    private Token Previous()
        => _tokens[_pos - 1];

    private bool IsAtEnd()
        => _pos >= _tokens.Count;
    
    private Token PeekNext()
        => _pos + 1 < _tokens.Count ? _tokens[_pos + 1] : _tokens.Last();

    private AssignmentNode ParseAssignment()
    {
        // consuming an assignment statement
        string name = Consume(TokenType.Identifier, "Expected variable for assignment").Value;
        Consume(TokenType.Operator, "=");
        Node expr = ParseExpression();
        Consume(TokenType.Punctuation, ";");
        return new AssignmentNode(name, expr);
    }
}