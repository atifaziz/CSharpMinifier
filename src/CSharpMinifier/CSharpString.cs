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
                "Invalid whitespace in raw string literal. Content lines must start with the exact same whitespace as the closing delimiter line.",
            StringValueParseResultStatus.InvalidRawStringQuotes =>
                "Invalid raw string literal. Opening and closing quote counts must match.",
            StringValueParseResultStatus.InvalidRawStringFormat =>
                "Invalid raw string literal format.",
            _ => throw new InvalidOperationException()
        });

    public static implicit operator bool(StringValueParseResult result) =>
        result.Status == StringValueParseResultStatus.Success;
}

static class CSharpString
{
    /// <summary>
    /// Counts consecutive occurrences of a character starting from a given position.
    /// </summary>
    static int CountLeadingChars(string source, int startIndex, int endIndex, char ch)
    {
        var count = 0;
        for (var i = startIndex; i < endIndex && source[i] == ch; i++)
            count++;
        return count;
    }

    /// <summary>
    /// Normalizes whitespace for a multi-line raw string literal content.
    /// </summary>
    static StringValueParseResult NormalizeRawStringWhitespace(string source, int contentStart, int contentEnd, int indentStart, int indentLength)
    {
        var sb = new StringBuilder();
        var lineStart = contentStart;
        var isFirstOutputLine = true;

        while (lineStart < contentEnd)
        {
            // Find the end of this line
            var lineEnd = lineStart;
            while (lineEnd < contentEnd && source[lineEnd] != '\n' && source[lineEnd] != '\r')
                lineEnd++;

            var lineLength = lineEnd - lineStart;

            // Add newline before this line (except for the first line)
            if (!isFirstOutputLine)
            {
                // We need to figure out what newline preceded this line
                // Look backwards from lineStart to find the newline character(s)
                if (lineStart > contentStart)
                {
                    if (lineStart >= 2 && source[lineStart - 2] == '\r' && source[lineStart - 1] == '\n')
                        _ = sb.Append("\r\n");
                    else if (source[lineStart - 1] == '\n')
                        _ = sb.Append('\n');
                    else if (source[lineStart - 1] == '\r')
                        _ = sb.Append('\r');
                }
            }

            if (lineLength > 0)
            {
                // Validate and strip indentation prefix
                if (lineLength < indentLength)
                {
                    // Line is shorter than required indentation
                    // Check if the remaining characters match the prefix (whitespace-only line)
                    for (var i = 0; i < lineLength; i++)
                    {
                        if (source[lineStart + i] != source[indentStart + i])
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart + i);
                    }
                    // Whitespace-only line - include as empty (nothing to append)
                }
                else
                {
                    // Validate the indentation prefix matches exactly
                    for (var i = 0; i < indentLength; i++)
                    {
                        if (source[lineStart + i] != source[indentStart + i])
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, lineStart + i);
                    }

                    // Append content after stripping indentation
                    _ = sb.Append(source, lineStart + indentLength, lineLength - indentLength);
                }
            }

            isFirstOutputLine = false;

            // Move to next line
            if (lineEnd < contentEnd)
            {
                var newlineLength = 1;
                if (source[lineEnd] == '\r' && lineEnd + 1 < contentEnd && source[lineEnd + 1] == '\n')
                    newlineLength = 2;

                lineStart = lineEnd + newlineLength;
            }
            else
            {
                break;
            }
        }

        return StringValueParseResult.Success(sb.ToString());
    }

    /// <summary>
    /// Extracts content from a single-line raw string (no whitespace normalization needed).
    /// </summary>
    static string ExtractSingleLineRawContent(string source, int contentStart, int contentEnd) =>
        source.Substring(contentStart, contentEnd - contentStart);

    /// <summary>
    /// Parses raw string literal content, applying whitespace normalization for multi-line strings.
    /// </summary>
    static StringValueParseResult ParseRawStringContent(string source, int contentStart, int contentEnd, int closingQuoteStart)
    {
        // Check if multi-line (contains newline in content)
        var hasNewline = false;
        for (var i = contentStart; i < contentEnd; i++)
        {
            if (source[i] is '\n' or '\r')
            {
                hasNewline = true;
                break;
            }
        }

        if (!hasNewline)
        {
            // Single-line raw string - no whitespace normalization
            return StringValueParseResult.Success(ExtractSingleLineRawContent(source, contentStart, contentEnd));
        }

        // Multi-line: find the start of the closing quote line for indentation
        // The closing quote line starts after the last newline before the closing quotes
        var closingQuoteLine = closingQuoteStart;
        while (closingQuoteLine > 0 && source[closingQuoteLine - 1] != '\n' && source[closingQuoteLine - 1] != '\r')
            closingQuoteLine--;

        // Calculate indentation length (whitespace before closing quotes on that line)
        var indentLength = closingQuoteStart - closingQuoteLine;

        // Skip opening line content (whitespace after opening quotes on same line is ignored)
        // Find the first newline after opening quotes
        var contentStartAfterFirstNewline = contentStart;
        while (contentStartAfterFirstNewline < contentEnd && source[contentStartAfterFirstNewline] != '\n' && source[contentStartAfterFirstNewline] != '\r')
            contentStartAfterFirstNewline++;

        // Skip the first newline itself
        if (contentStartAfterFirstNewline < contentEnd)
        {
            if (source[contentStartAfterFirstNewline] == '\r' && contentStartAfterFirstNewline + 1 < contentEnd && source[contentStartAfterFirstNewline + 1] == '\n')
                contentStartAfterFirstNewline += 2;
            else
                contentStartAfterFirstNewline++;
        }

        // Find where content ends (everything up to but not including the newline before closing quotes)
        var contentEndBeforeLastNewline = closingQuoteLine;
        // Go back past the newline that precedes the closing quote line
        if (contentEndBeforeLastNewline > contentStartAfterFirstNewline)
        {
            if (source[contentEndBeforeLastNewline - 1] == '\n')
            {
                contentEndBeforeLastNewline--;
                if (contentEndBeforeLastNewline > contentStartAfterFirstNewline && source[contentEndBeforeLastNewline - 1] == '\r')
                    contentEndBeforeLastNewline--;
            }
            else if (source[contentEndBeforeLastNewline - 1] == '\r')
            {
                contentEndBeforeLastNewline--;
            }
        }

        return NormalizeRawStringWhitespace(source, contentStartAfterFirstNewline, contentEndBeforeLastNewline, closingQuoteLine, indentLength);
    }

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
            // Stack depth tracks how many nested raw string contexts we're in
            // Only the outermost Start creates a new buffer context
            var nestingDepth = 0;
            var buffer = new List<Token>();

            foreach (var token in tokens)
            {
                // Track Start tokens for nesting depth
                if (token.Kind == TokenKind.InterpolatedRawStringLiteralStart)
                {
                    if (nestingDepth == 0)
                    {
                        // Start of a new outer raw string - begin buffering
                        buffer.Clear();
                        buffer.Add(token);
                        nestingDepth = 1;
                    }
                    else
                    {
                        // Inner Start - just buffer it
                        buffer.Add(token);
                        nestingDepth++;
                    }
                    continue;
                }

                // Track End tokens for nesting depth
                if (token.Kind == TokenKind.InterpolatedRawStringLiteralEnd)
                {
                    if (nestingDepth > 0)
                    {
                        buffer.Add(token);
                        nestingDepth--;

                        // When we return to depth 0, we have a complete outer raw string
                        if (nestingDepth == 0)
                        {
                            // Process the buffered tokens recursively
                            foreach (var bufferedResult in ProcessBufferedRawString(buffer, source, selector))
                                yield return bufferedResult;
                            buffer.Clear();
                        }
                    }
                    continue;
                }

                // If we're inside a raw string context, buffer this token
                if (nestingDepth > 0)
                {
                    buffer.Add(token);
                    continue;
                }

                // Regular token processing (not inside a raw string context)
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

    /// <summary>
    /// Processes a buffered sequence of tokens that form a complete interpolated raw string
    /// (including nested raw strings).
    /// </summary>
    static IEnumerable<T> ProcessBufferedRawString<T>(
        List<Token> buffer, string source, Func<Token, string, string, T> selector)
    {
        if (buffer.Count < 2)
            yield break;

        var startToken = buffer[0];
        var endToken = buffer[buffer.Count - 1];

        // Get dollar and quote counts from Start token
        var dollarCount = CountLeadingChars(source, startToken.Start.Offset, startToken.End.Offset, '$');
        var quoteStart = startToken.Start.Offset + dollarCount;
        var quoteCount = CountLeadingChars(source, quoteStart, startToken.End.Offset, '"');

        // Extract indentation info from the End token
        var endIndentInfo = GetRawStringEndIndentation(source, endToken, quoteCount);

        // Process Start token
        var startResult = TryParseInterpolatedRawStringPart(source, startToken, dollarCount, quoteCount, endIndentInfo, isStart: true, isEnd: false);
        if (startResult.Status == StringValueParseResultStatus.Success)
        {
            if (!string.IsNullOrEmpty(startResult.Value))
                yield return selector(startToken, source, startResult.Value!);
            // Empty string is valid - just don't yield
        }
        else if (startResult.Status != StringValueParseResultStatus.InvalidToken)
        {
            throw startResult.ToSyntaxError();
        }

        // Process tokens in between (index 1 to count-2)
        var i = 1;
        while (i < buffer.Count - 1)
        {
            var token = buffer[i];

            if (token.Kind == TokenKind.InterpolatedRawStringLiteralMid)
            {
                // Mid token from this raw string
                var midResult = TryParseInterpolatedRawStringPart(source, token, dollarCount, quoteCount, endIndentInfo, isStart: false, isEnd: false);
                if (midResult.Status == StringValueParseResultStatus.Success)
                {
                    if (!string.IsNullOrEmpty(midResult.Value))
                        yield return selector(token, source, midResult.Value!);
                    // Empty string is valid - just don't yield
                }
                else if (midResult.Status != StringValueParseResultStatus.InvalidToken)
                {
                    throw midResult.ToSyntaxError();
                }
                i++;
            }
            else if (token.Kind == TokenKind.InterpolatedRawStringLiteralStart)
            {
                // Nested raw string - find its matching End and process recursively
                var nestedBuffer = new List<Token> { token };
                var nestedDepth = 1;
                i++;
                while (i < buffer.Count - 1 && nestedDepth > 0)
                {
                    var nestedToken = buffer[i];
                    nestedBuffer.Add(nestedToken);
                    if (nestedToken.Kind == TokenKind.InterpolatedRawStringLiteralStart)
                        nestedDepth++;
                    else if (nestedToken.Kind == TokenKind.InterpolatedRawStringLiteralEnd)
                        nestedDepth--;
                    i++;
                }

                // Process the nested raw string recursively
                foreach (var result in ProcessBufferedRawString(nestedBuffer, source, selector))
                    yield return result;
            }
            else
            {
                // Other token (non-raw string) - process normally
                var innerResult = TryParse(source, token.Kind, token.Start.Offset, token.End.Offset);
                switch (innerResult.Status, innerResult.Value)
                {
                    case (StringValueParseResultStatus.Success, {} value):
                        yield return selector(token, source, value);
                        break;
                    case (StringValueParseResultStatus.InvalidToken, _):
                        break;
                    default:
                        throw innerResult.ToSyntaxError();
                }
                i++;
            }
        }

        // Process End token
        var endResult = TryParseInterpolatedRawStringPart(source, endToken, dollarCount, quoteCount, endIndentInfo, isStart: false, isEnd: true);
        if (endResult.Status == StringValueParseResultStatus.Success)
        {
            if (!string.IsNullOrEmpty(endResult.Value))
                yield return selector(endToken, source, endResult.Value!);
            // Empty string is valid - just don't yield
        }
        else if (endResult.Status != StringValueParseResultStatus.InvalidToken)
        {
            throw endResult.ToSyntaxError();
        }
    }

    /// <summary>
    /// Gets the indentation information from an interpolated raw string End token.
    /// Returns (closingQuoteLine, indentLength) where closingQuoteLine is the index of the start of the line with closing quotes.
    /// </summary>
    static (int ClosingQuoteLine, int IndentLength) GetRawStringEndIndentation(string source, Token endToken, int quoteCount)
    {
        // End token format: }...} content """
        // We need to find the closing quotes and extract the indentation

        var endOffset = endToken.End.Offset;
        var closingQuoteStart = endOffset - quoteCount;

        // Find the start of the closing quote line
        var closingQuoteLine = closingQuoteStart;
        while (closingQuoteLine > endToken.Start.Offset && source[closingQuoteLine - 1] != '\n' && source[closingQuoteLine - 1] != '\r')
            closingQuoteLine--;

        // Calculate indentation length
        var indentEnd = closingQuoteLine;
        while (indentEnd < closingQuoteStart && source[indentEnd] is ' ' or '\t')
            indentEnd++;

        return (closingQuoteLine, indentEnd - closingQuoteLine);
    }

    /// <summary>
    /// Parses a part of an interpolated raw string (Start, Mid, or End token).
    /// </summary>
    static StringValueParseResult TryParseInterpolatedRawStringPart(
        string source, Token token, int dollarCount, int quoteCount,
        (int ClosingQuoteLine, int IndentLength) endIndentInfo,
        bool isStart, bool isEnd)
    {
        var startOffset = token.Start.Offset;
        var endOffset = token.End.Offset;

        int contentStart, contentEnd;

        if (isStart)
        {
            // Start token format: $...$"""...content...{...{
            // Skip dollars and opening quotes
            contentStart = startOffset + dollarCount + quoteCount;

            // Find the trailing braces (dollarCount of them)
            contentEnd = endOffset - dollarCount;
        }
        else if (isEnd)
        {
            // End token format: }...}content"""
            // Skip leading braces
            contentStart = startOffset + dollarCount;

            // Stop before closing quotes
            contentEnd = endOffset - quoteCount;
        }
        else
        {
            // Mid token format: }...}content{...{
            // Skip leading braces
            contentStart = startOffset + dollarCount;

            // Stop before trailing braces
            contentEnd = endOffset - dollarCount;
        }

        // Check if multi-line (for whitespace normalization)
        var hasNewline = false;
        for (var i = contentStart; i < contentEnd; i++)
        {
            if (source[i] is '\n' or '\r')
            {
                hasNewline = true;
                break;
            }
        }

        string content;
        if (hasNewline)
        {
            // Multi-line: apply whitespace normalization using indentation from End token
            var result = NormalizeRawStringPartWhitespace(source, contentStart, contentEnd, endIndentInfo.IndentLength, isStart, isEnd);
            if (result.Status != StringValueParseResultStatus.Success)
                return result;
            content = result.Value ?? string.Empty;
        }
        else
        {
            // Single-line or no indentation: extract directly
            content = source.Substring(contentStart, contentEnd - contentStart);
        }

        // Replace escaped braces for interpolated strings
        if (dollarCount > 0)
        {
            var escapedOpen = new string('{', dollarCount + 1);
            var escapedClose = new string('}', dollarCount + 1);
            content = content.Replace(escapedOpen, new string('{', dollarCount))
                            .Replace(escapedClose, new string('}', dollarCount));
        }

        return StringValueParseResult.Success(content);
    }

    /// <summary>
    /// Normalizes whitespace for a part of an interpolated raw string.
    /// </summary>
    static StringValueParseResult NormalizeRawStringPartWhitespace(
        string source, int contentStart, int contentEnd, int indentLength,
        bool isStart, bool isEnd)
    {
        var sb = new StringBuilder();
        var lineStart = contentStart;
        var firstLine = true;

        while (lineStart < contentEnd)
        {
            // Find the end of this line
            var lineEnd = lineStart;
            while (lineEnd < contentEnd && source[lineEnd] != '\n' && source[lineEnd] != '\r')
                lineEnd++;

            // For Start token's first line, skip whitespace after opening quotes (it's ignored)
            if (isStart && firstLine)
            {
                // Skip to the newline - content on opening quote line is ignored
            }
            else
            {
                // Check if this is the last line (before closing quotes in End token)
                var isLastLine = lineEnd >= contentEnd;

                // For End token's last line, don't include the content (it's just the indentation before closing quotes)
                if (!(isEnd && isLastLine))
                {
                    var lineLength = lineEnd - lineStart;

                    // For End token's first line, don't strip indentation - it's a continuation
                    // of the previous source line, not the start of a new content line
                    if (isEnd && firstLine)
                    {
                        _ = sb.Append(source, lineStart, lineLength);
                    }
                    else if (lineLength >= indentLength)
                    {
                        // Strip indentation and append
                        _ = sb.Append(source, lineStart + indentLength, lineLength - indentLength);
                    }
                    // Short lines (whitespace-only) are allowed - they become empty
                }
            }

            // Handle newline
            if (lineEnd < contentEnd)
            {
                var newlineLength = 1;
                if (source[lineEnd] == '\r' && lineEnd + 1 < contentEnd && source[lineEnd + 1] == '\n')
                    newlineLength = 2;

                // For Start token, skip the first newline (after opening quotes)
                // For End token, skip the newline if the next line is just the indentation before closing quotes
                // (or if there's nothing after the newline, meaning no indentation)
                var nextLineStart = lineEnd + newlineLength;
                var skipBecauseEnd = false;
                if (isEnd)
                {
                    if (nextLineStart >= contentEnd)
                    {
                        // No content after newline - it's the newline before non-indented closing quotes
                        skipBecauseEnd = true;
                    }
                    else
                    {
                        // Check if everything after the newline is whitespace (the indentation)
                        var allWhitespace = true;
                        for (var i = nextLineStart; i < contentEnd; i++)
                        {
                            if (source[i] is not (' ' or '\t'))
                            {
                                allWhitespace = false;
                                break;
                            }
                        }
                        skipBecauseEnd = allWhitespace;
                    }
                }

                var skipNewline = (isStart && firstLine) || skipBecauseEnd;

                if (!skipNewline)
                    _ = sb.Append(source, lineEnd, newlineLength);

                lineStart = lineEnd + newlineLength;
            }
            else
            {
                break;
            }

            firstLine = false;
        }

        return StringValueParseResult.Success(sb.ToString());
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
                // Count opening quotes
                var quoteCount = CountLeadingChars(source, startIndex, endIndex, '"');
                var contentStart = startIndex + quoteCount;
                var closingQuoteStart = endIndex - quoteCount;
                return ParseRawStringContent(source, contentStart, closingQuoteStart, closingQuoteStart);
            }
            case TokenKind.InterpolatedRawStringLiteral:
            {
                // Count dollars then quotes
                var dollarCount = CountLeadingChars(source, startIndex, endIndex, '$');
                var quoteStart = startIndex + dollarCount;
                var quoteCount = CountLeadingChars(source, quoteStart, endIndex, '"');
                var contentStart = quoteStart + quoteCount;
                var closingQuoteStart = endIndex - quoteCount;
                var result = ParseRawStringContent(source, contentStart, closingQuoteStart, closingQuoteStart);
                if (result.Status == StringValueParseResultStatus.Success && result.Value != null)
                {
                    // For interpolated raw strings, replace escaped braces
                    var escapedOpen = new string('{', dollarCount + 1);
                    var escapedClose = new string('}', dollarCount + 1);
                    var replacedValue = result.Value.Replace(escapedOpen, new string('{', dollarCount))
                                                    .Replace(escapedClose, new string('}', dollarCount));
                    return StringValueParseResult.Success(replacedValue);
                }
                return result;
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
}
