#region Copyright (c) 2019 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace CSharpMinifier.Preprocessing
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Gratt;
    using PrefixParselet = System.Func<Token, Gratt.Parser<ISourceText, TokenKind, Token, int, Expression>, Expression>;
    using InfixParselet = System.Func<Token, Expression, Gratt.Parser<ISourceText, TokenKind, Token, int, Expression>, Expression>;
    using PrefixInterplet = System.Func<Token, Gratt.Parser<System.Func<string, bool>, TokenKind, Token, int, bool>, bool>;
    using InfixInterplet = System.Func<Token, bool, Gratt.Parser<System.Func<string, bool>, TokenKind, Token, int, bool>, bool>;

    public interface ISourceText
    {
        char this[int offset] { get; }
        string Substring(int offset, int length);
    }

    static class SourceText
    {
        public static ISourceText FromString(string s) =>
            new DelegatingSourceText(i => s[i], s.Substring);

        sealed class DelegatingSourceText : ISourceText
        {
            readonly Func<int, char> _indexer;
            readonly Func<int, int, string> _substring;

            public DelegatingSourceText(Func<int, char> indexer, Func<int, int, string> substring)
            {
                _indexer = indexer;
                _substring = substring;
            }

            public char this[int offset] => _indexer(offset);

            public string Substring(int offset, int length) =>
                _substring(offset, length);
        }
    }

    public enum ExpressionKind
    {
        True,
        False,
        Symbol,
        Not,
        And,
        Or,
        Equality,
        InEquality,
    }

    public abstract class Expression
    {
        readonly ISourceText _sourceText;

        protected ISourceText SourceText => _sourceText;
        public Token Token { get; }

        protected Expression(ISourceText sourceText, Token token)
        {
            _sourceText = sourceText;
            Token = token;
        }

        public abstract ExpressionKind Kind { get; }

        protected string TokenText => _sourceText.Substring(Token.StartOffset, Token.Length);
        protected char TokenChar(int offset) => _sourceText[Token.StartOffset + offset];
    }

    public class ConstExpression : Expression
    {
        public static ConstExpression True(ISourceText sourceText, Token token) =>
            new ConstExpression(true, sourceText, token);

        public static ConstExpression False(ISourceText sourceText, Token token) =>
            new ConstExpression(false, sourceText, token);

        public bool Value { get; }

        ConstExpression(bool value, ISourceText sourceText, Token token) :
            base(sourceText, token) => Value = value;

        public override ExpressionKind Kind => Value ? ExpressionKind.True : ExpressionKind.False;

        public override string ToString() => TokenText;
    }

    public class SymbolExpression : Expression
    {
        public string Name => TokenText;

        internal SymbolExpression(ISourceText sourceText, Token token) :
            base(sourceText, token) {}

        public override ExpressionKind Kind => ExpressionKind.Symbol;

        public override string ToString() => Name;
    }

    public class UnaryExpression : Expression
    {
        public Expression Operand { get; }

        internal UnaryExpression(ISourceText sourceText, Token token, Expression operand) :
            base(sourceText, token) => Operand = operand;

        public override ExpressionKind Kind => ExpressionKind.Not;

        public override string ToString() =>
            TokenText + Operand;

        internal UnaryExpression WithOperand(Expression value) =>
            value == Operand ? this : new UnaryExpression(SourceText, Token, value);
    }

    public class BinaryExpression : Expression
    {
        public Expression Left { get; }
        public Expression Right { get; }

        internal BinaryExpression(ISourceText sourceText, Token token, Expression left, Expression right) :
            base(sourceText, token)
        {
            Left = left;
            Right = right;
        }

        public override ExpressionKind Kind =>
            TokenChar(0) switch
            {
                '&' => ExpressionKind.And,
                '|' => ExpressionKind.Or,
                '=' => ExpressionKind.Equality,
                '!' => ExpressionKind.InEquality,
                _   => throw new NotSupportedException()
            };

        public override string ToString() =>
            string.Join(" ", Left, TokenText, Right);

        internal BinaryExpression WithLeft(Expression value) =>
            value == Right ? this : new BinaryExpression(SourceText, Token, value, Right);

        internal BinaryExpression WithRight (Expression value) =>
            value == Left ? this : new BinaryExpression(SourceText, Token, Left, value);
    }

    public static class ExpressionExtensions
    {
        public static bool Evaluate(this Expression expression, Func<string, bool> resolver)
        {
            switch (expression.Kind)
            {
                case ExpressionKind.True: return true;
                case ExpressionKind.False: return false;
                case ExpressionKind.Symbol:
                {
                    var e = (SymbolExpression)expression;
                    return resolver(e.Name);
                }
                case ExpressionKind.Not:
                {
                    var e = (UnaryExpression)expression;
                    return !Evaluate(e.Operand, resolver);
                }
                case ExpressionKind.And:
                {
                    var e = (BinaryExpression)expression;
                    return Evaluate(e.Left, resolver) && Evaluate(e.Right, resolver);
                }
                case ExpressionKind.Or:
                {
                    var e = (BinaryExpression)expression;
                    return Evaluate(e.Left, resolver) || Evaluate(e.Right, resolver);
                }
                case ExpressionKind.Equality:
                {
                    var e = (BinaryExpression)expression;
                    return Evaluate(e.Left, resolver) == Evaluate(e.Right, resolver);
                }
                case ExpressionKind.InEquality:
                {
                    var e = (BinaryExpression)expression;
                    return Evaluate(e.Left, resolver) != Evaluate(e.Right, resolver);
                }
                default:
                    throw new ArgumentException(null, nameof(expression));
            }
        }

        public static string Optimize(this Expression expression, Func<string, bool> resolver)
        {
            switch (expression.Kind)
            {
                case ExpressionKind.True: return "true";
                case ExpressionKind.False: return "false";
                case ExpressionKind.Symbol:
                {
                    var e = (SymbolExpression)expression;
                    return resolver(e.Name) ? "true" : e.Name;
                }
                case ExpressionKind.Not:
                {
                    var e = (UnaryExpression)expression;
                    return Optimize(e.Operand, resolver) switch
                    {
                        "true" => "false",
                        "false" => "true",
                        var op => $"(!{op})"
                    };
                }
                case ExpressionKind.And:
                {
                    var e = (BinaryExpression) expression;
                    return (Optimize(e.Left, resolver), Optimize(e.Right, resolver)) switch
                    {
                        ("true", var s) => s,
                        (var s, "true") => s,
                        ("false", _) => "false",
                        (_, "false") => "false",
                        var (l, r) => $"({l} && {r})"
                    };
                }
                case ExpressionKind.Or:
                {
                    var e = (BinaryExpression) expression;
                    return (Optimize(e.Left, resolver), Optimize(e.Right, resolver)) switch
                    {
                        ("true", "true") => "true",
                        ("true", "false") => "false",
                        ("false", "false") => "false",
                        ("false", "true") => "false",
                        ("true", var r) => r,
                        ("false", var r) => r,
                        (var l, "true") => l,
                        (var l, "false") => l,
                        var (l, r) => $"({l} || {r})"
                    };
                }
                case ExpressionKind.Equality:
                {
                    var e = (BinaryExpression) expression;
                    return (Optimize(e.Left, resolver), Optimize(e.Right, resolver)) switch
                    {
                        ("true", "true") => "true",
                        ("false", "false") => "true",
                        ("true", "false") => "false",
                        ("false", "true") => "false",
                        var (l, r) => $"({l} == {r})"
                    };
                }
                case ExpressionKind.InEquality:
                {
                    var e = (BinaryExpression) expression;
                    return (Optimize(e.Left, resolver), Optimize(e.Right, resolver)) switch
                    {
                        ("true", "true") => "false",
                        ("false", "false") => "false",
                        ("true", "false") => "true",
                        ("false", "true") => "true",
                        var (l, r) => $"({l} != {r})"
                    };
                }
                default:
                    throw new ArgumentException(null, nameof(expression));
            }
        }
    }

    public static class ExpressionParser
    {
        public static Expression Parse(string text) =>
            Parser.Parse(SourceText.FromString(text), 0,
                (kind, _, __) => Spec.Instance.Prefix(kind),
                (kind, _, __) => Spec.Instance.Infix(kind),
                from t in Scanner.Scan(text)
                where t.Kind != TokenKind.WhiteSpace
                select (t.Kind, t));

        static class Precedence
        {
            public const int Logical    = 30; // || &&
            public const int Relational = 40; // == !=
            public const int Prefix     = 70; // !
        }

        static class Parselets
        {
            public static readonly PrefixParselet Symbol =
                (token, parser) => new SymbolExpression(parser.State, token);

            public static readonly PrefixParselet TrueOrFalse =
                (token, parser) => token.Kind == TokenKind.True
                                 ? ConstExpression.True(parser.State, token)
                                 : ConstExpression.False(parser.State, token);

            public static readonly PrefixParselet Group =
                (token, parser) =>
                {
                    var expression = parser.Parse(0);
                    parser.Read(TokenKind.RParen, delegate { throw new ParseException("Expected ')'."); });
                    return expression;
                };

            public static PrefixParselet Unary(int precedence) =>
                (token, parser) => new UnaryExpression(parser.State, token, parser.Parse(precedence));

            public static InfixParselet Binary(int precedence) =>
                (token, left, parser) => new BinaryExpression(parser.State, token, left, parser.Parse(precedence));
        }

        static class Interplets
        {
            public static readonly PrefixParselet Symbol =
                (token, parser) => new SymbolExpression(parser.State, token);

            public static readonly PrefixParselet TrueOrFalse =
                (token, parser) => token.Kind == TokenKind.True
                                 ? ConstExpression.True(parser.State, token)
                                 : ConstExpression.False(parser.State, token);

            public static readonly PrefixParselet Group =
                (token, parser) =>
                {
                    var expression = parser.Parse(0);
                    parser.Read(TokenKind.RParen, delegate { throw new ParseException("Expected ')'."); });
                    return expression;
                };

            public static PrefixParselet Unary(int precedence) =>
                (token, parser) => new UnaryExpression(parser.State, token, parser.Parse(precedence));

            public static InfixParselet Binary(int precedence) =>
                (token, left, parser) => new BinaryExpression(parser.State, token, left, parser.Parse(precedence));
        }

        sealed class Spec : IEnumerable
        {
            public static readonly Spec Instance = new Spec
            {
                { TokenKind.Symbol            , Parselets.Symbol      },
                { TokenKind.True              , Parselets.TrueOrFalse },
                { TokenKind.False             , Parselets.TrueOrFalse },
                { TokenKind.LParen            , Parselets.Group       },
                { TokenKind.Bang              , Parselets.Unary(Precedence.Prefix) },
                { TokenKind.AmpersandAmpersand, Precedence.Logical   , Parselets.Binary(Precedence.Logical) },
                { TokenKind.PipePipe          , Precedence.Logical   , Parselets.Binary(Precedence.Logical) },
                { TokenKind.EqualEqual        , Precedence.Relational, Parselets.Binary(Precedence.Relational) },
                { TokenKind.BangEqual         , Precedence.Relational, Parselets.Binary(Precedence.Relational) },
            };

            readonly Dictionary<TokenKind, PrefixParselet> _prefixes = new Dictionary<TokenKind, PrefixParselet>();
            readonly Dictionary<TokenKind, (int, InfixParselet)> _infixes = new Dictionary<TokenKind, (int, InfixParselet)>();
            //readonly Dictionary<TokenKind, PrefixInterplet> _prefixes = new Dictionary<TokenKind, PrefixInterplet>();
            //readonly Dictionary<TokenKind, (int, InfixInterplet)> _infixes = new Dictionary<TokenKind, (int, InfixInterplet)>();

            Spec() { }

            void Add(TokenKind type, PrefixParselet prefix) =>
                _prefixes.Add(type, prefix);

            void Add(TokenKind type, int precedence, InfixParselet prefix) =>
                _infixes.Add(type, (precedence, prefix));

            public PrefixParselet Prefix(TokenKind type) => _prefixes[type];

            public (int, InfixParselet)? Infix(TokenKind type)
            {
                if (!_infixes.TryGetValue(type, out var v))
                    return default;
                var (precedence, infix) = v;
                return (precedence, infix);
            }

            IEnumerator IEnumerable.GetEnumerator() =>
                _prefixes.Cast<object>().Concat(_infixes.Cast<object>()).GetEnumerator();
        }
    }
}
