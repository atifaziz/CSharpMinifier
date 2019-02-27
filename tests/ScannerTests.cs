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

namespace CSharpMinifier.Tests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Internals;
    using MoreLinq;
    using NUnit.Framework;

    [TestFixture]
    public class ScannerTests
    {
        [Test]
        public void ScanNullSource()
        {
            var e = Assert.Throws<ArgumentNullException>(() => Scanner.Scan(null));
            Assert.That(e.ParamName, Is.EqualTo("source"));
        }

        [Test]
        public void ScanEmpty()
        {
            var tokens = Scanner.Scan(string.Empty);
            Assert.That(tokens, Is.Empty);
        }

        [TestCase("\"")]
        [TestCase("\"foo")]
        [TestCase("\"foo\r")]
        [TestCase("\"foo\n")]
        [TestCase("\'")]
        [TestCase("\'x")]
        [TestCase("\'x\r")]
        [TestCase("\'x\n")]
        [TestCase("@\"")]
        [TestCase("@\"foo")]
        [TestCase("@\"foo\r")]
        [TestCase("@\"foo\n")]
        [TestCase("$\"")]
        [TestCase("$\"foo")]
        [TestCase("$\"foo\r")]
        [TestCase("$\"foo\n")]
        [TestCase("$\"foo = {(}\"")]
        [TestCase("$\"foo = {)}\"")]
        [TestCase("/*")]
        [TestCase("/* foo")]
        public void SyntaxError(string source)
        {
            Assert.Throws<SyntaxErrorException>(() => Scanner.Scan(source).Consume());
        }

        [TestCase(" ",          @"WhiteSpace 1 0 1 "" """)]
        [TestCase(" \t\r\n ",   @"WhiteSpace 5 1 1 "" \t\r\n """)]
        [TestCase("\n\n",       @"WhiteSpace 2 2 0 ""\n\n""")]
        [TestCase("\r\n",       @"WhiteSpace 2 1 0 ""\r\n""")]
        [TestCase("\r\n\r\n",   @"WhiteSpace 4 2 0 ""\r\n\r\n""")]
        [TestCase("\r\r",       @"WhiteSpace 2 2 0 ""\r\r""")]
        [TestCase("\r\r\r\n\n", @"WhiteSpace 5 4 0 ""\r\r\r\n\n""")]
        [TestCase(" \r \r ",    @"WhiteSpace 5 2 1 "" \r \r """)]

        [TestCase("//"   , @"SingleLineComment 2 0 2 ""//""")]

        [TestCase("//\r",
            @"SingleLineComment 2 0  2 ""//""",
            @"WhiteSpace        1 1 =1 ""\r""")]

        [TestCase("//\n",
            @"SingleLineComment 2 0 2 ""//""",
            @"WhiteSpace        1 1 =1 ""\n""")]

        [TestCase("//\r\n",
            @"SingleLineComment 2 0 2 ""//""",
            @"WhiteSpace        2 1 =1 ""\r\n""")]

        [TestCase("/**/"        , @"MultiLineComment  4 0 4 ""/**/""")]
        [TestCase("/***/"       , @"MultiLineComment  5 0 5 ""/***/""")]
        [TestCase("/** foo **/" , @"MultiLineComment  11 0 11 ""/** foo **/""")]
        [TestCase("/*\n*/"      , @"MultiLineComment  5 0 5 ""/*\n*/""")]

        [TestCase("foo/**/",
            @"Text              3 0 3 ""foo""",
            @"MultiLineComment  4 0 4 ""/**/""")]

        [TestCase("22/7", @"Text 4 0 4 ""22/7""")]

        [TestCase("'a'" , @"Char 3 0 3 ""\'a\'""")]
        [TestCase("'\\\\'", @"Char 4 0 4 ""\'\\\\\'""")]

        [TestCase("ch='a';",
            @"Text 3 0 3 ""ch=""",
            @"Char 3 0 3 ""\'a\'""",
            @"Text 1 0 1 "";""")]

        [TestCase(" \r \r42",
            @"WhiteSpace 4 2 0 "" \r \r""",
            @"Text       2 0 2 ""42""")]

        [TestCase("#line 42",
            @"PreprocessorDirective 8 0 8 ""#line 42""")]

        [TestCase("  #line 42",
            @"WhiteSpace            2 0 2 ""  """,
            @"PreprocessorDirective 8 0 8 ""#line 42""")]

        [TestCase("\r#line 42",
            @"WhiteSpace            1 1 =1 ""\r""",
            @"PreprocessorDirective 8 0  8 ""#line 42""")]

        [TestCase("#line 42 // comment",
            @"PreprocessorDirective 9  0  9 ""#line 42 """,
            @"SingleLineComment     10 0 10 ""// comment""")]

        [TestCase("#error 42 //",
            @"PreprocessorDirective 10 0 10 ""#error 42 """,
            @"SingleLineComment      2 0  2 ""//""")]

        [TestCase("#error 42 /",
            @"PreprocessorDirective 11 0 11 ""#error 42 /""")]

        [TestCase("#error 42 /\r",
            @"PreprocessorDirective 11 0  11 ""#error 42 /""",
            @"WhiteSpace             1 1 -11 ""\r""")]

        [TestCase("#error 42 /\n",
            @"PreprocessorDirective 11 0  11 ""#error 42 /""",
            @"WhiteSpace             1 1 -11 ""\n""")]

        [TestCase("#error 42 / 42",
            @"PreprocessorDirective 14 0 14 ""#error 42 / 42""")]

        [TestCase("@\"\"",
            @"VerbatimString 3 0 3 ""@\""\""""")]

        [TestCase("@\"foobar\"",
            @"VerbatimString 9 0 9 ""@\""foobar\""""")]

        [TestCase("@\" \"\" foobar \"\" \"",
            @"VerbatimString 17 0 17 ""@\"" \""\"" foobar \""\"" \""""")]

        [TestCase("@\"foo\r\nbar\"",
            @"VerbatimString 11 0 11 ""@\""foo\r\nbar\""""")]

        [TestCase("var@class=@\"class\";",
            @"Text           10 0 10 ""var@class=""",
            @"VerbatimString  8 0  8 ""@\""class\""""",
            @"Text            1 0  1 "";""")]

        [TestCase("\"\""          , @"String  2 0  2 ""\""\""""")]
        [TestCase("\"foobar\""    , @"String  8 0  8 ""\""foobar\""""")]
        [TestCase("\"foo\\\\bar\"", @"String 10 0 10 ""\""foo\\\\bar\""""")]

        [TestCase("foo=\"bar\";",
            @"Text   4 0 4 ""foo=""",
            @"String 5 0 5 ""\""bar\""""",
            @"Text   1 0 1 "";""")]

        [TestCase("$$"                , @"Text                2 0  2 ""$$""")]
        [TestCase("$\"\""             , @"InterpolatedString  3 0  3 ""$\""\""""")]
        [TestCase("$\"foobar\""       , @"InterpolatedString  9 0  9 ""$\""foobar\""""")]
        [TestCase("$\"foo\\\\bar\""   , @"InterpolatedString 11 0 11 ""$\""foo\\\\bar\""""")]
        [TestCase("$\"foo{{bar}}baz\"", @"InterpolatedString 16 0 16 ""$\""foo{{bar}}baz\""""")]

        [TestCase("$\"x = {x}, y = {y}\"",
            @"InterpolatedStringStart 7 0 7 ""$\""x = {""",
            @"Text                    1 0 1 ""x""",
            @"InterpolatedStringMid   8 0 8 ""}, y = {""",
            @"Text                    1 0 1 ""y""",
            @"InterpolatedStringEnd   2 0 2 ""}\""""")]

        [TestCase("$\"x = {(x < 0 ? 0 : x)}, y = {y}\"",
            @"InterpolatedStringStart 7 0 7 ""$\""x = {""",
            @"Text                    2 0 2 ""(x""",
            @"WhiteSpace              1 0 1 "" """,
            @"Text                    1 0 1 ""<""",
            @"WhiteSpace              1 0 1 "" """,
            @"Text                    1 0 1 ""0""",
            @"WhiteSpace              1 0 1 "" """,
            @"Text                    1 0 1 ""?""",
            @"WhiteSpace              1 0 1 "" """,
            @"Text                    1 0 1 ""0""",
            @"WhiteSpace              1 0 1 "" """,
            @"Text                    1 0 1 "":""",
            @"WhiteSpace              1 0 1 "" """,
            @"Text                    2 0 2 ""x)""",
            @"InterpolatedStringMid   8 0 8 ""}, y = {""",
            @"Text                    1 0 1 ""y""",
            @"InterpolatedStringEnd   2 0 2 ""}\""""")]

        [TestCase("$\"today = { $\"{DateTime.Today:MMM dd, yyyy}\" }\"",
            @"InterpolatedStringStart  11 0 11 ""$\""today = {""",
            @"WhiteSpace                1 0  1 "" """,
            @"InterpolatedStringStart   3 0  3 ""$\""{""",
            @"Text                     14 0 14 ""DateTime.Today""",
            @"InterpolatedStringEnd    15 0 15 "":MMM dd, yyyy}\""""",
            @"WhiteSpace                1 0  1 "" """,
            @"InterpolatedStringEnd     2 0  2 ""}\""""")]

        [TestCase("Console.WriteLine($\"|{\"Left\",-7}|{\"Right\",7}|\");",
            @"Text                    18 0 18 ""Console.WriteLine(""",
            @"InterpolatedStringStart  4 0  4 ""$\""|{""",
            @"String                   6 0  6 ""\""Left\""""",
            @"InterpolatedStringMid    6 0  6 "",-7}|{""",
            @"String                   7 0  7 ""\""Right\""""",
            @"InterpolatedStringEnd    5 0  5 "",7}|\""""",
            @"Text                     2 0  2 "");""")]

        [TestCase("Console.WriteLine($\"|{foo(12,34),-7}|{bar(56,78),7}|\");",
            @"Text                    18 0 18 ""Console.WriteLine(""",
            @"InterpolatedStringStart  4 0  4 ""$\""|{""",
            @"Text                    10 0 10 ""foo(12,34)""",
            @"InterpolatedStringMid    6 0  6 "",-7}|{""",
            @"Text                    10 0 10 ""bar(56,78)""",
            @"InterpolatedStringEnd    5 0  5 "",7}|\""""",
            @"Text                     2 0  2 "");""")]

        [TestCase("// This is a comment\r\n" +
                  "\r\n" +
                  "static class Program\r\n" +
                  "{\r\n    // static readonly string s = \"This is a string in a comment\";\r\n" +
                  "    static void Main()\r\n" +
                  "    {\r\n" +
                  "        Console.WriteLine(\"Hello world!\");\r\n" +
                  "    }\r\n" +
                  "}\r\n",
            @"SingleLineComment  20 0  20 ""// This is a comment""",
            @"WhiteSpace          4 2 -20 ""\r\n\r\n""",
            @"Text                6 0   6 ""static""",
            @"WhiteSpace          1 0   1 "" """,
            @"Text                5 0   5 ""class""",
            @"WhiteSpace          1 0   1 "" """,
            @"Text                7 0   7 ""Program""",
            @"WhiteSpace          2 1  =1 ""\r\n""",
            @"Text                1 0   1 ""{""",
            @"WhiteSpace          6 1   3 ""\r\n    """,
            @"SingleLineComment  62 0  62 ""// static readonly string s = \""This is a string in a comment\"";""",
            @"WhiteSpace          6 1  =5 ""\r\n    """,
            @"Text                6 0   6 ""static""",
            @"WhiteSpace          1 0   1 "" """,
            @"Text                4 0   4 ""void""",
            @"WhiteSpace          1 0   1 "" """,
            @"Text                6 0   6 ""Main()""",
            @"WhiteSpace          6 1  =5 ""\r\n    """,
            @"Text                1 0   1 ""{""",
            @"WhiteSpace         10 1   3 ""\r\n        """,
            @"Text               18 0  18 ""Console.WriteLine(""",
            @"String             14 0  14 ""\""Hello world!\""""",
            @"Text                2 0   2 "");""",
            @"WhiteSpace          6 1  =5 ""\r\n    """,
            @"Text                1 0   1 ""}""",
            @"WhiteSpace          2 1  =1 ""\r\n""",
            @"Text                1 0   1 ""}""",
            @"WhiteSpace          2 1  =1 ""\r\n""")
        ]

        public void Scan(string source, params string[] expectations)
        {
            var tokens =
                from t in Scanner.Scan(source)
                select $"{t} {JsonString.Encode(source, t.Start.Offset, t.Length)}";

            Assert.That(
                tokens,
                Is.EqualTo(
                    from e in
                    expectations
                        .Select(e =>
                            e.Split(' ', 5, StringSplitOptions.RemoveEmptyEntries)
                             .Fold((knd, oc, lc, cc, txt) => new
                             {
                                 Kind         = Enum.TryParse<TokenKind>(knd, true, out var kind)
                                              ? kind
                                              : Enum.Parse<TokenKind>(knd + "Literal", true),
                                 OffsetChange = int.Parse(oc, NumberStyles.None, CultureInfo.InvariantCulture),
                                 LineChange   = int.Parse(lc, NumberStyles.None, CultureInfo.InvariantCulture),
                                 ColumnChange = cc[0] != '=' ? int.Parse(cc, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)  : 0,
                                 Column       = cc[0] == '=' ? int.Parse(cc.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture) : 0,
                                 Text         = txt,
                             }))
                        .Scan(new
                              {
                                  Kind  = TokenKind.Text,
                                  Start = new Position(0, 1, 1),
                                  End   = new Position(0, 1, 1),
                                  Text  = (string)null,
                              },
                              (s, e) => new
                              {
                                  e.Kind,
                                  Start = s.End,
                                  End = new Position(s.End.Offset + e.OffsetChange,
                                                     s.End.Line + e.LineChange,
                                                     e.Column > 0 ? e.Column
                                                                  : s.End.Column + e.ColumnChange),
                                  e.Text,
                              })
                        .Skip(1)
                    select $"{e.Kind} [{e.Start}..{e.End}) {e.Text}"));
        }
    }
}
