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

    public static implicit operator StringValueParseResult(string value) => Success(value);

    public static implicit operator bool(StringValueParseResult result) =>
        result.Status == StringValueParseResultStatus.Success;
}

enum NewLine { None, Lf, Cr, CrLf }

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

        return ParseValuesIterator(tokens, source, selector);
    }

    enum State { Stream, ProcessBuffer, Ended }
    enum BufferProcessingStage { Start, Middle, End }

    static IEnumerable<T> ParseValuesIterator<T>(IEnumerable<Token> tokens, string source,
                                                 Func<Token, string, string, T> selector)
    {
        Stack<(List<Token> Buffer, int Index, int DollarCount, int QuoteCount, Span EndIndent, BufferProcessingStage Stage)>? stack = null;

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
                                    switch (TryParse(source, token))
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
                                var dollarCount = CountLeadingChars(source, startToken.Span, '$');
                                var quoteCount = CountLeadingChars(source, startToken.Span.ShrinkBy(dollarCount, 0), '"');

                                var closingQuoteStart = endToken.End.Offset - quoteCount;
                                var indentStart = closingQuoteStart;
                                // Find the start of the closing quote line by walking backwards
                                while (indentStart > endToken.Start.Offset && source[indentStart - 1] is ' ' or '\t')
                                    indentStart--;
                                var indent = Span.StartEnd(indentStart, closingQuoteStart);
                                switch (TryParseInterpolatedRawStringPart(source, startToken, dollarCount, quoteCount, indent))
                                {
                                    case { Value: "" }: break;
                                    case { Value: {} value }: yield return selector(startToken, source, value); break;
                                    case var error: throw error.ToSyntaxError();
                                }

                                stack.Push(frame with // frame to continue with middle tokens
                                {
                                    Index = 1,
                                    DollarCount = dollarCount,
                                    QuoteCount = quoteCount,
                                    EndIndent = indent,
                                    Stage = BufferProcessingStage.Middle
                                });
                                break;
                            }
                            case BufferProcessingStage.Middle:
                            {
                                if (currentIndex >= buffer.Count - 1)
                                {
                                    // All middle tokens processed, move to end
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
                                        switch (TryParseInterpolatedRawStringPart(source, token,
                                                                                  frame.DollarCount, frame.QuoteCount, frame.EndIndent))
                                        {
                                            case { Value: "" }: break;
                                            case { Value: {} value }: yield return selector(token, source, value); break;
                                            case var error: throw error.ToSyntaxError();
                                        }

                                        stack.Push(frame with { Index = currentIndex + 1 }); // continue with next token
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
                                    default: // Other token (non-raw string) - process normally
                                    {
                                        switch (TryParse(source, token))
                                        {
                                            case { Value: {} value }: yield return selector(token, source, value); break;
                                            case var error: throw error.ToSyntaxError();
                                        }

                                        stack.Push(frame with { Index = currentIndex + 1 }); // continue with next token
                                        break;
                                    }
                                }
                                break;
                            }
                            case BufferProcessingStage.End:
                            {
                                var endToken = buffer[^1];
                                switch (TryParseInterpolatedRawStringPart(source, endToken,
                                                                          frame.DollarCount, frame.QuoteCount, frame.EndIndent))
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

    static StringValueParseResult TryParseInterpolatedRawStringPart(string source, Token token,
                                                                    int dollarCount, int quoteCount,
                                                                    Span indent)
    {
        var (part, content) =
            token.Kind.HasTraits(TokenKindTraits.InterpolatedStringStart)
            ? (InterpolatedPart.Start, token.Span.ShrinkBy(dollarCount + quoteCount, dollarCount))
            : token.Kind.HasTraits(TokenKindTraits.InterpolatedStringEnd)
            ? (InterpolatedPart.End, token.Span.ShrinkBy(dollarCount, quoteCount))
            : token.Kind.HasTraits(TokenKindTraits.InterpolatedStringMid)
            ? (InterpolatedPart.Mid, token.Span.ShrinkBy(dollarCount))
            : throw new UnreachableException();

        return TryParseRawStringContent(source, content, part, indent, 0);
    }

    enum InterpolatedPart { Start, Mid, End }

    static StringValueParseResult TryParseRawStringContent(string source, Span content, InterpolatedPart? part,
                                                           Span indent, // required for interpolated parts only
                                                           int closingQuoteStart) // required for regular raw strings only
    {
        var enumerator = new LineEnumerator(source, content);

        var moved = enumerator.MoveNext();
        Debug.Assert(moved); // a line is always returned, even for empty content

        var line = enumerator.Current;

        // Single-line: no indentation removal needed

        if (!line.HasNewLine)
            return source.Substring(content);

        // Multi-line: ...

        if (part is null or InterpolatedPart.Start)
        {
            // Validate opening line contains only whitespace after the quotes.

            foreach (var (i, ch) in line.Chars)
            {
                if (ch is not (' ' or '\t'))
                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringFormat, i);
            }

            // Adjust the content to exclude the new-line belonging to the opening quote line and
            // restart the enumerator so that the next line is read from there on.

            enumerator = new LineEnumerator(source, content with { Start = line.NewLineSpan.End });
        }

        // For raw string literal (non-interpolated), the caller will not provide the indent so
        // determine the indent from the closing quote line and validate it.

        if (part is null)
        {
            var closingLineStart = closingQuoteStart;
            while (closingLineStart > 0 && source[closingLineStart - 1] is not ('\n' or '\r'))
                closingLineStart--;

            indent = Span.StartEnd(closingLineStart, closingQuoteStart);

            foreach (var (i, ch) in source.Chars(indent)) // indent must be whitespace only
            {
                if (ch is not (' ' or '\t'))
                    return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringFormat, i);
            }
        }

        if (part is null or InterpolatedPart.End)
        {
            // Adjusts the content range to exclude the first new-line and the final one before the
            // closing quote line.

            var newLineLength = (source[indent.Start - 2], source[indent.Start - 1]) switch
            {
                ('\r', '\n') => 2,
                (_, '\r' or '\n') => 1,
                _ => 0
            };

            enumerator = new LineEnumerator(source, Span.StartEnd(part is null ? line.NewLineSpan.End : line.Start,
                                                                  indent.Start - newLineLength));
        }

        var nextIndent = indent;
        indent = part is InterpolatedPart.Mid or InterpolatedPart.End
               ? default // mid & end parts of interpolation are not initially indented
               : nextIndent;

        var sb = new StringBuilder();

        for (var i = 0; enumerator.MoveNext(); i++)
        {
            if (i > 0) // append new-line from previous line
                line.AppendNewLineTo(sb);

            line = enumerator.Current;

            // Validate indentation:
            // A line must either be empty or start with the expected indentation.

            var lineIndent = line.Content.Length >= indent.Length
                           ? indent
                           : indent with { Length = line.Content.Length };

            if (line.Content.Length > 0 && !line.StartsWith(lineIndent))
                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidRawStringWhitespace, line.Start);

            // Content after indentation:

            if (line.Content.Length > indent.Length)
                line.AppendTo(sb, indent.Length);

            indent = nextIndent; // restore indentation for subsequent lines
        }

        if (line.HasNewLine) // emit the final line's new-line if present
            line.AppendNewLineTo(sb);

        return sb.ToString();
    }

    static StringValueParseResult TryParse(string source, Token token)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var start = token.Start.Offset;
        var end = token.End.Offset - 1;
        var verbatim = false;
        var interpolated = false;
        string? s;
        StringValueParseResult r = default;

#pragma warning disable IDE0010 // Add missing cases (default error)
        switch (token.Kind)
#pragma warning restore IDE0010 // Add missing cases
        {
            case TokenKind.StringLiteral:
                r = Decode(start + 1, end, out s);
                break;
            case TokenKind.VerbatimStringLiteral:
                verbatim = true;
                s = source.Slice(start + 2, end);
                break;
            case TokenKind.InterpolatedStringLiteral:
            case TokenKind.InterpolatedStringLiteralStart:
                interpolated = true;
                r = Decode(start + 2, end, out s);
                break;
            case TokenKind.InterpolatedStringLiteralMid:
            case TokenKind.InterpolatedStringLiteralEnd:
            {
                interpolated = true;
                var i = source.IndexOf('}', start, token.Length);
                r = Decode(i + 1, end, out s);
                break;
            }
            case TokenKind.InterpolatedVerbatimStringLiteral:
            case TokenKind.InterpolatedVerbatimStringLiteralStart:
                verbatim = interpolated = true;
                s = source.Slice(start + 3, end);
                break;
            case TokenKind.InterpolatedVerbatimStringLiteralMid:
            case TokenKind.InterpolatedVerbatimStringLiteralEnd:
            {
                verbatim = interpolated = true;
                var i = source.IndexOf('}', start, token.Length) + 1;
                s = source.Slice(i, end);
                break;
            }
            case TokenKind.RawStringLiteral:
            {
                var span = token.Span;
                var quoteCount = CountLeadingChars(source, span, '"');
                var content = span.ShrinkBy(quoteCount);
                return TryParseRawStringContent(source, content, null, default, content.End);
            }
            case TokenKind.InterpolatedRawStringLiteral:
            {
                var span = token.Span;
                var dollarCount = CountLeadingChars(source, span, '$');
                var quoteCount = CountLeadingChars(source, span.ShrinkBy(dollarCount, 0), '"');
                var content = span.ShrinkBy(dollarCount + quoteCount, quoteCount);
                return TryParseRawStringContent(source, content, null, default, content.End);
            }
            default:
                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidToken, start);
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

    static int CountLeadingChars(string source, Span span, char ch)
    {
        var count = 0;
        for (var i = span.Start; i < span.End && source[i] == ch; i++)
            count++;
        return count;
    }

    /// <summary>
    /// Represents a single line from source text, with content and new-line terminator separated.
    /// </summary>
    [DebuggerDisplay($"{{{nameof(Start)}}}...{{{nameof(Content.End)}}} ({{{nameof(Content.Length)}}}), {nameof(NewLine)} = {{{nameof(NewLine)}}}: {{{nameof(Text)}}}")]
    readonly record struct SourceLine(string Source, Span Content, NewLine NewLine)
    {
        public int Start => Content.Start;

        public bool HasNewLine => NewLine is not NewLine.None;

        public Span NewLineSpan { get; } = new(Content.End, NewLine.Length);

        public CharSequence Chars => Source.Chars(Content);

        public void AppendTo(StringBuilder sb, int startOffset = 0) =>
            sb.Append(Source, Start + startOffset, Content.Length - startOffset);

        public void AppendNewLineTo(StringBuilder sb)
        {
#pragma warning disable IDE0010 // Add missing cases (default = no action)
            switch (NewLine)
#pragma warning restore IDE0010 // Add missing cases
            {
                case NewLine.Cr: _ = sb.Append('\r'); break;
                case NewLine.Lf: _ = sb.Append('\n'); break;
                case NewLine.CrLf: _ = sb.Append("\r\n"); break;
            }
        }

        public bool StartsWith(Span prefix)
        {
            if (Content.Length < prefix.Length)
                return false;

            for (int i = prefix.Start, j = Start; i < prefix.End; j++, i++)
            {
                if (Source[j] != Source[i])
                    return false;
            }

            return true;
        }

        string Text => Source.Substring(Content);
    }

    /// <remarks>
    /// Always returns at least a single line, even for empty spans.
    /// </remarks>
    struct LineEnumerator(string source, Span span)
    {
        static readonly char[] NewLineChars = ['\n', '\r'];

        bool eoi;
        Span span = span;

        public SourceLine Current { get; private set; }

        public bool MoveNext()
        {
            if (this.eoi)
                return false;

            if (this.span.Length == 0)
            {
                this.eoi = true;
                Current = new(source, this.span, NewLine.None);
                return true;
            }

            var end = this.span.End;
            var contentEnd = source.IndexOfAny(NewLineChars, this.span.Start, this.span.Length) switch
            {
                >= 0 and var i => i,
                _ => end,
            };

            var newLine =
                contentEnd >= end
                ? NewLine.None
                : source[contentEnd] switch
                  {
                      '\r' when contentEnd + 1 < end && source[contentEnd + 1] == '\n' => NewLine.CrLf,
                      '\r' => NewLine.Cr,
                      '\n' => NewLine.Lf,
                      _ => throw new UnreachableException(),
                  };

            Current = new(source, this.span with { End = contentEnd }, newLine);
            this.span = Span.StartEnd(contentEnd + newLine.Length, end);
            return true;
        }
    }
}

readonly record struct Span
{
    public static Span StartEnd(int start, int end)
    {
#if DEBUG
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), start, null);
        if (end < start) throw new ArgumentOutOfRangeException(nameof(end), end, null);
#endif
        return new(start, end - start);
    }

    public Span(int start, int length)
    {
#if DEBUG
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), start, null);
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), length, null);
#endif
        End = start + length;
        Start = start;
    }

    public int Start
    {
        get;
#if DEBUG
        init
        {
            if (value > End) throw new ArgumentOutOfRangeException(nameof(value), value, null);
            field = value;
        }
#else
        init;
#endif
    }

    public int End
    {
        get;
#if DEBUG
        init
        {
            if (value < Start) throw new ArgumentOutOfRangeException(nameof(value), value, null);
            field = value;
        }
#else
        init;
#endif
    }

    public int Length
    {
        get => End - Start;
        init
        {
#if DEBUG
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, null);
#endif
            End = Start + value;
        }
    }

    public Span ShrinkBy(int delta) => ShrinkBy(delta, delta);
    public Span ShrinkBy(int left, int right) => new(Start + left, Length - left - right);
}

readonly struct CharSequence(string source, Span span)
{
    public Enumerator GetEnumerator() => new(source, span);

    public struct Enumerator(string source, Span span)
    {
        Span span = span;

        public (int, char) Current { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (this.span.Length == 0)
                return false;

            var i = this.span.Start;
            Current = (i, source[i]);
            this.span = this.span.ShrinkBy(1, 0);
            return true;
        }
    }
}

file static class Extensions
{
    extension(string str)
    {
        public string Substring(Span span) => str.Substring(span.Start, span.Length);
        public CharSequence Chars(Span span) => new(str, span);
    }

    extension(Token token)
    {
        public Span Span => new(token.Start.Offset, token.Length);
    }

    extension(NewLine nl)
    {
        public int Length =>
#pragma warning disable CS8524 // The switch expression is not exhaustive (false negative)
#pragma warning disable IDE0072 // Add missing cases (false negative)
            nl switch
#pragma warning restore IDE0072 // Add missing cases (false negative)
#pragma warning restore CS8524 // The switch expression is not exhaustive (false negative)
            {
                NewLine.None => 0,
                NewLine.Cr or NewLine.Lf => 1,
                NewLine.CrLf => 2,
            };
    }
}
