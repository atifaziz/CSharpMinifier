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
        public static IEnumerable<Token> Scan(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return ScanImpl(source);
        }

        enum State
        {
            NewLine,
            LeadingWhiteSpace,
            Text,
            WhiteSpace,
            Cr,
            Slash,
            SingleLineComment,
            MultiLineComment,
            MultiLineCommentStar,
            String,
            StringEscape,
            At,
            VerbatimString,
            VerbatimStringQuote,
            VerbatimStringCr,
            Dollar,
            InterpolatedString,
            InterpolatedStringEscape,
            InterpolatedStringBrace,
            DollarAt,
            InterpolatedVerbatimString,
            InterpolatedVerbatimStringQuote,
            InterpolatedVerbatimStringBrace,
            InterpolatedVerbatimStringCr,
            Char,
            CharEscape,
            PreprocessorDirective,
            PreprocessorDirectiveSlash,
            PreprocessorDirectiveTrailingWhiteSpace,
            PreprocessorDirectiveTrailingWhiteSpaceSlash,
        }

        static IEnumerable<Token> ScanImpl(string source)
        {
            var state = State.NewLine;
            var si = 0;
            var pos = (Line: 1, Col: 0);
            var spos = (Line: 1, Col: 1);
            var ppdtwssi = -1;
            var ppdtwscol = 0;
            int i;
            var interpolated = new Stack<(bool Verbatim, int Parens)>();

            bool Interpolated() => interpolated.Count > 0;
            int Parens() => interpolated.Peek().Parens;
            int IncParens(int step = 1)
            {
                var (verbatim, parens) = interpolated.Pop();
                interpolated.Push((verbatim, parens + step));
                return parens;
            }

            T TransitReturn<T>(State newState, int offset, T token)
            {
                si = i + offset; spos = (pos.Line, pos.Col + offset);
                state = newState;
                return token;
            }

            Token CreateToken(TokenKind kind, int offset = 0) =>
                new Token(kind, new Position(si, spos.Line, spos.Col),
                                new Position(i + offset, pos.Line, pos.Col + offset));

            Token? TextTransit(State newState, int offset = 0) =>
                TransitReturn(newState, offset,
                              i + offset - si > 0
                              ? CreateToken(TokenKind.Text, offset)
                              : (Token?)null);

            Token Transit(TokenKind kind, State newState, int offset = 0) =>
                TransitReturn(newState, offset, CreateToken(kind, offset));

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
                    case State.NewLine:
                    {
                        switch (ch)
                        {
                            case ' ':
                            case '\t':
                                state = State.LeadingWhiteSpace;
                                break;
                            case '#':
                                state = State.PreprocessorDirective;
                                break;
                            default:
                                state = State.Text;
                                goto restart;
                        }
                        break;
                    }
                    case State.LeadingWhiteSpace:
                    {
                        switch (ch)
                        {
                            case ' ':
                            case '\t':
                                break;
                            case '#':
                                yield return Transit(TokenKind.WhiteSpace, State.PreprocessorDirective);
                                break;
                            default:
                                yield return Transit(TokenKind.WhiteSpace, State.Text);
                                goto restart;
                        }
                        break;
                    }
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
                                var (verbatim, parens) = interpolated.Pop();
                                if (TextTransit(verbatim ? State.InterpolatedVerbatimString : State.InterpolatedString) is Token text)
                                    yield return text;
                                if (parens != 0)
                                    throw SyntaxError("Parentheses mismatch in interpolated string expression.");
                                break;
                            }
                            case ' ':
                            case '\t':
                            {
                                if (TextTransit(State.WhiteSpace) is Token text)
                                    yield return text;
                                break;
                            }
                            case '\r':
                            {
                                if (TextTransit(State.Cr) is Token text)
                                    yield return text;
                                break;
                            }
                            case '\n':
                            {
                                if (TextTransit(State.Text) is Token text)
                                    yield return text;
                                pos = (pos.Line + 1, 0);
                                yield return Transit(TokenKind.NewLine, State.NewLine, 1);
                                break;
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
                            default:
                                yield return Transit(TokenKind.WhiteSpace, State.Text);
                                goto restart;
                        }
                        break;
                    }
                    case State.Cr:
                    {
                        switch (ch)
                        {
                            case '\r':
                                pos = (pos.Line + 1, 1);
                                yield return Transit(TokenKind.NewLine, State.Cr);
                                break;
                            case '\n':
                                pos = (pos.Line + 1, 0);
                                yield return Transit(TokenKind.NewLine, State.NewLine, 1);
                                break;
                            default:
                                pos = (pos.Line + 1, 1);
                                yield return Transit(TokenKind.NewLine, State.NewLine);
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
                            case ' ':
                            case '\t':
                                ppdtwssi = i;
                                ppdtwscol = pos.Col;
                                state = State.PreprocessorDirectiveTrailingWhiteSpace;
                                break;
                            case '\r':
                            case '\n':
                                yield return Transit(TokenKind.PreprocessorDirective, State.Text);
                                goto restart;
                        }
                        break;
                    }
                    case State.PreprocessorDirectiveSlash:
                    {
                        if (ch == '/')
                        {
                            yield return Transit(TokenKind.PreprocessorDirective, State.SingleLineComment, -1);
                        }
                        else
                        {
                            state = State.PreprocessorDirective;
                            goto restart;
                        }
                        break;
                    }
                    case State.PreprocessorDirectiveTrailingWhiteSpaceSlash:
                    {
                        if (ch == '/')
                        {
                            yield return CreateToken(TokenKind.PreprocessorDirective, ppdtwscol - pos.Col);
                            si = ppdtwssi; spos.Col = ppdtwscol;
                            yield return Transit(TokenKind.WhiteSpace, State.SingleLineComment, -1);
                        }
                        else
                        {
                            state = State.PreprocessorDirective;
                            goto restart;
                        }
                        break;
                    }
                    case State.PreprocessorDirectiveTrailingWhiteSpace:
                    {
                        switch (ch)
                        {
                            case ' ':
                            case '\t':
                                break;
                            case '\r':
                            case '\n':
                                yield return CreateToken(TokenKind.PreprocessorDirective, ppdtwscol - pos.Col);
                                si = ppdtwssi; spos.Col = ppdtwscol;
                                yield return Transit(TokenKind.WhiteSpace, State.Text);
                                goto restart;
                            case '/':
                                state = State.PreprocessorDirectiveTrailingWhiteSpaceSlash;
                                break;
                            default:
                                state = State.PreprocessorDirective;
                                goto restart;
                        }
                        break;
                    }
                    case State.At:
                    {
                        if (ch == '"')
                        {
                            if (TextTransit(State.VerbatimString, -1) is Token text)
                                yield return text;
                        }
                        else
                        {
                            state = State.Text;
                            goto restart;
                        }
                        break;
                    }
                    case State.Dollar:
                    {
                        switch (ch)
                        {
                            case '@':
                                state = State.DollarAt;
                                break;
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
                                yield return Transit(source[si] == '$'
                                                     ? TokenKind.InterpolatedStringLiteral
                                                     : TokenKind.InterpolatedStringLiteralEnd,
                                                     State.Text, 1);
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
                        state = State.InterpolatedString;
                        break;
                    }
                    case State.InterpolatedStringBrace:
                    {
                        if (ch == '{')
                        {
                            state = State.InterpolatedString;
                        }
                        else
                        {
                            yield return Transit(source[si] == '$'
                                                 ? TokenKind.InterpolatedStringLiteralStart
                                                 : TokenKind.InterpolatedStringLiteralMid,
                                                 State.Text);
                            interpolated.Push((false, 0));
                            goto restart;
                        }
                        break;
                    }
                    case State.DollarAt:
                    {
                        if (ch == '"')
                        {
                            if (TextTransit(State.InterpolatedVerbatimString, -2) is Token text)
                                yield return text;
                        }
                        else
                        {
                            state = State.Text;
                        }
                        break;
                    }
                    case State.InterpolatedVerbatimString:
                    {
                        switch (ch)
                        {
                            case '"':
                                state = State.InterpolatedVerbatimStringQuote;
                                break;
                            case '{':
                                state = State.InterpolatedVerbatimStringBrace;
                                break;
                            case '\n':
                                pos = (pos.Line + 1, 0);
                                break;
                            case '\r':
                                state = State.InterpolatedVerbatimStringCr;
                                break;
                        }
                        break;
                    }
                    case State.InterpolatedVerbatimStringQuote:
                    {
                        if (ch == '"')
                        {
                            state = State.InterpolatedVerbatimString;
                        }
                        else
                        {
                            yield return Transit(source[si] == '$'
                                                 ? TokenKind.InterpolatedVerbatimStringLiteral
                                                 : TokenKind.InterpolatedVerbatimStringLiteralEnd,
                                                 State.Text);
                            goto restart;
                        }
                        break;
                    }
                    case State.InterpolatedVerbatimStringBrace:
                    {
                        if (ch == '{')
                        {
                            state = State.InterpolatedVerbatimString;
                        }
                        else
                        {
                            yield return Transit(source[si] == '$'
                                                 ? TokenKind.InterpolatedVerbatimStringLiteralStart
                                                 : TokenKind.InterpolatedVerbatimStringLiteralMid,
                                                 State.Text);
                            interpolated.Push((true, 0));
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
                            case '\n':
                                pos = (pos.Line + 1, 0);
                                break;
                            case '\r':
                                state = State.VerbatimStringCr;
                                break;
                        }
                        break;
                    }
                    case State.VerbatimStringQuote:
                    {
                        if (ch == '"')
                        {
                            state = State.VerbatimString;
                        }
                        else
                        {
                            yield return Transit(TokenKind.VerbatimStringLiteral, State.Text);
                            goto restart;
                        }
                        break;
                    }
                    case State.InterpolatedVerbatimStringCr:
                    case State.VerbatimStringCr:
                    {
                        if (ch != '\n')
                            pos = (pos.Line + 1, ch == '\r' ? 0 : 1);
                        state = state == State.InterpolatedVerbatimStringCr
                              ? State.InterpolatedVerbatimString
                              : State.VerbatimString;
                        goto restart;
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
                        if (ch == '\r' || ch == '\n')
                        {
                            yield return Transit(TokenKind.SingleLineComment, State.Text);
                            goto restart;
                        }
                        break;
                    }
                    case State.MultiLineComment:
                    {
                        if (ch == '*')
                            state = State.MultiLineCommentStar;
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
                case State.VerbatimStringCr:
                case State.InterpolatedString:
                case State.InterpolatedStringEscape:
                case State.InterpolatedStringBrace:
                case State.InterpolatedVerbatimString:
                case State.InterpolatedVerbatimStringBrace:
                case State.InterpolatedVerbatimStringCr:
                    throw SyntaxError("Unterminated string starting.");
                case State.Char:
                    throw SyntaxError("Unterminated character literal.");
                case State.MultiLineComment:
                case State.MultiLineCommentStar:
                    throw SyntaxError("Unterminated multi-line comment");
                default:
                {
                    if (state == State.Cr)
                        pos = (pos.Line + 1, 0);

                    if (si < source.Length)
                    {
                        pos.Col++;

                        if (state == State.PreprocessorDirectiveTrailingWhiteSpace)
                        {
                            yield return CreateToken(TokenKind.PreprocessorDirective, ppdtwscol - pos.Col);
                            si = ppdtwssi; spos.Col = ppdtwscol;
                            yield return CreateToken(TokenKind.WhiteSpace);
                        }
                        else
                        {
                            var token
                                = state == State.SingleLineComment ? TokenKind.SingleLineComment
                                : state == State.WhiteSpace || state == State.LeadingWhiteSpace ? TokenKind.WhiteSpace
                                : state == State.Cr ? TokenKind.NewLine
                                : state == State.PreprocessorDirective || state == State.PreprocessorDirectiveSlash ? TokenKind.PreprocessorDirective
                                : state == State.VerbatimStringQuote ? TokenKind.VerbatimStringLiteral
                                : state == State.PreprocessorDirectiveTrailingWhiteSpaceSlash ? TokenKind.PreprocessorDirective
                                : state == State.InterpolatedVerbatimStringQuote
                                  ? source[si] == '$' ? TokenKind.InterpolatedVerbatimStringLiteral
                                  : TokenKind.InterpolatedVerbatimStringLiteralEnd
                                : TokenKind.Text;

                            yield return CreateToken(token);
                        }
                    }
                    break;
                }
            }
        }

        public static IEnumerable<string> ParseStrings(string source) =>
            CSharpString.ParseValues(Scan(source), source);

        public static IEnumerable<T>
                ParseStrings<T>(string source,
                                Func<Token, string, string, T> selector) =>
            CSharpString.ParseValues(Scan(source), source, selector);

        public static IEnumerable<Region> ScanRegions(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return _(); IEnumerable<Region> _()
            {
                var tokens = (List<Token>)null;
                var level = 0;
                var awaitingEndRegionLineEnding = false;
                var lwsToken = (Token?)null;
                var startMessage = (string)null;
                var endMessage = (string)null;

                foreach (var token in Scan(source))
                {
                    switch (token.Kind)
                    {
                        case TokenKind.WhiteSpace when level == 0:
                            lwsToken = token;
                            break;

                        case TokenKind.PreprocessorDirective:
                            var (name, specifics) = SplitName(token);
                            switch (name)
                            {
                                case "region":
                                    if (level == 0)
                                    {
                                        startMessage = specifics;
                                        tokens = new List<Token>();
                                        if (lwsToken is Token t)
                                            tokens.Add(t);
                                    }
                                    level++;
                                    break;

                                case "endregion":
                                    level--;
                                    if (level == 0)
                                    {
                                        awaitingEndRegionLineEnding = true;
                                        endMessage = specifics;
                                    }
                                    break;
                            }
                            break;
                    }

                    if (tokens != null)
                    {
                        tokens.Add(token);

                        if (token.Kind == TokenKind.NewLine && awaitingEndRegionLineEnding)
                        {
                            awaitingEndRegionLineEnding = false;
                            yield return new Region(startMessage, endMessage, tokens);
                            tokens = null;
                        }
                    }
                }

                if (tokens != null && awaitingEndRegionLineEnding)
                    yield return new Region(startMessage, endMessage, tokens);

                (string, string) SplitName(Token token)
                {
                    var parts = source.Slice(token.Start.Offset + 1, token.End.Offset)
                                      .TrimStart()
                                      .Split(Minifier.SpaceOrTab, 2);
                    return (parts[0], parts.Length > 1 ? parts[1].Trim() : string.Empty);
                }
            }
        }
    }
}
