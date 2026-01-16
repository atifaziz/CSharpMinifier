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
        Dollar,
        InterpolatedString,
        InterpolatedStringEscape,
        InterpolatedStringBrace,
        DollarAt,
        AtDollar,
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
        Quote,
        QuoteQuote,
        RawStringOpeningDelimiter,
        RawString,
        RawStringCr,
        RawStringQuote,
        RawStringQuoteQuote,
        DollarQuote,
        DollarQuoteQuote,
        InterpolatedRawStringOpeningDelimiter,
        InterpolatedRawString,
        InterpolatedRawStringCr,
        InterpolatedRawStringBrace,
        InterpolatedRawStringQuote,
        InterpolatedRawStringQuoteQuote,
    }

    enum InterpolatedStringKind { None, Regular, Verbatim, Raw }

    struct InterpolationState(InterpolatedStringKind kind)
    {
        public InterpolatedStringKind InterpolatedStringKind { get; } = kind;

        public readonly bool IsSome => InterpolatedStringKind is not InterpolatedStringKind.None;

        public int Parentheses { get; set; }
        public int Braces      { get; set; }
        public int Brackets    { get; set; }
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
        var rawStringQuoteCount = 0;
        var rawStringClosingQuoteCount = 0;
        var interpolatedRawStringDollarCount = 0;
        var interpolatedRawStringBraceCount = 0;

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
                            if (TextTransit(State.Quote) is {} text)
                                yield return text;
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
                        case (')', { IsSome: true }):
                            if (interpolationState.Parentheses-- == 0)
                                throw SyntaxError("Parentheses mismatch in interpolated string expression.");
                            break;
                        case ('{', { IsSome: true }):
                            interpolationState.Braces++;
                            break;
                        case ('}', { IsSome: true, Braces: > 0 }):
                            interpolationState.Braces--;
                            break;
                        case ('[', { IsSome: true }):
                            interpolationState.Brackets++;
                            break;
                        case (']', { IsSome: true }):
                            if (interpolationState.Brackets-- == 0)
                                throw SyntaxError("Brackets mismatch in interpolated string expression.");
                            break;
                        case (',' or ':', { IsSome: true, Parentheses: 0, Braces: 0, Brackets: 0 }):
                        case ('}', { IsSome: true }) :
                        {
#pragma warning disable IDE0072 // Populate switch
                            var newState = interpolationState.InterpolatedStringKind switch
                            {
                                InterpolatedStringKind.Verbatim => State.InterpolatedVerbatimString,
                                InterpolatedStringKind.Raw => State.InterpolatedRawString,
                                _ => State.InterpolatedString
                            };
#pragma warning restore IDE0072

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

                            interpolationState = interpolationStateStack.Count > 0 ? interpolationStateStack.Pop() : new();
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
                            state = State.DollarQuote;
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
                        state = State.DollarQuoteQuote;
                    }
                    else
                    {
                        // It was $" (regular interpolated string)
                        if (TextTransit(State.InterpolatedString, -2) is {} text)
                            yield return text;
                        goto restart;
                    }
                    break;
                }
                case State.DollarQuoteQuote:
                {
                    if (ch == '"')
                    {
                        // It's $""" (interpolated raw string)
                        // Emit any pending text before the $
                        if (TextTransit(State.InterpolatedRawStringOpeningDelimiter, -3) is {} text)
                            yield return text;
                        interpolatedRawStringDollarCount = 1;
                        rawStringQuoteCount = 3;
                        rawStringClosingQuoteCount = 0;
                    }
                    else
                    {
                        // It was $"" (empty interpolated string)
                        if (TextTransit(State.Text, -2) is {} text)
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
                        if (interpolationState.IsSome)
                            interpolationStateStack.Push(interpolationState);
                        interpolationState = new(InterpolatedStringKind.Regular);
                        goto restart;
                    }
                    break;
                }
                case State.AtDollar:
                case State.DollarAt:
                {
                    if (ch == '"')
                    {
                        if (TextTransit(State.InterpolatedVerbatimString, -2) is {} text)
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
                        yield return Transit(IsDollarOrAt(source[si])
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
                        yield return Transit(IsDollarOrAt(source[si])
                                             ? TokenKind.InterpolatedVerbatimStringLiteralStart
                                             : TokenKind.InterpolatedVerbatimStringLiteralMid,
                                             State.Text);
                        if (interpolationState.IsSome)
                            interpolationStateStack.Push(interpolationState);
                        interpolationState = new(InterpolatedStringKind.Verbatim);
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
                case State.Quote:
                {
                    if (ch == '"')
                    {
                        state = State.QuoteQuote;
                    }
                    else
                    {
                        state = State.String;
                        goto restart;
                    }
                    break;
                }
                case State.QuoteQuote:
                {
                    if (ch == '"')
                    {
                        rawStringQuoteCount = 3;
                        rawStringClosingQuoteCount = 0;
                        state = State.RawStringOpeningDelimiter;
                    }
                    else
                    {
                        // It was just an empty string ""
                        yield return Transit(TokenKind.StringLiteral, State.Text);
                        goto restart;
                    }
                    break;
                }
                case State.RawStringOpeningDelimiter:
                {
                    if (ch == '"')
                    {
                        // Continue counting opening quotes
                        rawStringQuoteCount++;
                    }
                    else
                    {
                        // Non-quote character, opening delimiter is complete
                        // Now we're in the content/body of the raw string
                        state = State.RawString;
                        goto restart;
                    }
                    break;
                }
                case State.RawString:
                {
#pragma warning disable IDE0010 // Populate switch
                    switch (ch)
#pragma warning restore IDE0010
                    {
                        case '"':
                            // Start of potential closing delimiter
                            rawStringClosingQuoteCount = 1;
                            state = State.RawStringQuote;
                            break;
                        case '\n':
                            pos = (pos.Line + 1, 0);
                            break;
                        case '\r':
                            state = State.RawStringCr;
                            break;
                        // All other characters are content, stay in RawString
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
                case State.RawStringQuote:
                {
                    if (ch == '"')
                    {
                        rawStringClosingQuoteCount = 2;
                        state = State.RawStringQuoteQuote;
                    }
                    else
                    {
                        // Not a closing delimiter, back to RawString
                        state = State.RawString;
                        goto restart;
                    }
                    break;
                }
                case State.RawStringQuoteQuote:
                {
                    if (ch == '"')
                    {
                        // Continue counting quotes
                        rawStringClosingQuoteCount++;
                    }
                    else
                    {
                        // End of quote sequence, check if we have enough
                        if (rawStringClosingQuoteCount == rawStringQuoteCount)
                        {
                            // Exact match - close the raw string
                            yield return Transit(TokenKind.RawStringLiteral, State.Text);
                            rawStringQuoteCount = 0;
                            rawStringClosingQuoteCount = 0;
                            goto restart;
                        }
                        else
                        {
                            // Not a match, back to scanning raw string content
                            state = State.RawString;
                            goto restart;
                        }
                    }
                    break;
                }
                case State.InterpolatedRawStringOpeningDelimiter:
                {
                    if (ch == '"')
                    {
                        // Continue counting opening quotes
                        rawStringQuoteCount++;
                    }
                    else
                    {
                        // Non-quote character, opening delimiter is complete
                        // Now we're in the content/body of the interpolated raw string
                        state = State.InterpolatedRawString;
                        goto restart;
                    }
                    break;
                }
                case State.InterpolatedRawString:
                {
#pragma warning disable IDE0010 // Populate switch
                    switch (ch)
#pragma warning restore IDE0010
                    {
                        case '"':
                            // Start of potential closing delimiter
                            rawStringClosingQuoteCount = 1;
                            state = State.InterpolatedRawStringQuote;
                            break;
                        case '{':
                            // Start counting braces for interpolation
                            interpolatedRawStringBraceCount = 1;
                            state = State.InterpolatedRawStringBrace;
                            break;
                        case '\n':
                            pos = (pos.Line + 1, 0);
                            break;
                        case '\r':
                            state = State.InterpolatedRawStringCr;
                            break;
                        // All other characters are content
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
                case State.InterpolatedRawStringBrace:
                {
                    if (ch == '{')
                    {
                        // Continue counting braces
                        interpolatedRawStringBraceCount++;
                    }
                    else
                    {
                        // Non-brace character, check brace count against dollar count
                        var B = interpolatedRawStringBraceCount;
                        var D = interpolatedRawStringDollarCount;
                        
                        if (B < D)
                        {
                            // All braces are literal, back to interpolated raw string
                            state = State.InterpolatedRawString;
                            goto restart;
                        }
                        else if (B == D)
                        {
                            // Enter hole (interpolation expression)
                            var kind = interpolationState.IsSome
                                     ? TokenKind.InterpolatedRawStringLiteralMid
                                     : TokenKind.InterpolatedRawStringLiteralStart;
                            if (TextTransit(State.Text, -B) is {} text)
                                yield return text;
                            yield return Transit(kind, State.Text);
                            interpolationStateStack.Push(interpolationState);
                            interpolationState = new InterpolationState(InterpolatedStringKind.Raw);
                            goto restart;
                        }
                        else // B > D
                        {
                            // Enter hole with some braces literal in the token
                            var bracesInToken = Math.Max(B, 2 * D - 1);
                            var bracesInHole = B - bracesInToken;
                            var kind = interpolationState.IsSome
                                     ? TokenKind.InterpolatedRawStringLiteralMid
                                     : TokenKind.InterpolatedRawStringLiteralStart;
                            if (TextTransit(State.Text, -bracesInToken) is {} text)
                                yield return text;
                            yield return Transit(kind, State.Text);
                            interpolationStateStack.Push(interpolationState);
                            interpolationState = new InterpolationState(InterpolatedStringKind.Raw) { Braces = bracesInHole };
                            goto restart;
                        }
                    }
                    break;
                }
                case State.InterpolatedRawStringQuote:
                {
                    if (ch == '"')
                    {
                        rawStringClosingQuoteCount = 2;
                        state = State.InterpolatedRawStringQuoteQuote;
                    }
                    else
                    {
                        // Not a closing delimiter, back to InterpolatedRawString
                        state = State.InterpolatedRawString;
                        goto restart;
                    }
                    break;
                }
                case State.InterpolatedRawStringQuoteQuote:
                {
#pragma warning restore CA1303
                    if (ch == '"')
                    {
                        // Continue counting quotes
                        rawStringClosingQuoteCount++;
                    }
                    else
                    {
                        // End of quote sequence, check if we have enough
                        if (rawStringClosingQuoteCount == rawStringQuoteCount)
                        {
#pragma warning restore CA1303
                            // Exact match - close the interpolated raw string
                            var kind = interpolationState.IsSome
                                     ? TokenKind.InterpolatedRawStringLiteralEnd
                                     : TokenKind.InterpolatedRawStringLiteral;
                            yield return Transit(kind, State.Text);
                            if (interpolationState.IsSome)
                                interpolationState = interpolationStateStack.Pop();
                            rawStringQuoteCount = 0;
                            rawStringClosingQuoteCount = 0;
                            interpolatedRawStringDollarCount = 0;
                            goto restart;
                        }
                        else
                        {
#pragma warning restore CA1303
                            // Not a match, back to scanning interpolated raw string content
                            state = State.InterpolatedRawString;
                            goto restart;
                        }
                    }
                    break;
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
            case State.Quote:
            case State.DollarQuote:
            case State.RawStringOpeningDelimiter:
            case State.RawString:
            case State.RawStringCr:
            case State.RawStringQuote:
            case State.InterpolatedRawStringOpeningDelimiter:
            case State.InterpolatedRawString:
            case State.InterpolatedRawStringCr:
            case State.InterpolatedRawStringBrace:
            case State.InterpolatedRawStringQuote:
                throw SyntaxError("Unterminated string starting.");
            case State.DollarQuoteQuote:
                // Special case: $"" is an empty interpolated string
                pos.Col++;
                yield return Transit(TokenKind.InterpolatedStringLiteral, State.Text);
                yield break;
            case State.RawStringQuoteQuote:
            case State.InterpolatedRawStringQuoteQuote:
                // Special case: if we have the right number of closing quotes, this is valid
                if (rawStringClosingQuoteCount != rawStringQuoteCount)
                    throw SyntaxError("Unterminated string starting.");
                // Fall through to emit the token
                goto default;
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
                            State.QuoteQuote => TokenKind.StringLiteral,
                            State.RawStringQuoteQuote when rawStringClosingQuoteCount == rawStringQuoteCount => TokenKind.RawStringLiteral,
                            State.InterpolatedRawStringQuoteQuote when rawStringClosingQuoteCount == rawStringQuoteCount && interpolationState.IsSome => TokenKind.InterpolatedRawStringLiteralEnd,
                            State.InterpolatedRawStringQuoteQuote when rawStringClosingQuoteCount == rawStringQuoteCount => TokenKind.InterpolatedRawStringLiteral,
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

            (string, string) SplitName(Token token)
            {
                var parts = source.Slice(token.Start.Offset + 1, token.End.Offset)
                                  .TrimStart()
                                  .Split(SpaceOrTab, 2);
                return (parts[0], parts.Length > 1 ? parts[1].Trim() : string.Empty);
            }
        }
    }

    static readonly char[] SpaceOrTab = [' ', '\t'];
}
