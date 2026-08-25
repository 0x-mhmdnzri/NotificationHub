using System.Globalization;
using System.Text.Json;

namespace NotificationHub.Core.Expressions;

/// <summary>
/// Safe expression language (no code execution) — SEC-13 limits on size/depth.
/// Supports comparisons, string ops, exists, and/or/not, parentheses.
/// </summary>
public sealed class SimpleExpressionEvaluator : IExpressionEvaluator
{
    public const int MaxExpressionLength = 512;
    public const int MaxTokens = 128;
    public const int MaxParenDepth = 16;

    public bool Evaluate(string? expression, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        if (expression.Length > MaxExpressionLength) return false;
        try
        {
            var tokens = Tokenizer.Tokenize(expression);
            if (tokens.Count > MaxTokens + 1) return false; // +1 for EOF
            var parser = new Parser(tokens, data);
            return parser.ParseExpression();
        }
        catch
        {
            return false;
        }
    }

    private enum TokenKind { Ident, String, Number, Op, LParen, RParen, Eof }

    private sealed record Token(TokenKind Kind, string Text);

    private static class Tokenizer
    {
        public static List<Token> Tokenize(string input)
        {
            var tokens = new List<Token>();
            var i = 0;
            while (i < input.Length)
            {
                var c = input[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '(') { tokens.Add(new Token(TokenKind.LParen, "(")); i++; continue; }
                if (c == ')') { tokens.Add(new Token(TokenKind.RParen, ")")); i++; continue; }

                if (c is '"' or '\'')
                {
                    var q = c; i++;
                    var start = i;
                    var len = 0;
                    while (i < input.Length && input[i] != q)
                    {
                        i++;
                        len++;
                        if (len > 256) throw new InvalidOperationException("String literal too long");
                    }
                    var s = input[start..i];
                    if (i < input.Length) i++;
                    tokens.Add(new Token(TokenKind.String, s));
                    continue;
                }

                if (i + 1 < input.Length)
                {
                    var two = input[i..(i + 2)];
                    if (two is "==" or "!=" or ">=" or "<=")
                    {
                        tokens.Add(new Token(TokenKind.Op, two));
                        i += 2;
                        continue;
                    }
                }

                if (c is '>' or '<' or '!')
                {
                    tokens.Add(new Token(TokenKind.Op, c.ToString()));
                    i++;
                    continue;
                }

                if (char.IsDigit(c) || (c == '-' && i + 1 < input.Length && char.IsDigit(input[i + 1])))
                {
                    var start = i; i++;
                    while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.')) i++;
                    tokens.Add(new Token(TokenKind.Number, input[start..i]));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    var start = i; i++;
                    while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_' || input[i] == '.')) i++;
                    var word = input[start..i];
                    if (word.Length > 64) throw new InvalidOperationException("Identifier too long");
                    var lower = word.ToLowerInvariant();
                    if (lower is "and" or "or" or "not" or "contains" or "startswith" or "endswith" or "exists" or "true" or "false")
                        tokens.Add(new Token(TokenKind.Op, lower));
                    else
                        tokens.Add(new Token(TokenKind.Ident, word));
                    continue;
                }

                throw new InvalidOperationException($"Unexpected character '{c}'");
            }
            tokens.Add(new Token(TokenKind.Eof, ""));
            return tokens;
        }
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private readonly IReadOnlyDictionary<string, object?> _data;
        private int _pos;
        private int _depth;

        public Parser(List<Token> tokens, IReadOnlyDictionary<string, object?> data)
        {
            _tokens = tokens;
            _data = data;
        }

        private Token Peek() => _tokens[_pos];
        private Token Next() => _tokens[_pos++];

        public bool ParseExpression()
        {
            var result = ParseOr();
            if (Peek().Kind != TokenKind.Eof)
                throw new InvalidOperationException("Unexpected trailing tokens");
            return result;
        }

        private bool ParseOr()
        {
            var left = ParseAnd();
            while (Peek().Kind == TokenKind.Op && Peek().Text == "or")
            {
                Next();
                var right = ParseAnd();
                left = left || right;
            }
            return left;
        }

        private bool ParseAnd()
        {
            var left = ParseNot();
            while (Peek().Kind == TokenKind.Op && Peek().Text == "and")
            {
                Next();
                var right = ParseNot();
                left = left && right;
            }
            return left;
        }

        private bool ParseNot()
        {
            if (Peek().Kind == TokenKind.Op && Peek().Text == "not")
            {
                Next();
                return !ParseNot();
            }
            return ParsePrimary();
        }

        private bool ParsePrimary()
        {
            if (Peek().Kind == TokenKind.LParen)
            {
                Next();
                _depth++;
                if (_depth > MaxParenDepth) throw new InvalidOperationException("Expression too deeply nested");
                var inner = ParseOr();
                _depth--;
                if (Peek().Kind != TokenKind.RParen) throw new InvalidOperationException("Expected ')'");
                Next();
                return inner;
            }

            if (Peek().Kind == TokenKind.Op && Peek().Text == "exists")
            {
                Next();
                if (Peek().Kind != TokenKind.Ident) throw new InvalidOperationException("Expected field after exists");
                var field = Next().Text;
                return _data.TryGetValue(field, out var v) && v is not null && !(v is string s && string.IsNullOrEmpty(s));
            }

            if (Peek().Kind == TokenKind.Op && Peek().Text is "true" or "false")
                return Next().Text == "true";

            if (Peek().Kind != TokenKind.Ident)
                throw new InvalidOperationException("Expected identifier");

            var leftField = Next().Text;
            var leftVal = Resolve(leftField);

            if (Peek().Kind != TokenKind.Op)
                return IsTruthy(leftVal);

            var op = Next().Text;
            if (op is "contains" or "startswith" or "endswith")
            {
                var right = ReadValue();
                var ls = leftVal?.ToString() ?? "";
                var rs = right?.ToString() ?? "";
                return op switch
                {
                    "contains" => ls.Contains(rs, StringComparison.OrdinalIgnoreCase),
                    "startswith" => ls.StartsWith(rs, StringComparison.OrdinalIgnoreCase),
                    "endswith" => ls.EndsWith(rs, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }

            var rightVal = ReadValue();
            return Compare(leftVal, op, rightVal);
        }

        private object? ReadValue()
        {
            var t = Peek();
            if (t.Kind == TokenKind.String) { Next(); return t.Text; }
            if (t.Kind == TokenKind.Number)
            {
                Next();
                if (double.TryParse(t.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)) return n;
                return t.Text;
            }
            if (t.Kind == TokenKind.Op && t.Text is "true" or "false") { Next(); return t.Text == "true"; }
            if (t.Kind == TokenKind.Ident) { Next(); return Resolve(t.Text); }
            throw new InvalidOperationException("Expected value");
        }

        private object? Resolve(string field)
        {
            if (!_data.TryGetValue(field, out var raw) || raw is null) return null;
            if (raw is JsonElement je) return FromJson(je);
            return raw;
        }

        private static object? FromJson(JsonElement je) => je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => je.ToString()
        };

        private static bool IsTruthy(object? v) => v switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrEmpty(s) && !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase),
            int n => n != 0,
            long n => n != 0,
            double d => Math.Abs(d) > double.Epsilon,
            _ => true
        };

        private static bool Compare(object? left, string op, object? right)
        {
            if (op is "==" or "!=")
            {
                var eq = ValuesEqual(left, right);
                return op == "==" ? eq : !eq;
            }

            if (TryToDouble(left, out var ld) && TryToDouble(right, out var rd))
            {
                return op switch
                {
                    ">" => ld > rd,
                    ">=" => ld >= rd,
                    "<" => ld < rd,
                    "<=" => ld <= rd,
                    _ => false
                };
            }

            var ls = left?.ToString() ?? "";
            var rs = right?.ToString() ?? "";
            var cmp = string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
            return op switch
            {
                ">" => cmp > 0,
                ">=" => cmp >= 0,
                "<" => cmp < 0,
                "<=" => cmp <= 0,
                _ => false
            };
        }

        private static bool ValuesEqual(object? a, object? b)
        {
            if (a is null && b is null) return true;
            if (a is null || b is null) return false;
            if (TryToDouble(a, out var da) && TryToDouble(b, out var db))
                return Math.Abs(da - db) < 0.0000001;
            return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryToDouble(object? v, out double d)
        {
            switch (v)
            {
                case null: d = 0; return false;
                case double x: d = x; return true;
                case float x: d = x; return true;
                case int x: d = x; return true;
                case long x: d = x; return true;
                case JsonElement je when je.ValueKind == JsonValueKind.Number:
                    d = je.GetDouble(); return true;
                default:
                    return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d);
            }
        }
    }
}
