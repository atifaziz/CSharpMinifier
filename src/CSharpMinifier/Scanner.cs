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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CSharpMinifier;

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
        MultiLineCommentCr,
        String,
        StringEscape,
        At,
        VerbatimString,
        VerbatimStringQuote,
        VerbatimStringCr,
        Quote,
        QuoteQuote,
        Dollar,
        DollarDollar,
        DollarQuote,
        DollarQuoteQuote,
        InterpolatedString,
        InterpolatedStringEscape,
        InterpolatedStringBrace,
        DollarAt,
        AtDollar,
        InterpolatedVerbatimString,
        InterpolatedVerbatimStringQuote,
        InterpolatedVerbatimStringBrace,
        InterpolatedVerbatimStringCr,
        RawString,
        RawStringOpening,
        RawStringClosing,
        RawStringCr,
        InterpolatedRawString,
        InterpolatedRawStringOpening,
        InterpolatedRawStringClosing,
        InterpolatedRawStringBrace,
        InterpolatedRawStringCr,
        Char,
        CharEscape,
        PreprocessorDirective,
        PreprocessorDirectiveSlash,
        PreprocessorDirectiveTrailingWhiteSpace,
        PreprocessorDirectiveTrailingWhiteSpaceSlash,
    }

    enum InterpolatedStringKind { None, Regular, Verbatim, Raw }

    struct InterpolationState(InterpolatedStringKind kind)
    {
        public InterpolatedStringKind InterpolatedStringKind { get; } = kind;

        public readonly bool IsSome => InterpolatedStringKind is not InterpolatedStringKind.None;

        public int Parentheses { get; set;  }
        public int Braces      { get; set;  }
        public int Brackets    { get; set;  }
        public int Dollars     { get; init; } // $ count before opening quotes of interpolated raw strings
        public int Quotes      { get; init; } // opening quote count for raw strings
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
        var interpolationState = new InterpolationState();
        var interpolationStateStack = new Stack<InterpolationState>();
        var dollars = 0;
        var quotesOrBraces = 0;  // For counting consecutive quotes or braces during scanning
        var rawQuoteCountdown = 0;

        T TransitReturn<T>(State newState, int offset, T token)
        {
            si = i + offset; spos = (pos.Line, pos.Col + offset);
            state = newState;
            return token;
        }

        Token CreateToken(TokenKind kind, int offset = 0) =>
            new(kind, new Position(si, spos.Line, spos.Col),
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

        static bool IsDollarOrAt(char ch) => ch is '$' or '@';

        Token? EnterInterpolation(InterpolationState newState, int offset)
        {
            var scannerState = newState.InterpolatedStringKind switch
            {
                InterpolatedStringKind.Regular => State.InterpolatedString,
                InterpolatedStringKind.Verbatim => State.InterpolatedVerbatimString,
                InterpolatedStringKind.Raw => State.InterpolatedRawString,
                InterpolatedStringKind.None or _ => throw new UnreachableException(),
            };
            var text = TextTransit(scannerState, offset);
            if (interpolationState.IsSome)
                interpolationStateStack.Push(interpolationState);
            interpolationState = newState;
            dollars = quotesOrBraces = 0;
            return text;
        }

        Token ExitInterpolation(TokenKind tokenKind, int offset = 0)
        {
            Debug.Assert(interpolationState.IsSome);

            var token = Transit(tokenKind, State.Text, offset);
            interpolationState = interpolationStateStack.Count > 0 ? interpolationStateStack.Pop() : new();
            return token;
        }

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
#pragma warning disable IDE0010 // Add missing cases (default continue)
                    switch (ch, interpolationState)
#pragma warning restore IDE0010 // Add missing cases
                    {
                        case ('/', _):
                            state = State.Slash;
                            break;
                        case ('"', _):
                        {
                            state = State.Quote;
                            break;
                        }
                        case ('\'', _):
                        {
                            if (TextTransit(State.Char) is {} text)
                                yield return text;
                            break;
                        }
                        case ('@', _):
                            state = State.At;
                            break;
                        case ('$', _):
                            state = State.Dollar;
                            break;
                        case ('(', { IsSome: true }):
                            interpolationState.Parentheses++;
                            break;
                        case (')', { IsSome: true, Parentheses: 0 }):
                            throw SyntaxError("Parentheses mismatch in interpolated string expression.");
                        case (')', { IsSome: true }):
                            interpolationState.Parentheses--;
                            break;
                        case ('[', { IsSome: true }):
                            interpolationState.Brackets++;
                            break;
                        case (']', { IsSome: true, Brackets: 0 }):
                            throw SyntaxError("Brackets mismatch in interpolated string expression.");
                        case (']', { IsSome: true }):
                            interpolationState.Brackets--;
                            break;
                        case ('{', { IsSome: true }):
                            interpolationState.Braces++;
                            break;
                        case ('}', { IsSome: true, Braces: > 0 }):
                            interpolationState.Braces--;
                            break;
                        case (',' or ':', { IsSome: true, Parentheses: 0, Braces: 0, Brackets: 0 }):
                        case ('}', { IsSome: true }) :
                        {
                            var newState = interpolationState.InterpolatedStringKind switch
                            {
                                InterpolatedStringKind.Verbatim => State.InterpolatedVerbatimString,
                                InterpolatedStringKind.Raw => State.InterpolatedRawString,
                                InterpolatedStringKind.Regular => State.InterpolatedString,
                                InterpolatedStringKind.None or _ => throw new UnreachableException(),
                            };

                            if (TextTransit(newState) is {} text)
                                yield return text;

                            if (interpolationState switch
                                {
                                    { Parentheses: > 0 } => "Parentheses mismatch in interpolated string expression.",
                                    { Braces     : > 0 } => "Braces mismatch in interpolated string expression.",
                                    { Brackets   : > 0 } => "Brackets mismatch in interpolated string expression.",
                                    _ => null
                                } is {} message)
                            {
                                throw SyntaxError(message);
                            }

                            break;
                        }
                        case (' ', _):
                        case ('\t', _):
                        {
                            if (TextTransit(State.WhiteSpace) is {} text)
                                yield return text;
                            break;
                        }
                        case ('\r', _):
                        {
                            if (TextTransit(State.Cr) is {} text)
                                yield return text;
                            break;
                        }
                        case ('\n', _):
                        {
                            if (TextTransit(State.Text) is {} text)
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
#pragma warning disable IDE0010 // Add missing cases (default continue)
                    switch (ch)
#pragma warning restore IDE0010 // Add missing cases
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
                    switch (ch)
                    {
                        case '"':
                            if (TextTransit(State.VerbatimString, -1) is {} text)
                                yield return text;
                            break;
                        case '$':
                            state = State.AtDollar;
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
                        case '@':
                            state = State.DollarAt;
                            break;
                        case '"':
                            // Could be regular interpolated string or interpolated raw string so
                            // wait to see if next char is also a quote.
                            state = State.DollarQuote;
                            break;
                        case '$':
                            // Multiple dollar signs so track count for potential raw string
                            // interpolation.
                            dollars = 2;
                            state = State.DollarDollar;
                            break;
                        default:
                            state = State.Text;
                            goto restart;
                    }
                    break;
                }
                case State.DollarDollar:
                {
                    switch (ch)
                    {
                        case '$':
                            dollars++;
                            break;
                        case '"':
                            quotesOrBraces = 1;
                            state = State.InterpolatedRawStringOpening;
                            break;
                        default:
                            state = State.Text;
                            goto restart;
                    }
                    break;
                }
                case State.DollarQuote:
                {
                    if (ch == '"')
                    {
                        // Seen `$""`, but that's not enough to know if it's an empty interpolated
                        // string or the start of a raw string, so defer decision until the next
                        // char is seen.
                        state = State.DollarQuoteQuote;
                    }
                    else
                    {
                        if (EnterInterpolation(new(InterpolatedStringKind.Regular), -2) is {} text)
                            yield return text;
                        goto restart;
                    }
                    break;
                }
                case State.DollarQuoteQuote:
                {
                    if (ch == '"') // 3rd quote; mark the start of an interpolated raw string
                    {
                        dollars = 1;
                        quotesOrBraces = 3;
                        state = State.InterpolatedRawStringOpening;
                    }
                    else // Regular interpolated string that's empty, i.e.: $""
                    {
                        if (TextTransit(State.Text, -3) is {} text)
                            yield return text;
                        yield return Transit(TokenKind.InterpolatedStringLiteral, State.Text);
                        goto restart;
                    }
                    break;
                }
                case State.InterpolatedString:
                {
#pragma warning disable IDE0010 // Add missing cases (default continue)
                    switch (ch)
#pragma warning disable IDE0010 // Add missing cases (default continue)
                    {
                        case '"':
                            yield return
                                ExitInterpolation(source[si] == '$'
                                                  ? TokenKind.InterpolatedStringLiteral
                                                  : TokenKind.InterpolatedStringLiteralEnd, 1);
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
                        goto restart;
                    }
                    break;
                }
                case State.AtDollar:
                case State.DollarAt:
                {
                    if (ch == '"')
                    {
                        if (EnterInterpolation(new(InterpolatedStringKind.Verbatim), -2) is {} text)
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
                        yield return
                            ExitInterpolation(IsDollarOrAt(source[si])
                                              ? TokenKind.InterpolatedVerbatimStringLiteral
                                              : TokenKind.InterpolatedVerbatimStringLiteralEnd);
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
                        yield return Transit(IsDollarOrAt(source[si])
                                             ? TokenKind.InterpolatedVerbatimStringLiteralStart
                                             : TokenKind.InterpolatedVerbatimStringLiteralMid,
                                             State.Text);
                        goto restart;
                    }
                    break;
                }
                case State.Quote:
                {
                    if (ch == '"')
                    {
                        state = State.QuoteQuote;
                    }
                    else
                    {
                        if (TextTransit(State.String, -1) is {} text)
                            yield return text;
                    }
                    break;
                }
                case State.QuoteQuote:
                {
                    if (ch == '"')
                    {
                        quotesOrBraces = 3;
                        state = State.RawStringOpening;
                    }
                    else // was an empty string literal ""
                    {
                        if (TextTransit(State.String, -2) is {} text)
                            yield return text;
                        yield return Transit(TokenKind.StringLiteral, State.Text);
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
                            if (TextTransit(ch == '/' ? State.SingleLineComment : State.MultiLineComment, -1) is {} text)
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
                    if (ch is '\r' or '\n')
                    {
                        yield return Transit(TokenKind.SingleLineComment, State.Text);
                        goto restart;
                    }
                    break;
                }
                case State.MultiLineComment:
                {
#pragma warning disable IDE0010 // Add missing cases (default break)
                    switch (ch)
#pragma warning restore IDE0010 // Add missing cases
                    {
                        case '*':
                            state = State.MultiLineCommentStar;
                            break;
                        case '\n':
                            pos = (pos.Line + 1, 0);
                            break;
                        case '\r':
                            state = State.MultiLineCommentCr;
                            break;
                    }
                    break;
                }
                case State.MultiLineCommentCr:
                    if (ch != '\n')
                        pos = (pos.Line + 1, ch == '\r' ? 0 : 1);
                    state = State.MultiLineComment;
                    goto restart;
                case State.MultiLineCommentStar:
                {
                    switch (ch)
                    {
                        case '/':
                            yield return Transit(TokenKind.MultiLineComment, State.Text, 1);
                            break;
                        case '*':
                            break;
                        case '\r':
                            state = State.MultiLineCommentCr;
                            break;
                        case '\n':
                            pos = (pos.Line + 1, 0);
                            break;
                        default:
                            state = State.MultiLineComment;
                            break;
                    }
                    break;
                }
                case State.RawStringOpening:
                {
                    if (ch == '"')
                    {
                        quotesOrBraces++;
                    }
                    else
                    {
                        if (TextTransit(State.RawString, -quotesOrBraces) is {} text)
                            yield return text;
                        goto restart;
                    }
                    break;
                }
                case State.RawString:
                {
                    switch (ch)
                    {
                        case '"': // Start counting (down) closing quotes
                            rawQuoteCountdown = quotesOrBraces - 1;
                            state = State.RawStringClosing;
                            break;
                        case '\r':
                            state = State.RawStringCr;
                            break;
                        case '\n':
                            pos = (pos.Line + 1, 0);
                            break;
                        default: // Continue consuming raw string content
                            break;
                    }
                    break;
                }
                case State.RawStringClosing:
                {
                    if (ch == '"')
                    {
                        if (--rawQuoteCountdown == 0)
                            yield return Transit(TokenKind.RawStringLiteral, State.Text, 1);
                    }
                    else // Not enough quotes, continue in raw string content
                    {
                        rawQuoteCountdown = 0;
                        state = State.RawString;
                        goto restart;
                    }
                    break;
                }
                case State.RawStringCr:
                {
                    if (ch != '\n')
                        pos = (pos.Line + 1, ch == '\r' ? 0 : 1);
                    state = State.RawString;
                    goto restart;
                }
                case State.InterpolatedRawStringOpening:
                {
                    if (ch == '"')
                    {
                        quotesOrBraces++;
                    }
                    else // Count of dollars & opening quotes for raw string delimiter now known!
                    {
                        Debug.Assert(dollars > 0);
                        Debug.Assert(quotesOrBraces >= 3);

                        var newState = new InterpolationState(InterpolatedStringKind.Raw)
                        {
                            Dollars = dollars,
                            Quotes = quotesOrBraces,
                        };

                        if (EnterInterpolation(newState, -dollars - quotesOrBraces) is {} text)
                            yield return text;

                        goto restart;
                    }
                    break;
                }
                case State.InterpolatedRawString:
                {
                    switch (ch)
                    {
                        case '"': // Start counting (down) closing quotes
                        {
                            rawQuoteCountdown = interpolationState.Quotes - 1;
                            state = State.InterpolatedRawStringClosing;
                            break;
                        }
                        case '{': // Potential hole opening
                        {
                            if (interpolationState.Dollars == 1) // Single brace case
                            {
                                var tokenKind = source[si] == '$'
                                    ? TokenKind.InterpolatedRawStringLiteralStart
                                    : TokenKind.InterpolatedRawStringLiteralMid;
                                yield return Transit(tokenKind, State.Text, 1);
                            }
                            else // Need more braces; transition to brace counting state
                            {
                                quotesOrBraces = 1;
                                state = State.InterpolatedRawStringBrace;
                            }

                            break;
                        }
                        case '\r':
                            state = State.InterpolatedRawStringCr;
                            break;
                        case '\n':
                            pos = (pos.Line + 1, 0);
                            break;
                        default:
                            break;
                    }

                    break;
                }
                case State.InterpolatedRawStringClosing:
                {
                    if (ch == '"')
                    {
                        if (--rawQuoteCountdown == 0)
                        {
                            yield return ExitInterpolation(source[si] == '$'
                                                           ? TokenKind.InterpolatedRawStringLiteral
                                                           : TokenKind.InterpolatedRawStringLiteralEnd, 1);
                        }
                    }
                    else // Not enough quotes, continue in interpolated raw string content
                    {
                        rawQuoteCountdown = 0;
                        state = State.InterpolatedRawString;
                        goto restart;
                    }

                    break;
                }
                case State.InterpolatedRawStringBrace:
                {
                    if (ch == '{') // Keep counting opening braces
                    {
                        quotesOrBraces++;
                    }
                    else if (quotesOrBraces < interpolationState.Dollars) // Not enough braces so it's just content
                    {
                        quotesOrBraces = 0;
                        state = State.InterpolatedRawString;
                        goto restart;
                    }
                    else
                    {
                        // Maximum allowed braces are 2*D-1 for D dollar signs
                        var maxAllowedBraces = interpolationState.Dollars * 2 - 1;
                        var actualBraces = Math.Min(quotesOrBraces, maxAllowedBraces);

                        // Technically, C# does not permit to more than the maximum allowed braces,
                        // but tolerate any excess as part of the interpolated expression (hole).

                        var innerBraces = quotesOrBraces - actualBraces;

                        var tokenKind = source[si] == '$'
                                      ? TokenKind.InterpolatedRawStringLiteralStart
                                      : TokenKind.InterpolatedRawStringLiteralMid;

                        yield return Transit(tokenKind, State.Text, -innerBraces);

                        interpolationState.Braces = innerBraces;
                        quotesOrBraces = 0;
                        goto restart;
                    }

                    break;
                }
                case State.InterpolatedRawStringCr:
                {
                    if (ch != '\n')
                        pos = (pos.Line + 1, ch == '\r' ? 0 : 1);
                    state = State.InterpolatedRawString;
                    goto restart;
                }
                default:
                    throw new UnreachableException();
            }
        }

#pragma warning disable IDE0010 // Add missing cases (see default)
        switch (state)
#pragma warning restore IDE0010 // Add missing cases
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
            case State.RawString:
            case State.RawStringCr:
            case State.InterpolatedRawString:
            case State.InterpolatedRawStringBrace:
            case State.InterpolatedRawStringCr:
            case State.Quote:
            case State.DollarQuote:
            case State.InterpolatedRawStringOpening:
            case State.RawStringOpening:
            case State.RawStringClosing:
                throw SyntaxError("Unterminated string starting.");
            case State.QuoteQuote:
            case State.DollarQuoteQuote:
            {
                pos.Col++;
                var interpolated = state == State.DollarQuoteQuote;

                if (TextTransit(state, -2 - (interpolated ? 1 : 0)) is { } text)
                    yield return text;

                yield return CreateToken(interpolated ? TokenKind.InterpolatedStringLiteral : TokenKind.StringLiteral);
                break;
            }
            case State.Char:
                throw SyntaxError("Unterminated character literal.");
            case State.MultiLineComment:
            case State.MultiLineCommentStar:
            case State.MultiLineCommentCr:
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

#pragma warning disable IDE0072 // Add missing cases (see default)
                        var token = state switch
#pragma warning restore IDE0072 // Add missing cases
                        {
                            State.SingleLineComment => TokenKind.SingleLineComment,
                            State.WhiteSpace or State.LeadingWhiteSpace => TokenKind.WhiteSpace,
                            State.Cr => TokenKind.NewLine,
                            State.PreprocessorDirective or State.PreprocessorDirectiveSlash => TokenKind.PreprocessorDirective,
                            State.VerbatimStringQuote => TokenKind.VerbatimStringLiteral,
                            State.PreprocessorDirectiveTrailingWhiteSpaceSlash => TokenKind.PreprocessorDirective,
                            State.InterpolatedVerbatimStringQuote when IsDollarOrAt(source[si]) => TokenKind.InterpolatedVerbatimStringLiteral,
                            State.InterpolatedVerbatimStringQuote => TokenKind.InterpolatedVerbatimStringLiteralEnd,
                            _ => TokenKind.Text
                        };

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
            var tokens = (List<Token>?)null;
            var level = 0;
            var awaitingEndRegionLineEnding = false;
            var lwsToken = (Token?)null;
            var startMessage = string.Empty;
            var endMessage = string.Empty;

            foreach (var token in Scan(source))
            {
#pragma warning disable IDE0010 // Add missing cases (false negative)
                switch (token.Kind)
#pragma warning restore IDE0010 // Add missing cases
                {
                    case TokenKind.WhiteSpace when level == 0:
                        lwsToken = token;
                        break;

                    case TokenKind.PreprocessorDirective:
#pragma warning disable IDE0010 // Add missing cases (default ignore)
                        switch (SplitName(token))
#pragma warning restore IDE0010 // Add missing cases
                        {
                            case ("region", var specifics):
                                if (level == 0)
                                {
                                    startMessage = specifics;
                                    tokens = [];
                                    if (lwsToken is {} t)
                                        tokens.Add(t);
                                }
                                level++;
                                break;

                            case ("endregion", var specifics):
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

            (string, string) SplitName(Token token) =>
                source.Slice(token.Start.Offset + 1, token.End.Offset)
                      .TrimStart()
                      .Split(SpaceOrTab, 2) switch
                {
                    [var name] => (name, string.Empty),
                    [var name, var rest] => (name, rest.Trim()),
                    _ => throw new UnreachableException(),
                };
        }
    }

    static readonly char[] SpaceOrTab = [' ', '\t'];
}
