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
            foreach (var token in tokens)
            {
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
        }
    }

    static StringValueParseResult TryParse(string source, TokenKind kind, int startIndex, int endIndex)
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
                        var actualContentEnd = lastNewlinePos;
                        
                        // Apply whitespace normalization
                        r = NormalizeRawStringWhitespace(source, actualContentStart, actualContentEnd, closingQuoteLineStart);
                        if (!r)
                            return r;
                        
                        s = r.Value;
                    }
                }
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
    static StringValueParseResult NormalizeRawStringWhitespace(string source, int contentStart, int contentEnd, int closingQuoteLineStart)
    {
        // Find indentation from closing quote line (all chars from line start to closing quotes)
        var indentLength = contentEnd - closingQuoteLineStart;
        
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
                
                // Preserve line ending
                _ = sb.Append('\r');
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
