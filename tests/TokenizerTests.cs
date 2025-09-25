using EvmCompiler.Core.Tokens;

namespace tests;

public class TokenizerTests
{
 private static string[] Format(string src)
        => new Tokenizer(src)
              .Tokenize()
              .Select(t => $"{t.Type}: '{t.Value}'")
              .ToArray();

    [Theory]
    [InlineData(
        "let x = 5;",
        new[] { "Keyword: 'let'", "Identifier: 'x'", "Operator: '='", "NumberLiteral: '5'", "Punctuation: ';'" }
    )]
    [InlineData(
        "const s = \"hello\";",
        new[] { "Keyword: 'const'", "Identifier: 's'", "Operator: '='", "StringLiteral: 'hello'", "Punctuation: ';'" }
    )]
    [InlineData(
        "var flag = true;",
        new[] { "Keyword: 'var'", "Identifier: 'flag'", "Operator: '='", "BooleanLiteral: 'true'", "Punctuation: ';'" }
    )]
    [InlineData(
        "if(a<=b){ }",
        new[]
        {
            "Keyword: 'if'",
            "Punctuation: '('",
            "Identifier: 'a'",
            "Operator: '<='",
            "Identifier: 'b'",
            "Punctuation: ')'",
            "Punctuation: '{'",
            "Punctuation: '}'"
        }
    )]
    [InlineData(
        "x!=y",
        new[] { "Identifier: 'x'", "Operator: '!='" , "Identifier: 'y'" }
    )]
    [InlineData(
        "num>=100",
        new[] { "Identifier: 'num'", "Operator: '>='", "NumberLiteral: '100'" }
    )]
    [InlineData(
        "\"Esc\\\"aped\"",
        new[] { "StringLiteral: 'Esc\"aped'" }
    )]
    [InlineData(
        "a && b || !c",
        new[]
        {
            "Identifier: 'a'",
            "Operator: '&&'",
            "Identifier: 'b'",
            "Operator: '||'",
            "Operator: '!'",
            "Identifier: 'c'"
        }
    )]
    [InlineData(
        "12345",
        new[] { "NumberLiteral: '12345'" }
    )]
    public void Tokenize_RazlicitiUlazi_VratiOcekivaneTokene(string src, string[] ocekivano)
    {
        var stvarni = Format(src);
        Assert.Equal(ocekivano, stvarni);
    }

    [Fact]
    public void Tokenize_IgnoriseRazmakeINoveRedove()
    {
        var src = "  let   \n  x  =  42  ; ";
        var stvarni = Format(src);
        var ocekivano = new[]
        {
            "Keyword: 'let'",
            "Identifier: 'x'",
            "Operator: '='",
            "NumberLiteral: '42'",
            "Punctuation: ';'"
        };
        Assert.Equal(ocekivano, stvarni);
    }

    [Fact]
    public void Tokenize_SlozenIzraz_KorektniTokeni()
    {
        var src = "result = (a + b) * (c - d) / 10;";
        var stvarni = Format(src);
        var ocekivano = new[]
        {
            "Identifier: 'result'",
            "Operator: '='",
            "Punctuation: '('",
            "Identifier: 'a'",
            "Operator: '+'",
            "Identifier: 'b'",
            "Punctuation: ')'",
            "Operator: '*'",
            "Punctuation: '('",
            "Identifier: 'c'",
            "Operator: '-'",
            "Identifier: 'd'",
            "Punctuation: ')'",
            "Operator: '/'",
            "NumberLiteral: '10'",
            "Punctuation: ';'"
        };
        Assert.Equal(ocekivano, stvarni);
    }
}
