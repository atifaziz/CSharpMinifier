#region Copyright (c) 2005 Atif Aziz. All rights reserved.
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
using System.Globalization;
using System.Text;
using static JsonStringParser.ParseResult;

// ReSharper disable once CheckNamespace
// ReSharper disable once PartialTypeWithSinglePart

static partial class JsonStringParser
{
    public enum ParseResult
    {
        Success,
        UnquotedString,
        InvalidQuote,
        UnterminatedString,
        InvalidEscapeSequence,
        InvalidHexadecimalEscapeSequence,
    }

    [ThreadStatic] static StringBuilder _sb;

    public static string Parse(string input) =>
        Parse(input.AsSpan());

    public static string Parse(ReadOnlySpan<char> input) =>
        TryParse(input) ?? throw new FormatException("JSON string is malformed.");

    public static string TryParse(string input) =>
        TryParse(input.AsSpan());

    public static string TryParse(in ReadOnlySpan<char> input) =>
        TryParse(input, out var length, out var s) == Success && length == input.Length ? s : null;

    public static ParseResult TryParse(string input,
                                       out int parsedLength,
                                       out string output) =>
        TryParse(input.AsSpan(), out parsedLength, out output);

    public static ParseResult TryParse(in ReadOnlySpan<char> input,
                                       out int parsedLength,
                                       out string output)
    {
        var sb = _sb;
        var result = TryParse(input, out parsedLength, ref sb);
        output = result == Success ? sb.ToString() : null;
        if (sb != null)
        {
            sb.Clear();
            if (sb.Capacity > 512)
                sb.Capacity = 512;
            _sb = sb;
        }
        return result;
    }

    public static ParseResult TryParse(string input,
                                       out int parsedLength,
                                       ref StringBuilder output) =>
        TryParse(input.AsSpan(), out parsedLength, ref output);

    public static ParseResult TryParse(in ReadOnlySpan<char> input,
                                       out int parsedLength,
                                       ref StringBuilder output)
    {
        parsedLength = 0;

        Span<char> hexDigits = stackalloc char[4];

        char? Read(ref ReadOnlySpan<char>.Enumerator ee) =>
            ee.MoveNext() ? ee.Current : (char?) null;

        void Write(char ch, ref StringBuilder sb, ref int counter, int increment = 1)
        {
            counter += increment;
            (sb ?? (sb = new StringBuilder())).Append(ch);
        }

        var e = input.GetEnumerator();
        if (!(Read(ref e) is char quote))
            return UnquotedString;

        if (quote != '\'' && quote != '"')
            return InvalidQuote;

        parsedLength++;

        while (true)
        {
            switch (Read(ref e))
            {
                case null:
                case '\n':
                case '\r':
                    return UnterminatedString;

                case '\\':
                    switch (Read(ref e))
                    {
                        case null:
                            return InvalidEscapeSequence;

                        case 'b': Write('\b', ref output, ref parsedLength, 2); break; // Backspace
                        case 't': Write('\t', ref output, ref parsedLength, 2); break; // Horizontal tab
                        case 'n': Write('\n', ref output, ref parsedLength, 2); break; // Newline
                        case 'f': Write('\f', ref output, ref parsedLength, 2); break; // Form feed
                        case 'r': Write('\r', ref output, ref parsedLength, 2); break; // Carriage return

                        case 'u':
                        {
                            if (Read(ref e) is char d1 &&
                                Read(ref e) is char d2 &&
                                Read(ref e) is char d3 &&
                                Read(ref e) is char d4)
                            {
                                hexDigits[0] = d1;
                                hexDigits[1] = d2;
                                hexDigits[2] = d3;
                                hexDigits[3] = d4;

                                if (!ushort.TryParse(hexDigits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var n))
                                    return InvalidHexadecimalEscapeSequence;

                                Write((char)n, ref output, ref parsedLength, 6);
                            }
                            else
                            {
                                return InvalidHexadecimalEscapeSequence;
                            }
                            break;
                        }

                        case char ch:
                            Write(ch, ref output, ref parsedLength, 2);
                            break;
                    }
                    break;

                case char ch when ch == quote:
                    parsedLength++;
                    return Success;

                case char ch:
                    Write(ch, ref output, ref parsedLength);
                    break;
            }
        }
    }
}
