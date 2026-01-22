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
                "Line contains different whitespace than the closing line of the raw string literal.",
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
    /// Validates indentation and appends the line content (after stripping indentation) to the StringBuilder.
    /// Returns an error result if validation fails, or null on success.
    /// </summary>
    static StringValueParseResult? ValidateAndStripIndent(
        StringBuilder sb, string source, int lineStart, int lineLength, int indentStart, int indentLength)
    {
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

        return null;
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
                if (ValidateAndStripIndent(sb, source, lineStart, lineLength, indentStart, indentLength) is {} error)
                    return error;
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

        // If content ends right after a newline (empty line at the end), add the trailing newline
        if (lineStart == contentEnd && !isFirstOutputLine && lineStart > contentStart)
        {
            // Check if there's a newline just before lineStart
            if (lineStart >= 2 && source[lineStart - 2] == '\r' && source[lineStart - 1] == '\n')
                _ = sb.Append("\r\n");
            else if (source[lineStart - 1] == '\n')
                _ = sb.Append('\n');
            else if (source[lineStart - 1] == '\r')
                _ = sb.Append('\r');
        }

        return StringValueParseResult.Success(sb.ToString());
    }

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

        if (!hasNewline) // no whitespace normalization needed
            return StringValueParseResult.Success(source[contentStart..contentEnd]);

        // Multi-line: find the start of the closing quote line for indentation
        // The closing quote line starts after the last newline before the closing quotes
        var closingQuoteLine = closingQuoteStart;
        while (closingQuoteLine > 0 && source[closingQuoteLine - 1] is not '\n' and not '\r')
            closingQuoteLine--;

        // Calculate indentation length (whitespace before closing quotes on that line)
        var indentLength = closingQuoteStart - closingQuoteLine;

        // Validate that closing quote line contains only whitespace before the quotes
        for (var i = closingQuoteLine; i < closingQuoteStart; i++)
        {
            if (source[i] is not (' ' or '\t'))
                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, i);
        }

        // Skip opening line content (whitespace after opening quotes on same line is ignored)
        // Find the first newline after opening quotes
        var contentStartAfterFirstNewline = contentStart;
        while (contentStartAfterFirstNewline < contentEnd && source[contentStartAfterFirstNewline] is not '\n' and not '\r')
            contentStartAfterFirstNewline++;

        // Validate that opening line contains only whitespace after the quotes
        for (var i = contentStart; i < contentStartAfterFirstNewline; i++)
        {
            if (source[i] is not (' ' or '\t'))
                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, i);
        }

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
            switch (source[contentEndBeforeLastNewline - 1])
            {
                case '\n':
                {
                    contentEndBeforeLastNewline--;
                    if (contentEndBeforeLastNewline > contentStartAfterFirstNewline && source[contentEndBeforeLastNewline - 1] == '\r')
                        contentEndBeforeLastNewline--;
                    break;
                }
                case '\r':
                    contentEndBeforeLastNewline--;
                    break;
                default:
                    break;
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

        return ParseValuesIterator(tokens, source, selector);
    }

    enum State { Stream, ProcessBuffer, Ended }
    enum BufferProcessingStage { Start, Middle, End }

    static IEnumerable<T> ParseValuesIterator<T>(IEnumerable<Token> tokens, string source,
                                                 Func<Token, string, string, T> selector)
    {
        Stack<(List<Token> Buffer, int Index, int DollarCount, int QuoteCount, (int, int) EndIndentInfo, BufferProcessingStage Stage)>? stack = null;

        void PushBufferProcessing(List<Token> buffer)
        {
            stack ??= new();
            stack.Push((buffer, 0, 0, 0, default, BufferProcessingStage.Start));
        }

        using var enumerator = tokens.GetEnumerator();

        for (var state = State.Stream; state is not State.Ended;)
        {
            switch (state)
            {
                case State.Stream:
                {
                    var depth = 0;
                    List<Token>? buffer = null;

                    do
                    {
                        if (!enumerator.MoveNext())
                        {
                            if (depth > 0 || buffer is { Count: > 0 })
                                throw new InvalidTokenSourceException("Invalid token stream.");

                            state = State.Ended;
                        }
                        else
                        {
                            var token = enumerator.Current;
                            if (!token.Kind.HasTraits(TokenKindTraits.String))
                                continue;

                            if (depth == 0)
                            {
                                if (token.Kind == TokenKind.InterpolatedRawStringLiteralStart)
                                {
                                    buffer = [token];
                                    depth = 1;
                                }
                                else
                                {
                                    // Regular token processing (not inside a raw string context)
                                    switch (TryParse(source, token.Kind, token.Start.Offset,
                                                     token.End.Offset))
                                    {
                                        case { Value: { } value }:
                                            yield return selector(token, source, value); break;
                                        case var error: throw error.ToSyntaxError();
                                    }
                                }
                            }
                            else
                            {
                                Debug.Assert(buffer is not null);
                                buffer!.Add(token);

#pragma warning disable IDE0010 // Add missing cases
                                switch (token.Kind)
#pragma warning restore IDE0010 // Add missing cases
                                {
                                    case TokenKind.InterpolatedRawStringLiteralEnd:
                                    {
                                        if (--depth == 0)
                                        {
                                            PushBufferProcessing(buffer);
                                            buffer = null;
                                            state = State.ProcessBuffer;
                                        }

                                        break;
                                    }
                                    case TokenKind.InterpolatedRawStringLiteralStart:
                                    {
                                        depth++;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    while (state == State.Stream);

                    break;
                }
                case State.ProcessBuffer:
                {
                    Debug.Assert(stack is not null);
                    while (stack is { Count: > 0 })
                    {
                        var frame = stack.Pop();
                        var buffer = frame.Buffer;
                        var currentIndex = frame.Index;

                        if (buffer.Count < 2)
                            throw new UnreachableException();

#pragma warning disable IDE0010 // Add missing cases (all cases covered)
                        switch (frame.Stage)
#pragma warning restore IDE0010 // Add missing cases
                        {
                            case BufferProcessingStage.Start:
                            {
                                var startToken = buffer[0];
                                var endToken = buffer[^1];
                                var dollarCount = CountLeadingChars(source, startToken.Start.Offset, startToken.End.Offset, '$');
                                var quoteStart = startToken.Start.Offset + dollarCount;
                                var quoteCount = CountLeadingChars(source, quoteStart, startToken.End.Offset, '"');
                                var endIndentInfo = GetRawStringEndIndentation(source, endToken, quoteCount);

                                switch (TryParseInterpolatedRawStringPart(source, startToken,
                                                                          dollarCount, quoteCount, endIndentInfo))
                                {
                                    case { Value: "" }: break;
                                    case { Value: {} value }: yield return selector(startToken, source, value); break;
                                    case var error: throw error.ToSyntaxError();
                                }

                                // Push frame to continue with middle tokens
                                stack.Push(frame with
                                {
                                    Index = 1,
                                    DollarCount = dollarCount,
                                    QuoteCount = quoteCount,
                                    EndIndentInfo = endIndentInfo,
                                    Stage = BufferProcessingStage.Middle
                                });
                                break;
                            }
                            case BufferProcessingStage.Middle:
                            {
                                if (currentIndex >= buffer.Count - 1)
                                {
                                    // All middle tokens processed, move to End
                                    stack.Push(frame with { Stage = BufferProcessingStage.End });
                                    break;
                                }

                                var token = buffer[currentIndex];

#pragma warning disable IDE0010 // Add missing cases (covered by default)
                                switch (token.Kind)
#pragma warning restore IDE0010 // Add missing cases
                                {
                                    case TokenKind.InterpolatedRawStringLiteralMid:
                                    {
                                        // Mid token from this raw string
                                        switch (TryParseInterpolatedRawStringPart(source, token,
                                                                                  frame.DollarCount, frame.QuoteCount, frame.EndIndentInfo))
                                        {
                                            case { Value: "" }: break;
                                            case { Value: {} value }: yield return selector(token, source, value); break;
                                            case var error: throw error.ToSyntaxError();
                                        }

                                        // Continue with next token
                                        stack.Push(frame with { Index = currentIndex + 1 });
                                        break;
                                    }
                                    case TokenKind.InterpolatedRawStringLiteralStart:
                                    {
                                        // Nested raw string - find its matching End and push to stack
                                        List<Token> nestedBuffer = [token];
                                        var depth = 1;
                                        var i = currentIndex + 1;
                                        while (i < buffer.Count - 1 && depth > 0)
                                        {
                                            var nestedToken = buffer[i];
                                            nestedBuffer.Add(nestedToken);
#pragma warning disable IDE0010 // Add missing cases (necessary handled)
                                            switch (nestedToken.Kind)
#pragma warning restore IDE0010 // Add missing cases
                                            {
                                                case TokenKind.InterpolatedRawStringLiteralStart: depth++; break;
                                                case TokenKind.InterpolatedRawStringLiteralEnd: depth--; break;
                                            }
                                            i++;
                                        }

                                        stack.Push(frame with { Index = i }); // continuation frame for current buffer
                                        PushBufferProcessing(nestedBuffer);   // nested buffer processing
                                        break;
                                    }
                                    default:
                                    {
                                        // Other token (non-raw string) - process normally
                                        switch (TryParse(source, token.Kind, token.Start.Offset, token.End.Offset))
                                        {
                                            case { Value: {} value }: yield return selector(token, source, value); break;
                                            case var error: throw error.ToSyntaxError();
                                        }

                                        // Continue with next token
                                        stack.Push(frame with { Index = currentIndex + 1 });
                                        break;
                                    }
                                }
                                break;
                            }
                            case BufferProcessingStage.End:
                            {
                                var endToken = buffer[^1];
                                switch (TryParseInterpolatedRawStringPart(source, endToken,
                                                                          frame.DollarCount, frame.QuoteCount, frame.EndIndentInfo))
                                {
                                    case { Value: "" }: break;
                                    case { Value: {} value }: yield return selector(endToken, source, value); break;
                                    case var error: throw error.ToSyntaxError();
                                }
                                break;
                            }
                        }
                    }

                    state = State.Stream;
                    break;
                }
                case State.Ended:
                default:
                    throw new UnreachableException();
            }
        }
    }

    public sealed class InvalidTokenSourceException(string? message, Exception? inner) : Exception(message, inner)
    {
        public InvalidTokenSourceException() : this(null, null) {}

        public InvalidTokenSourceException(string? message) :
            this(message, null) {}
    }

    /// <summary>
    /// Gets the indentation information from an interpolated raw string End token.
    /// Returns (closingQuoteLine, indentLength) where closingQuoteLine is the index of the start of the line with closing quotes.
    /// </summary>
    static (int Start, int Length) GetRawStringEndIndentation(string source, Token endToken, int quoteCount)
    {
        // End token format: }...} content """
        // We need to find the closing quotes and extract the indentation

        var endOffset = endToken.End.Offset;
        var closingQuoteStart = endOffset - quoteCount;

        // Find the start of the closing quote line
        for (var closingQuoteLine = closingQuoteStart; closingQuoteLine > endToken.Start.Offset;)
        {
            switch (source[closingQuoteLine - 1])
            {
                case '\n' or '\r': return (closingQuoteLine, closingQuoteStart - closingQuoteLine);
                case ' ' or '\t': closingQuoteLine--; break;
                default: return (closingQuoteStart, 0);
            }
        }

        return (closingQuoteStart, 0);
    }

    static readonly char[] NewLineChars = ['\r', '\n'];

    enum InterpolatedPart { Start, Mid, End }

    /// <summary>
    /// Parses a part of an interpolated raw string (Start, Mid, or End token).
    /// </summary>
    static StringValueParseResult TryParseInterpolatedRawStringPart(
        string source, Token token, int dollarCount, int quoteCount,
        (int ClosingQuoteLine, int IndentLength) endIndentInfo)
    {
        var startOffset = token.Start.Offset;
        var endOffset = token.End.Offset;

        var (part, contentStart, contentEnd) =
            token.Kind.HasTraits(TokenKindTraits.InterpolatedStringStart)
            ? (InterpolatedPart.Start, startOffset + dollarCount + quoteCount, endOffset - dollarCount)
            : token.Kind.HasTraits(TokenKindTraits.InterpolatedStringEnd)
            ? (InterpolatedPart.End, startOffset + dollarCount, endOffset - quoteCount)
            : token.Kind.HasTraits(TokenKindTraits.InterpolatedStringMid)
            ? (InterpolatedPart.Mid, startOffset + dollarCount, endOffset - dollarCount)
            : throw new UnreachableException();

        // Check if multi-line (for whitespace normalization):
        // - Single-line or no indentation: extract directly
        // - Multi-line: apply whitespace normalization using indentation from End token

        return source.IndexOfAny(NewLineChars, contentStart, contentEnd - contentStart) switch
        {
            < 0 => StringValueParseResult.Success(source[contentStart..contentEnd]),
            _   => NormalizeRawStringPartWhitespace(source, contentStart, contentEnd,
                                                    endIndentInfo.ClosingQuoteLine, endIndentInfo.IndentLength, part) switch
                   {
                       { Value: { } value } => StringValueParseResult.Success(value),
                       var error => error
                   },
        };
    }

    /// <summary>
    /// Normalizes whitespace for a part of an interpolated raw string.
    /// </summary>
    static StringValueParseResult NormalizeRawStringPartWhitespace(
        string source, int contentStart, int contentEnd, int indentStart, int indentLength, InterpolatedPart part)
    {
        var sb = new StringBuilder();
        var lineStart = contentStart;
        var firstLine = true;

        while (lineStart < contentEnd)
        {
            // Find the end of this line
            var lineEnd = lineStart;
            while (lineEnd < contentEnd && source[lineEnd] is not '\n' and not '\r')
                lineEnd++;

            // For Start token's first line, skip whitespace after opening quotes (it's ignored)
            if (part == InterpolatedPart.Start && firstLine)
            {
                // Skip to the newline - content on opening quote line is ignored
            }
            else
            {
                // Check if this is the last line (before closing quotes in End token)
                var isLastLine = lineEnd >= contentEnd;

                // For End token's last line, don't include the content (it's just the indentation before closing quotes)
                if (!(part == InterpolatedPart.End && isLastLine))
                {
                    var lineLength = lineEnd - lineStart;

                    // For End token's first line, don't strip indentation - it's a continuation
                    // of the previous source line, not the start of a new content line
                    if (part == InterpolatedPart.End && firstLine)
                    {
                        _ = sb.Append(source, lineStart, lineLength);
                    }
                    else if (ValidateAndStripIndent(sb, source, lineStart, lineLength, indentStart, indentLength) is { } error)
                    {
                        return error;
                    }
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
                if (part == InterpolatedPart.End)
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

                var skipNewline = (part == InterpolatedPart.Start && firstLine) || skipBecauseEnd;

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
