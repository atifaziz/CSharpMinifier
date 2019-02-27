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

namespace CSharpMinifier
{
    using System;
    using System.Collections.Generic;

    public static class Scanner
    {
        enum State
        {
            Text,
            WhiteSpace,
            WhiteSpaceCr,
            Slash,
            SingleLineComment,
            MultiLineComment,
            MultiLineCommentStar,
            String,
            StringEscape,
            At,
            VerbatimString,
            VerbatimStringQuote,
            Dollar,
            InterpolatedString,
            InterpolatedStringEscape,
            InterpolatedStringBrace,
            Char,
            CharEscape,
            PreprocessorDirective,
            PreprocessorDirectiveSlash,
        }

        public static IEnumerable<Token> Scan(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return ScanImpl(source);
        }

        static IEnumerable<Token> ScanImpl(string source)
        {
            var state = State.Text;
            var si = 0;
            var pos = (Line: 1, Col: 0);
            var spos = (Line: 1, Col: 1);
            int i;
            var interpolated = new Stack<int>();

            bool Interpolated() => interpolated.Count > 0;
            int Parens() => interpolated.Peek();
            int IncParens(int step = 1)
            {
                var parens = interpolated.Pop();
                interpolated.Push(parens + step);
                return parens;
            }

            Token? TextTransit(State newState, int offset = 0)
            {
                var token = i + offset - si > 0
                          ? new Token(TokenKind.Text, new Position(si, spos.Line, spos.Col),
                                                      new Position(i + offset, pos.Line, pos.Col + offset))
                          : (Token?)null;
                si = i + offset; spos = (pos.Line, pos.Col + offset);
                state = newState;
                return token;
            }

            Token Transit(TokenKind kind, State newState, int offset = 0)
            {
                var token = new Token(kind, new Position(si, spos.Line, spos.Col),
                                            new Position(i + offset, pos.Line, pos.Col + offset));
                si = i + offset; spos = (pos.Line, pos.Col + offset);
                state = newState;
                return token;
            }

            Exception SyntaxError(string message) =>
                throw new SyntaxErrorException($"{message} The syntax error is at line {pos.Line} and column {pos.Col} (or offset {i}). The last anchor was at line {spos.Line} and column {spos.Col} (or offset {si})");

            //
            // While the C# language specification defines the following line
            // terminators:
            //
            // new_line
            //     : '<Carriage return character (U+000D)>'
            //     | '<Line feed character (U+000A)>'
            //     | '<Carriage return character (U+000D) followed by line feed character (U+000A)>'
            //     | '<Next line character (U+0085)>'
            //     | '<Line separator character (U+2028)>'
            //     | '<Paragraph separator character (U+2029)>'
            //     ;
            //
            // Source: https://github.com/dotnet/csharplang/blob/master/spec/lexical-structure.md#line-terminators
            //
            // we don't support the last three cases (yet), assuming they are
            // odd and rare.
            //

            for (i = 0; i < source.Length; i++)
            {
                var ch = source[i];
                pos.Col++;
                restart:
                switch (state)
                {
                    case State.Text:
                    {
                        switch (ch)
                        {
                            case '/':
                                state = State.Slash;
                                break;
                            case '"':
                            {
                                if (TextTransit(State.String) is Token text)
                                    yield return text;
                                break;
                            }
                            case '\'':
                            {
                                if (TextTransit(State.Char) is Token text)
                                    yield return text;
                                break;
                            }
                            case '@':
                                state = State.At;
                                break;
                            case '$':
                                state = State.Dollar;
                                break;
                            case '(' when Interpolated():
                                IncParens();
                                break;
                            case ')' when Interpolated():
                                if (IncParens(-1) == 0)
                                    throw SyntaxError("Parentheses mismatch in interpolated string expression.");
                                break;
                            case ',' when Interpolated() && Parens() == 0:
                            case ':' when Interpolated() && Parens() == 0:
                            case '}' when Interpolated():
                            {
                                if (TextTransit(State.InterpolatedString) is Token text)
                                    yield return text;
                                if (interpolated.Pop() != 0)
                                    throw SyntaxError("Parentheses mismatch in interpolated string expression.");
                                break;
                            }
                            case '#' when si == 0:
                                state = State.PreprocessorDirective;
                                goto restart;
                            case ' ':
                            case '\t':
                            case '\r':
                            case '\n':
                            {
                                if (TextTransit(State.WhiteSpace) is Token text)
                                    yield return text;
                                goto restart;
                            }
                        }
                        break;
                    }
                    case State.WhiteSpace:
                    {
                        switch (ch)
                        {
                            case ' ':
                            case '\t':
                                break;
                            case '\r':
                                state = State.WhiteSpaceCr;
                                break;
                            case '\n':
                                pos = (pos.Line + 1, 0);
                                break;
                            default:
                                yield return Transit(TokenKind.WhiteSpace,
                                                     ch == '#' ? State.PreprocessorDirective : State.Text);
                                goto restart;
                        }
                        break;
                    }
                    case State.WhiteSpaceCr:
                    {
                        switch (ch)
                        {
                            case ' ':
                            case '\t':
                                pos = (pos.Line + 1, 1);
                                state = State.WhiteSpace;
                                goto restart;
                            case '\r':
                                pos = (pos.Line + 1, 0);
                                break;
                            case '\n':
                                state = State.WhiteSpace;
                                goto restart;
                            default:
                                pos = (pos.Line + 1, 1);
                                yield return Transit(TokenKind.WhiteSpace,
                                                     ch == '#' ? State.PreprocessorDirective : State.Text);
                                goto restart;
                        }
                        break;
                    }
                    case State.PreprocessorDirective:
                    {
                        switch (ch)
                        {
                            case '/':
                                state = State.PreprocessorDirectiveSlash;
                                break;
                            case '\r':
                            case '\n':
                                yield return Transit(TokenKind.PreprocessorDirective, State.WhiteSpace);
                                goto restart;
                        }
                        break;
                    }
                    case State.PreprocessorDirectiveSlash:
                    {
                        switch (ch)
                        {
                            case '/':
                                yield return Transit(TokenKind.PreprocessorDirective, State.SingleLineComment, -1);
                                break;
                            case '\r':
                            case '\n':
                                state = State.PreprocessorDirective;
                                goto restart;
                        }
                        break;
                    }
                    case State.At:
                    {
                        switch (ch)
                        {
                            case '"':
                                if (TextTransit(State.VerbatimString, -1) is Token text)
                                    yield return text;
                                break;
                            default:
                                state = State.Text;
                                goto restart;
                        }
                        break;
                    }
                    case State.Dollar:
                    {
                        switch (ch)
                        {
                            case '"':
                                if (TextTransit(State.InterpolatedString, -1) is Token text)
                                    yield return text;
                                break;
                            default:
                                state = State.Text;
                                goto restart;
                        }
                        break;
                    }
                    case State.InterpolatedString:
                    {
                        switch (ch)
                        {
                            case '"':
                                yield return Transit(TokenKind.InterpolatedStringLiteral, State.Text, 1);
                                break;
                            case '\\':
                                state = State.InterpolatedStringEscape;
                                break;
                            case '{':
                                state = State.InterpolatedStringBrace;
                                break;
                            case '\r':
                            case '\n':
                                throw SyntaxError("Unterminated interpolated string.");
                        }
                        break;
                    }
                    case State.InterpolatedStringEscape:
                    {
                        // TODO handle unterminated
                        state = State.InterpolatedString;
                        break;
                    }
                    case State.InterpolatedStringBrace:
                    {
                        switch (ch)
                        {
                            case '{':
                                state = State.InterpolatedString;
                                break;
                            default:
                                yield return Transit(TokenKind.InterpolatedStringLiteral, State.Text);
                                interpolated.Push(0);
                                goto restart;
                        }
                        break;
                    }
                    case State.String:
                    {
                        switch (ch)
                        {
                            case '"':
                                yield return Transit(TokenKind.StringLiteral, State.Text, 1);
                                break;
                            case '\\':
                                state = State.StringEscape;
                                break;
                            case '\r':
                            case '\n':
                                throw SyntaxError("Unterminated string.");
                        }
                        break;
                    }
                    case State.StringEscape:
                    {
                        // TODO handle unterminated
                        state = State.String;
                        break;
                    }
                    case State.Char:
                    {
                        switch (ch)
                        {
                            case '\'':
                                yield return Transit(TokenKind.CharLiteral, State.Text, 1);
                                break;
                            case '\\':
                                state = State.CharEscape;
                                break;
                            case '\r':
                            case '\n':
                                throw SyntaxError("Unterminated character.");
                        }
                        break;
                    }
                    case State.CharEscape:
                    {
                        // TODO handle unterminated
                        state = State.Char;
                        break;
                    }
                    case State.VerbatimString:
                    {
                        switch (ch)
                        {
                            case '"':
                                state = State.VerbatimStringQuote;
                                break;
                        }
                        break;
                    }
                    case State.VerbatimStringQuote:
                    {
                        switch (ch)
                        {
                            case '"':
                                state = State.VerbatimString;
                                break;
                            default:
                                yield return Transit(TokenKind.VerbatimStringLiteral, State.Text);
                                goto restart;
                        }
                        break;
                    }
                    case State.Slash:
                    {
                        switch (ch)
                        {
                            case '/':
                            case '*':
                                if (TextTransit(ch == '/' ? State.SingleLineComment : State.MultiLineComment, -1) is Token text)
                                    yield return text;
                                break;
                            default:
                                state = State.Text;
                                goto restart;
                        }
                        break;
                    }
                    case State.SingleLineComment:
                    {
                        switch (ch)
                        {
                            case '\r':
                            case '\n':
                                yield return Transit(TokenKind.SingleLineComment, State.Text);
                                goto restart;
                        }
                        break;
                    }
                    case State.MultiLineComment:
                    {
                        switch (ch)
                        {
                            case '*':
                                state = State.MultiLineCommentStar;
                                break;
                        }
                        break;
                    }
                    case State.MultiLineCommentStar:
                    {
                        switch (ch)
                        {
                            case '/':
                                yield return Transit(TokenKind.MultiLineComment, State.Text, 1);
                                break;
                            case '*':
                                break;
                            default:
                                state = State.MultiLineComment;
                                break;
                        }
                        break;
                    }
                }
            }

            switch (state)
            {
                case State.String:
                case State.StringEscape:
                case State.VerbatimString:
                case State.InterpolatedString:
                case State.InterpolatedStringEscape:
                case State.InterpolatedStringBrace:
                    throw SyntaxError("Unterminated string starting.");
                case State.Char:
                    throw SyntaxError("Unterminated character literal.");
                case State.MultiLineComment:
                case State.MultiLineCommentStar:
                    throw SyntaxError("Unterminated multi-line comment");
                default:
                {
                    if (state == State.WhiteSpaceCr)
                        pos = (pos.Line + 1, 0);

                    if (si < source.Length)
                    {
                        var token = state == State.SingleLineComment ? TokenKind.SingleLineComment
                                  : state == State.WhiteSpace || state == State.WhiteSpaceCr ? TokenKind.WhiteSpace
                                  : state == State.PreprocessorDirective || state == State.PreprocessorDirectiveSlash ? TokenKind.PreprocessorDirective
                                  : state == State.VerbatimStringQuote ? TokenKind.VerbatimStringLiteral
                                  : TokenKind.Text;
                        pos = (pos.Line, pos.Col + 1);
                        yield return Transit(token, State.Text);
                    }
                    break;
                }
            }
        }
    }
}
