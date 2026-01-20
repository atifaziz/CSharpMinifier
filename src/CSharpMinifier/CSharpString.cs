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
using System.Runtime.CompilerServices;
using System.Text;

namespace CSharpMinifier;

enum StringValueParseResultStatus
{
    Success,
    InvalidToken,
    InvalidEscapeSequence,
    InvalidUnicodeEscapeCharacterSequence,
    InvalidHexadecimalEscapeSequence,
    InvalidRawStringWhitespace,
    InvalidRawStringQuotes,
    InvalidRawStringFormat,
}

readonly record struct StringValueParseResult
{
    public StringValueParseResultStatus Status { get; }
    public int ErrorOffset { get; }
    public string? Value { get; }

    internal static StringValueParseResult Error(StringValueParseResultStatus status, int offset) =>
        new(status, offset, null);

    internal static StringValueParseResult Success(string value) =>
        new(StringValueParseResultStatus.Success, 0, value);

    StringValueParseResult(StringValueParseResultStatus status, int errorOffset, string? value)
    {
        Status      = status;
        ErrorOffset = errorOffset;
        Value       = value;
    }

    public override string ToString()
        => Status == StringValueParseResultStatus.Success ? Value ?? string.Empty
         : $"{Status} @ {ErrorOffset}";

    internal SyntaxErrorException ToSyntaxError() =>
#pragma warning disable IDE0072 // Add missing cases (default throw)
        throw new SyntaxErrorException(Status switch
#pragma warning restore IDE0072 // Add missing cases
        {
            StringValueParseResultStatus.InvalidToken =>
                "Token is not a string.",
            StringValueParseResultStatus.InvalidEscapeSequence =>
                "Invalid escape sequence in string.",
            StringValueParseResultStatus.InvalidUnicodeEscapeCharacterSequence =>
                "Invalid Unicode character escape sequence in string.",
            StringValueParseResultStatus.InvalidHexadecimalEscapeSequence =>
                "Invalid hexadecimal escape sequence in string.",
            StringValueParseResultStatus.InvalidRawStringWhitespace =>
                "Invalid whitespace indentation in raw string literal.",
            StringValueParseResultStatus.InvalidRawStringQuotes =>
                "Opening and closing quote counts don't match in raw string literal.",
            StringValueParseResultStatus.InvalidRawStringFormat =>
                "Invalid format in multi-line raw string literal.",
            _ => throw new InvalidOperationException()
        });

    public static implicit operator bool(StringValueParseResult result) =>
        result.Status == StringValueParseResultStatus.Success;
}

static class CSharpString
{
    public static IEnumerable<string>
            ParseValues(IEnumerable<Token> tokens, string source) =>
        ParseValues(tokens, source, (_, _, str) => str);

    public static IEnumerable<T>
            ParseValues<T>(IEnumerable<Token> tokens, string source,
                           Func<Token, string, string, T> selector)
    {
        if (tokens == null) throw new ArgumentNullException(nameof(tokens));
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (selector == null) throw new ArgumentNullException(nameof(selector));

        return _(); IEnumerable<T> _()
        {
            // Stack for tracking nested interpolated raw strings
            // Each entry: (dollarCount, quoteCount, bufferedTokens, startToken)
            var stack = new Stack<(int Dollars, int Quotes, List<Token> Buffered, Token Start)>();
            
            foreach (var token in tokens)
            {
                // Check if this is an interpolated raw string Start token
                // If we're not currently inside another raw interpolated string, push onto stack
                // If we are inside one, it will be buffered below
                if (token.Kind == TokenKind.InterpolatedRawStringLiteralStart && stack.Count == 0)
                {
                    // Extract dollar and quote counts from the token
                    var dollarCount = CountLeadingChars(source, token.Start.Offset, token.End.Offset, '$');
                    var afterDollars = token.Start.Offset + dollarCount;
                    var quoteCount = CountLeadingChars(source, afterDollars, token.End.Offset, '"');
                    
                    // Push new state onto stack
                    stack.Push((dollarCount, quoteCount, new List<Token>(), token));
                    continue;
                }
                
                // Check if this is a Mid token and we have an active interpolation
                if (token.Kind == TokenKind.InterpolatedRawStringLiteralMid && stack.Count > 0)
                {
                    // Buffer this token
                    var (_, _, buffered, _) = stack.Peek();
                    buffered.Add(token);
                    continue;
                }
                
                // Check if this is an End token and we have an active interpolation
                if (token.Kind == TokenKind.InterpolatedRawStringLiteralEnd && stack.Count > 0)
                {
                    // Check if this End matches the Start on top of stack
                    // Count braces and quotes to determine if they match
                    var (startDollars, startQuotes, bufferedTokens, startToken) = stack.Peek();
                    
                    var endBraceCount = CountLeadingChars(source, token.Start.Offset, token.End.Offset, '}');
                    var afterBraces = token.Start.Offset + endBraceCount;
                    var endQuoteCount = 0;
                    for (var idx = token.End.Offset - 1; idx >= afterBraces && source[idx] == '"'; idx--)
                        endQuoteCount++;
                    
                    // If dollar and quote counts match, this End is for our Start
                    if (endBraceCount == startDollars && endQuoteCount == startQuotes)
                    {
                        // Pop the state - the values are already in startToken and bufferedTokens from Peek above
                        var (_, _, _, _) = stack.Pop();
                    
                        // Determine indentation from the End token
                        var beforeClosingQuotes = token.End.Offset - endQuoteCount;
                    
                    // Find last newline to determine indentation
                    var lastNewlinePos = -1;
                    for (var i = beforeClosingQuotes - 1; i >= afterBraces; i--)
                    {
                        if (source[i] == '\n')
                        {
                            lastNewlinePos = i;
                            break;
                        }
                    }
                    
                    var closingQuoteLineStart = lastNewlinePos >= 0 ? lastNewlinePos + 1 : (int?)null;
                    var indentLength = closingQuoteLineStart.HasValue ? beforeClosingQuotes - closingQuoteLineStart.Value : 0;
                    
                    // Now reprocess Start, all buffered tokens, and End with the indentation context
                    // Process Start token
                    var startResult = TryParse(source, startToken.Kind, startToken.Start.Offset, startToken.End.Offset, closingQuoteLineStart, indentLength);
                    switch (startResult.Status, startResult.Value)
                    {
                        case (StringValueParseResultStatus.Success, {} value):
                            yield return selector(startToken, source, value);
                            break;
                        case (StringValueParseResultStatus.InvalidToken, _):
                            break;
                        default:
                            throw startResult.ToSyntaxError();
                    }
                    
                    // Process buffered tokens
                    // Mid tokens need indentation context, other tokens are processed recursively
                    var bufferIndex = 0;
                    while (bufferIndex < bufferedTokens.Count)
                    {
                        var bufferedToken = bufferedTokens[bufferIndex];
                        
                        if (bufferedToken.Kind == TokenKind.InterpolatedRawStringLiteralMid)
                        {
                            // Mid token needs indentation context from outer End
                            var midResult = TryParse(source, bufferedToken.Kind, bufferedToken.Start.Offset, bufferedToken.End.Offset, closingQuoteLineStart, indentLength);
                            switch (midResult.Status, midResult.Value)
                            {
                                case (StringValueParseResultStatus.Success, {} value):
                                    yield return selector(bufferedToken, source, value);
                                    break;
                                case (StringValueParseResultStatus.InvalidToken, _):
                                    break;
                                default:
                                    throw midResult.ToSyntaxError();
                            }
                            bufferIndex++;
                        }
                        else if (bufferedToken.Kind == TokenKind.InterpolatedRawStringLiteralStart)
                        {
                            // Nested raw interpolated string - find its matching End and process as a group
                            // Recursively process this Start and all tokens until matching End
                            var nestedTokens = new List<Token> { bufferedToken };
                            bufferIndex++;
                            var nestedLevel = 1;
                            
                            while (bufferIndex < bufferedTokens.Count && nestedLevel > 0)
                            {
                                var t = bufferedTokens[bufferIndex];
                                nestedTokens.Add(t);
                                
                                if (t.Kind == TokenKind.InterpolatedRawStringLiteralStart)
                                    nestedLevel++;
                                else if (t.Kind == TokenKind.InterpolatedRawStringLiteralEnd)
                                    nestedLevel--;
                                
                                bufferIndex++;
                            }
                            
                            // Recursively process the nested tokens
                            foreach (var value in ParseValues(nestedTokens, source, selector))
                            {
                                yield return value;
                            }
                        }
                        else
                        {
                            // Regular token (string, text, etc.)
                            var bufferedResult = TryParse(source, bufferedToken.Kind, bufferedToken.Start.Offset, bufferedToken.End.Offset);
                            switch (bufferedResult.Status, bufferedResult.Value)
                            {
                                case (StringValueParseResultStatus.Success, {} value):
                                    yield return selector(bufferedToken, source, value);
                                    break;
                                case (StringValueParseResultStatus.InvalidToken, _):
                                    break;
                                default:
                                    throw bufferedResult.ToSyntaxError();
                            }
                            bufferIndex++;
                        }
                    }
                    
                    // Process End token
                    var endResult = TryParse(source, token.Kind, token.Start.Offset, token.End.Offset, closingQuoteLineStart, indentLength);
                    switch (endResult.Status, endResult.Value)
                    {
                        case (StringValueParseResultStatus.Success, {} value):
                            yield return selector(token, source, value);
                            break;
                        case (StringValueParseResultStatus.InvalidToken, _):
                            break;
                        default:
                            throw endResult.ToSyntaxError();
                    }
                    
                        continue;
                    }
                    // else: End token doesn't match - must be for a nested string, so fall through to buffer it
                }
                
                // If we're inside a raw interpolated string, buffer this token
                if (stack.Count > 0)
                {
                    var (_, _, buffered, _) = stack.Peek();
                    buffered.Add(token);
                    continue;
                }
                
                // Check for error case: Mid or End without matching Start
                if ((token.Kind == TokenKind.InterpolatedRawStringLiteralMid || 
                     token.Kind == TokenKind.InterpolatedRawStringLiteralEnd) && 
                    stack.Count == 0)
                {
                    // Invalid token stream - unmatched Mid or End
                    throw new SyntaxErrorException($"Unmatched {token.Kind} token at offset {token.Start.Offset}");
                }
                
                // Normal token processing (not part of interpolated raw string state machine)
                var result = TryParse(source, token.Kind, token.Start.Offset, token.End.Offset);
                switch (result.Status, result.Value)
                {
                    case (StringValueParseResultStatus.Success, {} value):
                        yield return selector(token, source, value);
                        break;
                    case (StringValueParseResultStatus.InvalidToken, _):
                        break;
                    default:
                        throw result.ToSyntaxError();
                }
            }
            
            // Check for unclosed interpolated raw strings
            if (stack.Count > 0)
            {
                throw new SyntaxErrorException("Unclosed interpolated raw string literal");
            }
        }
    }

    static StringValueParseResult TryParse(string source, TokenKind kind, int startIndex, int endIndex, 
                                           int? indentationLineStart = null, int indentLength = 0)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var end = endIndex - 1;
        var verbatim = false;
        var interpolated = false;
        string? s;
        StringValueParseResult r = default;

#pragma warning disable IDE0010 // Add missing cases (default error)
        switch (kind)
#pragma warning restore IDE0010 // Add missing cases
        {
            case TokenKind.StringLiteral:
                r = Decode(startIndex + 1, end, out s);
                break;
            case TokenKind.VerbatimStringLiteral:
                verbatim = true;
                s = source.Slice(startIndex + 2, end);
                break;
            case TokenKind.InterpolatedStringLiteral:
            case TokenKind.InterpolatedStringLiteralStart:
                interpolated = true;
                r = Decode(startIndex + 2, end, out s);
                break;
            case TokenKind.InterpolatedStringLiteralMid:
            case TokenKind.InterpolatedStringLiteralEnd:
            {
                interpolated = true;
                var i = source.IndexOf('}', startIndex, endIndex - startIndex);
                r = Decode(i + 1, end, out s);
                break;
            }
            case TokenKind.InterpolatedVerbatimStringLiteral:
            case TokenKind.InterpolatedVerbatimStringLiteralStart:
                verbatim = interpolated = true;
                s = source.Slice(startIndex + 3, end);
                break;
            case TokenKind.InterpolatedVerbatimStringLiteralMid:
            case TokenKind.InterpolatedVerbatimStringLiteralEnd:
            {
                verbatim = interpolated = true;
                var i = source.IndexOf('}', startIndex, endIndex - startIndex) + 1;
                s = source.Slice(i, end);
                break;
            }
            case TokenKind.RawStringLiteral:
            {
                // Raw string: """content"""
                // Count opening quotes
                var openingQuoteCount = CountLeadingChars(source, startIndex, endIndex, '"');
                
                // Opening quotes must be at least 3
                if (openingQuoteCount < 3)
                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidToken, startIndex);
                
                // Find where content starts (after opening quotes)
                var contentStart = startIndex + openingQuoteCount;
                
                // Count closing quotes (work backwards from end)
                var closingQuoteCount = 0;
                for (var i = endIndex - 1; i >= contentStart && source[i] == '"'; i--)
                    closingQuoteCount++;
                
                // Validate quote counts match
                if (openingQuoteCount != closingQuoteCount)
                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringQuotes, startIndex);
                
                // Find where content ends (before closing quotes)
                var contentEnd = endIndex - closingQuoteCount;
                
                // Check if this is a multi-line raw string (contains newline)
                var hasNewline = false;
                var firstNewlinePos = -1;
                for (var i = contentStart; i < contentEnd; i++)
                {
                    if (source[i] == '\n')
                    {
                        hasNewline = true;
                        firstNewlinePos = i;
                        break;
                    }
                }
                
                if (!hasNewline)
                {
                    // Single-line: no whitespace normalization needed
                    s = source.Substring(contentStart, contentEnd - contentStart);
                }
                else
                {
                    // Multi-line: apply whitespace normalization rules
                    
                    // Find the last newline before closing quotes
                    var lastNewlinePos = -1;
                    for (var i = contentEnd - 1; i >= contentStart; i--)
                    {
                        if (source[i] == '\n')
                        {
                            lastNewlinePos = i;
                            break;
                        }
                    }
                    
                    if (lastNewlinePos < 0)
                    {
                        // This shouldn't happen if hasNewline is true, but handle it
                        s = source.Substring(contentStart, contentEnd - contentStart);
                    }
                    else
                    {
                        // The closing quote line starts after the last newline
                        var closingQuoteLineStart = lastNewlinePos + 1;
                        
                        // Check everything after opening quotes on same line is whitespace
                        var afterOpeningQuotes = contentStart;
                        var firstLineEnd = firstNewlinePos;
                        
                        // Scan from after quotes to first newline
                        for (var i = afterOpeningQuotes; i < firstLineEnd; i++)
                        {
                            if (source[i] is not (' ' or '\t' or '\r'))
                            {
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringFormat, i);
                            }
                        }
                        
                        // Check everything before closing quotes on same line is whitespace
                        for (var i = closingQuoteLineStart; i < contentEnd; i++)
                        {
                            if (source[i] is not (' ' or '\t' or '\r'))
                            {
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringFormat, i);
                            }
                        }
                        
                        // Content starts after the first newline (and skip \n from CRLF if present)
                        var actualContentStart = firstNewlinePos + 1;
                        
                        // Content ends at the last newline (we don't include the final newline)
                        // If the last newline is \n and preceded by \r, exclude the \r too
                        var actualContentEnd = lastNewlinePos;
                        if (lastNewlinePos > contentStart && source[lastNewlinePos] == '\n' && source[lastNewlinePos - 1] == '\r')
                        {
                            actualContentEnd = lastNewlinePos - 1; // Exclude the \r before the \n
                        }
                        
                        // The indentation is determined by the whitespace before closing quotes
                        // which is from closingQuoteLineStart to contentEnd
                        var closingLineIndentLength = contentEnd - closingQuoteLineStart;
                        
                        // Apply whitespace normalization
                        r = NormalizeRawStringWhitespace(source, actualContentStart, actualContentEnd, closingQuoteLineStart, closingLineIndentLength);
                        if (!r)
                            return r;
                        
                        s = r.Value;
                    }
                }
                break;
            }
            case TokenKind.InterpolatedRawStringLiteral:
            {
                // Complete interpolated raw string without holes: $"""content"""
                // Skip dollars at start
                var dollarCount = CountLeadingChars(source, startIndex, endIndex, '$');
                var afterDollars = startIndex + dollarCount;
                
                // Count opening quotes
                var openingQuoteCount = CountLeadingChars(source, afterDollars, endIndex, '"');
                
                // Opening quotes must be at least 3
                if (openingQuoteCount < 3)
                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidToken, startIndex);
                
                // Find where content starts (after dollars and opening quotes)
                var contentStart = afterDollars + openingQuoteCount;
                
                // Count closing quotes (work backwards from end)
                var closingQuoteCount = 0;
                for (var i = endIndex - 1; i >= contentStart && source[i] == '"'; i--)
                    closingQuoteCount++;
                
                // Validate quote counts match
                if (openingQuoteCount != closingQuoteCount)
                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringQuotes, startIndex);
                
                // Find where content ends (before closing quotes)
                var contentEnd = endIndex - closingQuoteCount;
                
                // Check if this is a multi-line raw string
                var hasNewline = false;
                var firstNewlinePos = -1;
                for (var i = contentStart; i < contentEnd; i++)
                {
                    if (source[i] == '\n')
                    {
                        hasNewline = true;
                        firstNewlinePos = i;
                        break;
                    }
                }
                
                if (!hasNewline)
                {
                    // Single-line: no whitespace normalization needed
                    s = source.Substring(contentStart, contentEnd - contentStart);
                }
                else
                {
                    // Multi-line: apply whitespace normalization
                    var lastNewlinePos = -1;
                    for (var i = contentEnd - 1; i >= contentStart; i--)
                    {
                        if (source[i] == '\n')
                        {
                            lastNewlinePos = i;
                            break;
                        }
                    }
                    
                    if (lastNewlinePos < 0)
                    {
                        s = source.Substring(contentStart, contentEnd - contentStart);
                    }
                    else
                    {
                        var closingQuoteLineStart = lastNewlinePos + 1;
                        
                        // Check everything after opening quotes on same line is whitespace
                        for (var i = contentStart; i < firstNewlinePos; i++)
                        {
                            if (source[i] is not (' ' or '\t' or '\r'))
                            {
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringFormat, i);
                            }
                        }
                        
                        // Check everything before closing quotes on same line is whitespace
                        for (var i = closingQuoteLineStart; i < contentEnd; i++)
                        {
                            if (source[i] is not (' ' or '\t' or '\r'))
                            {
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringFormat, i);
                            }
                        }
                        
                        var actualContentStart = firstNewlinePos + 1;
                        var actualContentEnd = lastNewlinePos;
                        if (lastNewlinePos > contentStart && source[lastNewlinePos] == '\n' && source[lastNewlinePos - 1] == '\r')
                        {
                            actualContentEnd = lastNewlinePos - 1; // Exclude the \r before the \n
                        }
                        var closingLineIndentLength = contentEnd - closingQuoteLineStart;
                        
                        r = NormalizeRawStringWhitespace(source, actualContentStart, actualContentEnd, closingQuoteLineStart, closingLineIndentLength);
                        if (!r)
                            return r;
                        
                        s = r.Value;
                    }
                }
                
                interpolated = true;
                break;
            }
            case TokenKind.InterpolatedRawStringLiteralStart:
            case TokenKind.InterpolatedRawStringLiteralMid:
            case TokenKind.InterpolatedRawStringLiteralEnd:
            {
                // These cases require indentation context from End token
                // Determine position based on token type
                var pos = startIndex;
                
                // For Start tokens, skip dollars first
                if (kind == TokenKind.InterpolatedRawStringLiteralStart)
                {
                    var dollarCount = CountLeadingChars(source, pos, endIndex, '$');
                    pos += dollarCount;
                    
                    // Then skip opening quotes
                    var openingQuoteCount = CountLeadingChars(source, pos, endIndex, '"');
                    pos += openingQuoteCount;
                }
                else
                {
                    // For Mid/End, skip closing braces first
                    var braceCount = CountLeadingChars(source, pos, endIndex, '}');
                    pos += braceCount;
                }
                
                // Find where the content ends (before trailing braces or quotes)
                int contentEnd;
                if (kind == TokenKind.InterpolatedRawStringLiteralEnd)
                {
                    // End: content ends before closing quotes
                    // Count closing quotes from the end
                    var closingQuoteCount = 0;
                    for (var i = endIndex - 1; i >= pos && source[i] == '"'; i--)
                        closingQuoteCount++;
                    
                    contentEnd = endIndex - closingQuoteCount;
                }
                else
                {
                    // Start/Mid: content ends before trailing opening braces
                    // Count opening braces from the end
                    var trailingBraceCount = 0;
                    for (var i = endIndex - 1; i >= pos && source[i] == '{'; i--)
                        trailingBraceCount++;
                    
                    contentEnd = endIndex - trailingBraceCount;
                }
                
                // Content is from pos to contentEnd
                var contentStart = pos;
                
                // Check if multi-line and apply normalization if indentation context is provided
                var hasNewline = false;
                var firstNewlinePos = -1;
                for (var i = contentStart; i < contentEnd; i++)
                {
                    if (source[i] == '\n')
                    {
                        hasNewline = true;
                        firstNewlinePos = i;
                        break;
                    }
                }
                
                if (!hasNewline || !indentationLineStart.HasValue)
                {
                    // Single-line or no indentation context: just extract content
                    s = contentStart < contentEnd ? source.Substring(contentStart, contentEnd - contentStart) : string.Empty;
                }
                else
                {
                    // Multi-line with indentation context
                    // For Start tokens: skip opening newline and remove indentation from first line
                    // For Mid/End tokens: just extract content (they're mid-line or end-of-line)
                    
                    if (kind == TokenKind.InterpolatedRawStringLiteralStart)
                    {
                        // Skip the opening newline
                        var actualContentStart = firstNewlinePos + 1;
                        
                        // Remove indentation from the first line of content
                        var lineStart = actualContentStart;
                        var lineEnd = contentEnd;
                        
                        // Find end of first line (or use contentEnd if no more newlines)
                        for (var i = actualContentStart; i < contentEnd; i++)
                        {
                            if (source[i] == '\n')
                            {
                                lineEnd = i;
                                break;
                            }
                        }
                        
                        // Remove indentation from this line
                        if (lineEnd - lineStart >= indentLength)
                        {
                            // Validate indentation matches
                            var indentMatches = true;
                            for (var j = 0; j < indentLength; j++)
                            {
                                if (source[lineStart + j] != source[indentationLineStart.Value + j])
                                {
                                    indentMatches = false;
                                    break;
                                }
                            }
                            
                            if (!indentMatches)
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart);
                            
                            s = source.Substring(lineStart + indentLength, contentEnd - lineStart - indentLength);
                        }
                        else
                        {
                            // Line is shorter than indentation - might be whitespace-only or error
                            s = source.Substring(lineStart, contentEnd - lineStart);
                        }
                    }
                    else
                    {
                        // Mid/End tokens: extract content, but for End tokens, exclude the final newline
                        var actualContentEnd = contentEnd;
                        
                        if (kind == TokenKind.InterpolatedRawStringLiteralEnd)
                        {
                            // Find and exclude the last newline
                            for (var i = contentEnd - 1; i >= contentStart; i--)
                            {
                                if (source[i] == '\n')
                                {
                                    actualContentEnd = i;
                                    // Also exclude preceding \r if CRLF
                                    if (i > contentStart && source[i - 1] == '\r')
                                    {
                                        actualContentEnd = i - 1;
                                    }
                                    break;
                                }
                            }
                        }
                        
                        s = contentStart < actualContentEnd ? source.Substring(contentStart, actualContentEnd - contentStart) : string.Empty;
                    }
                }
                
                interpolated = true;
                break;
            }
            default:
                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidToken, startIndex);
        }

        if (s == null)
            return r;

        if (interpolated)
            s = s.Replace("{{", "{").Replace("}}", "}");
        if (verbatim)
            s = s.Replace("\"\"", "\"");

        return StringValueParseResult.Success(s);

        StringValueParseResult Decode(int si, int ei, out string? decoded)
        {
            var length = ei - si;
            if (length == 0)
                return StringValueParseResult.Success(decoded = string.Empty);

            var i = source.IndexOf('\\', si, length);
            if (i < 0)
                return StringValueParseResult.Success(decoded = source.Substring(si, length));

            decoded = null;
            var sb = new StringBuilder();

            do
            {
                if (si < i)
                    _ = sb.Append(source, si, i - si);

                if (i + 1 == ei)
                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidEscapeSequence, i);

                switch (source[i + 1])
                {
                    case '\'': _ = sb.Append('\''); si = i + 2; break; // Single quote
                    case '"' : _ = sb.Append('\"'); si = i + 2; break; // Double quote
                    case '\\': _ = sb.Append('\\'); si = i + 2; break; // Backslash
                    case '0' : _ = sb.Append('\0'); si = i + 2; break; // Null
                    case 'a' : _ = sb.Append('\a'); si = i + 2; break; // Alert
                    case 'b' : _ = sb.Append('\b'); si = i + 2; break; // Backspace
                    case 'f' : _ = sb.Append('\f'); si = i + 2; break; // Form feed
                    case 'n' : _ = sb.Append('\n'); si = i + 2; break; // New line
                    case 'r' : _ = sb.Append('\r'); si = i + 2; break; // Carriage return
                    case 't' : _ = sb.Append('\t'); si = i + 2; break; // Horizontal tab
                    case 'v' : _ = sb.Append('\v'); si = i + 2; break; // Vertical tab
                    case 'u' :
                    {
                        var v = 0;
                        var dsi = i + 2;
                        var dei = dsi + 4;
                        if (dei > ei)
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidUnicodeEscapeCharacterSequence, i);
                        int di;
                        for (di = dsi; di < dei; di++)
                            _ = TryFoldNextHexDigit(ref v, source[di]);
                        si = di;
                        _ = sb.Append((char)v);
                        break;
                    }
                    case 'U':
                    {
                        var v = 0;
                        var dsi = i + 2;
                        var dei = dsi + 8;
                        if (dei > ei)
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidUnicodeEscapeCharacterSequence, i);
                        int di;
                        for (di = dsi; di < dei; di++)
                            _ = TryFoldNextHexDigit(ref v, source[di]);
                        si = di;
                        if (v >= 0x10FFFF)
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidUnicodeEscapeCharacterSequence, i);
                        if (v < 0x10000)
                        {
                            _ = sb.Append((char)v);
                        }
                        else
                        {
                            var x = v - 0x10000;
                            var h = (x >> 10) + 0xD800;
                            var l = (x & 0x3ff) + 0xDC00;
                            _ = sb.Append((char)h).Append((char)l);
                        }
                        break;
                    }
                    case 'x':
                    {
                        var v = 0;
                        var dsi = i + 2;
                        if (dsi == ei)
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidHexadecimalEscapeSequence, i);
                        int di;
                        var dei = Math.Min(ei, dsi + 4);
                        for (di = dsi; di < dei; di++)
                        {
                            if (!TryFoldNextHexDigit(ref v, source[di]))
                            {
                                if (di == dsi)
                                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidHexadecimalEscapeSequence, i);
                                break;
                            }
                        }
                        si = di;
                        _ = sb.Append((char)v);
                        break;
                    }
                    default:
                        return StringValueParseResult.Error(StringValueParseResultStatus.InvalidEscapeSequence, i);
                }
                i = source.IndexOf('\\', si, ei - si);
            }
            while (i >= 0);

            if (si < ei)
                _ = sb.Append(source, si, ei - si);

            return StringValueParseResult.Success(decoded = sb.ToString());
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryFoldNextHexDigit(ref int acc, char ch)
    {
        if (ch is >= '0' and <= '9')
        {
            acc = (acc << 4) + ch - '0';
            return true;
        }

        if (ch is >= 'a' and <= 'f' or >= 'A' and <= 'F')
        {
            acc = (acc << 4) + 10 + ((ch & ~0x20) - 'A');
            return true;
        }

        return false;
    }

    /// <summary>
    /// Counts consecutive occurrences of a specific character starting from a position.
    /// </summary>
    static int CountLeadingChars(string source, int startIndex, int endIndex, char ch)
    {
        var count = 0;
        for (var i = startIndex; i < endIndex && source[i] == ch; i++)
            count++;
        return count;
    }

    /// <summary>
    /// Normalizes whitespace in multi-line raw string literals per C# spec.
    /// Extracts indentation from closing quote line and removes it from all content lines.
    /// </summary>
    static StringValueParseResult NormalizeRawStringWhitespace(string source, int contentStart, int contentEnd, int closingQuoteLineStart, int indentLength)
    {
        // If no indentation required, just extract content
        if (indentLength == 0)
        {
            var content = source.Substring(contentStart, contentEnd - contentStart);
            return StringValueParseResult.Success(content);
        }

        var sb = new StringBuilder();
        var lineStart = contentStart;
        var i = contentStart;

        while (i < contentEnd)
        {
            // Find end of current line
            if (source[i] == '\n')
            {
                // Process the line (excluding the newline)
                var lineEnd = i;
                
                // Check if line starts with correct indentation
                var lineLength = lineEnd - lineStart;
                
                // Empty or whitespace-only lines still need validation
                if (lineLength > 0)
                {
                    // Validate indentation: must have at least indentLength chars matching exactly
                    if (lineLength < indentLength)
                    {
                        // Check if line is all whitespace - if so, include it
                        var allWhitespace = true;
                        for (var j = lineStart; j < lineEnd; j++)
                        {
                            if (source[j] is not (' ' or '\t'))
                            {
                                allWhitespace = false;
                                break;
                            }
                        }
                        
                        if (!allWhitespace)
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart);
                        
                        // For whitespace-only lines, include them as-is
                        _ = sb.Append(source, lineStart, lineLength);
                    }
                    else
                    {
                        // Validate that the indentation matches exactly (char by char)
                        for (var j = 0; j < indentLength; j++)
                        {
                            if (source[lineStart + j] != source[closingQuoteLineStart + j])
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart);
                        }
                        
                        // Add line content without the indentation prefix
                        _ = sb.Append(source, lineStart + indentLength, lineLength - indentLength);
                    }
                }
                
                // Add newline (but not the final one before closing quotes)
                if (i + 1 < contentEnd) // Not the last newline
                {
                    _ = sb.Append('\n');
                }
                
                i++;
                lineStart = i;
            }
            else if (source[i] == '\r')
            {
                // Process the line (excluding \r and potentially \n)
                var lineEnd = i;
                
                // Check if line starts with correct indentation
                var lineLength = lineEnd - lineStart;
                
                if (lineLength > 0)
                {
                    // Validate indentation
                    if (lineLength < indentLength)
                    {
                        // Check if line is all whitespace
                        var allWhitespace = true;
                        for (var j = lineStart; j < lineEnd; j++)
                        {
                            if (source[j] is not (' ' or '\t'))
                            {
                                allWhitespace = false;
                                break;
                            }
                        }
                        
                        if (!allWhitespace)
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart);
                        
                        // For whitespace-only lines, include them as-is
                        _ = sb.Append(source, lineStart, lineLength);
                    }
                    else
                    {
                        // Validate that the indentation matches exactly
                        for (var j = 0; j < indentLength; j++)
                        {
                            if (source[lineStart + j] != source[closingQuoteLineStart + j])
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart);
                        }
                        
                        // Add line content without the indentation prefix
                        _ = sb.Append(source, lineStart + indentLength, lineLength - indentLength);
                    }
                }
                
                // Preserve line ending (but not the final one before closing quotes)
                if (i + 1 < contentEnd) // Not the last \r or part of last CRLF
                {
                    _ = sb.Append('\r');
                }
                i++;
                
                // Check for CRLF
                if (i < contentEnd && source[i] == '\n')
                {
                    // Only add \n if not the final newline before closing quotes
                    if (i + 1 < contentEnd)
                    {
                        _ = sb.Append('\n');
                    }
                    i++;
                }
                
                lineStart = i;
            }
            else
            {
                i++;
            }
        }
        
        // Handle last line if it doesn't end with newline
        if (lineStart < contentEnd)
        {
            var lineLength = contentEnd - lineStart;
            
            if (lineLength < indentLength)
            {
                // Check if line is all whitespace
                var allWhitespace = true;
                for (var j = lineStart; j < contentEnd; j++)
                {
                    if (source[j] is not (' ' or '\t'))
                    {
                        allWhitespace = false;
                        break;
                    }
                }
                
                if (!allWhitespace)
                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart);
                
                _ = sb.Append(source, lineStart, lineLength);
            }
            else
            {
                // Validate indentation
                for (var j = 0; j < indentLength; j++)
                {
                    if (source[lineStart + j] != source[closingQuoteLineStart + j])
                        return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart);
                }
                
                _ = sb.Append(source, lineStart + indentLength, lineLength - indentLength);
            }
        }

        return StringValueParseResult.Success(sb.ToString());
    }
}
