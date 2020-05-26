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
    using System.Runtime.CompilerServices;
    using System.Text;

    enum StringValueParseResultStatus
    {
        Success,
        InvalidToken,
        InvalidEscapeSequence,
        InvalidUnicodeEscapeCharacterSequence,
        InvalidHexadecimalEscapeSequence,
    }

    readonly struct StringValueParseResult : IEquatable<StringValueParseResult>
    {
        public StringValueParseResultStatus Status { get; }
        public int ErrorOffset { get; }
        public string Value { get; }

        internal static StringValueParseResult Error(StringValueParseResultStatus status, int offset) =>
            new StringValueParseResult(status, offset, null);

        internal static StringValueParseResult Success(string value) =>
            new StringValueParseResult(StringValueParseResultStatus.Success, 0, value);

        StringValueParseResult(StringValueParseResultStatus status, int errorOffset, string value)
        {
            Status      = status;
            ErrorOffset = errorOffset;
            Value       = value;
        }

        public bool Equals(StringValueParseResult other)
            => Status == other.Status
            && ErrorOffset == other.ErrorOffset
            && string.Equals(Value, other.Value);

        public override bool Equals(object obj) =>
            obj is StringValueParseResult other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((int) Status * 397) ^ ErrorOffset * 397 ^ (Value != null ? Value.GetHashCode() : 0));

        public static bool operator ==(StringValueParseResult left, StringValueParseResult right) =>
            left.Equals(right);

        public static bool operator !=(StringValueParseResult left, StringValueParseResult right) =>
            !left.Equals(right);

        public override string ToString()
            => Status == StringValueParseResultStatus.Success ? Value ?? string.Empty
             : $"{Status} @ {ErrorOffset}";

        internal SyntaxErrorException ToSyntaxError() =>
            throw new SyntaxErrorException(Status switch
            {
                StringValueParseResultStatus.InvalidToken =>
                    "Token is not a string.",
                StringValueParseResultStatus.InvalidEscapeSequence =>
                    "Invalid escape sequence in string.",
                StringValueParseResultStatus.InvalidUnicodeEscapeCharacterSequence =>
                    "Invalid Unicode character escape sequence in string.",
                StringValueParseResultStatus.InvalidHexadecimalEscapeSequence =>
                    "Invalid hexadecimal escape sequence in string.",
                _ => throw new InvalidOperationException()
            });

        public static implicit operator bool(StringValueParseResult result) =>
            result.Status == StringValueParseResultStatus.Success;
    }

    static class CSharpString
    {
        public static IEnumerable<string>
                ParseValues(IEnumerable<Token> tokens, string source) =>
            ParseValues(tokens, source, (_, __, str) => str);

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
                    switch (result.Status)
                    {
                        case StringValueParseResultStatus.Success:
                            yield return selector(token, source, result.Value);
                            break;
                        case StringValueParseResultStatus.InvalidToken:
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
            string s;
            StringValueParseResult r = default;

            switch (kind)
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

            StringValueParseResult Decode(int si, int ei, out string decoded)
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
                        sb.Append(source, si, i - si);

                    if (i + 1 == ei)
                        return StringValueParseResult.Error(StringValueParseResultStatus.InvalidEscapeSequence, i);

                    switch (source[i + 1])
                    {
                        case '\'': sb.Append('\''); si = i + 2; break; // Single quote
                        case '"' : sb.Append('\"'); si = i + 2; break; // Double quote
                        case '\\': sb.Append('\\'); si = i + 2; break; // Backslash
                        case '0' : sb.Append('\0'); si = i + 2; break; // Null
                        case 'a' : sb.Append('\a'); si = i + 2; break; // Alert
                        case 'b' : sb.Append('\b'); si = i + 2; break; // Backspace
                        case 'f' : sb.Append('\f'); si = i + 2; break; // Form feed
                        case 'n' : sb.Append('\n'); si = i + 2; break; // New line
                        case 'r' : sb.Append('\r'); si = i + 2; break; // Carriage return
                        case 't' : sb.Append('\t'); si = i + 2; break; // Horizontal tab
                        case 'v' : sb.Append('\v'); si = i + 2; break; // Vertical tab
                        case 'u' :
                        {
                            var v = 0;
                            var dsi = i + 2;
                            var dei = dsi + 4;
                            if (dei > ei)
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidUnicodeEscapeCharacterSequence, i);
                            int di;
                            for (di = dsi; di < dei; di++)
                                TryFoldNextHexDigit(ref v, source[di]);
                            si = di;
                            sb.Append((char) v);
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
                                TryFoldNextHexDigit(ref v, source[di]);
                            si = di;
                            if (v >= 0x10FFFF)
                                return StringValueParseResult.Error(StringValueParseResultStatus.InvalidUnicodeEscapeCharacterSequence, i);
                            if (v < 0x10000)
                            {
                                sb.Append((char) v);
                            }
                            else
                            {
                                var x = v - 0x10000;
                                var h = (x >> 10) + 0xD800;
                                var l = (x & 0x3ff) + 0xDC00;
                                sb.Append((char) h).Append((char) l);
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
                            sb.Append((char) v);
                            break;
                        }
                        default:
                            return StringValueParseResult.Error(StringValueParseResultStatus.InvalidEscapeSequence, i);
                    }
                    i = source.IndexOf('\\', si, ei - si);
                }
                while (i >= 0);

                if (si < ei)
                    sb.Append(source, si, ei - si);

                return StringValueParseResult.Success(decoded = sb.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryFoldNextHexDigit(ref int acc, char ch)
        {
            if (ch >= '0' && ch <= '9')
            {
                acc = (acc << 4) + ch - '0';
                return true;
            }

            if (ch >= 'a' && ch <= 'f' || ch >= 'A' && ch <= 'F')
            {
                acc = (acc << 4) + 10 + ((ch & ~0x20) - 'A');
                return true;
            }

            return false;
        }
    }
}
