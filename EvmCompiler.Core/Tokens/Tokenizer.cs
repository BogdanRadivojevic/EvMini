using System.Text;

namespace EvmCompiler.Core.Tokens;

public class Tokenizer : ITokenizer
{
    private readonly string _input;
    private int _position = 0;

    private static readonly HashSet<string> Keywords = new() { "let", "const", "var", "if", "else", "while", "for", "return", "break", "continue" };
    private static readonly HashSet<char> Operators = new() { '+', '-', '*', '/', '=', '>', '<', '!', '&', '|' };
    private static readonly HashSet<char> Punctuation = new() { ';', ':', '(', ')', '{', '}', '[', ']', ',' };

    public Tokenizer(string input) {
        _input = input;
    }

    public List<Token> Tokenize() {
        List<Token> tokens = new();

        while (_position < _input.Length) {
            char current = _input[_position];

            if (char.IsWhiteSpace(current)) {
                _position++;
                continue;
            }
            
            // string literal
            if (current == '"' || current == '\'') {
                char quote = current;
                    _position++;  // skip opening quote
                var sb = new StringBuilder();
                while (_position < _input.Length && _input[_position] != quote) {
                    // support escaped characters, e.g. \" or \\
                    if (_input[_position] == '\\' && _position + 1 < _input.Length) {
                        sb.Append(_input[_position + 1]);
                        _position += 2;
                    } else {
                        sb.Append(_input[_position]);
                        _position++;
                    }
                }
                if (_position >= _input.Length)
                    throw new Exception("Missing closing quote for string literal.");
                _position++; // skip closing quote

                tokens.Add(new Token(TokenType.StringLiteral, sb.ToString()));
                continue;
            }

            if (char.IsLetter(current)) {
                string word = ReadWhile(char.IsLetterOrDigit);

                // --- recognize boolean literals ---
                if (word == "true" || word == "false")
                {
                    tokens.Add(new Token(TokenType.BooleanLiteral, word));
                }
                else
                {
                    TokenType type = Keywords.Contains(word)
                        ? TokenType.Keyword 
                        : TokenType.Identifier;
                    tokens.Add(new Token(type, word));
                }
                continue;
            }
            else if (char.IsDigit(current)) {
                string number = ReadWhile(char.IsDigit);
                tokens.Add(new Token(TokenType.NumberLiteral, number));
            }
            else if (Operators.Contains(current)) {
                string op = ReadWhile(c => Operators.Contains(c));
                tokens.Add(new Token(TokenType.Operator, op));
            }
            else if (Punctuation.Contains(current)) {
                tokens.Add(new Token(TokenType.Punctuation, current.ToString()));
                _position++;
            }
            else {
                throw new Exception($"Unknown character: '{current}' at position {_position}");
            }
        }

        return tokens;
    }

    private string ReadWhile(Func<char, bool> condition) {
        StringBuilder result = new();
        while (_position < _input.Length && condition(_input[_position])) {
            result.Append(_input[_position]);
            _position++;
        }
        return result.ToString();
    }
}