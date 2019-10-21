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

namespace CSharpMinifier.Tests.Preprocessing
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Internals;
    using MoreLinq;
    using NUnit.Framework;
    using CSharpMinifier.Preprocessing;

    [TestFixture]
    public class ScannerTests
    {
        [Test]
        public void ScanNullExpression()
        {
            var e = Assert.Throws<ArgumentNullException>(() => Scanner.Scan(null));
            Assert.That(e.ParamName, Is.EqualTo("expression"));
        }

        [Test]
        public void ScanEmpty()
        {
            var tokens = Scanner.Scan(string.Empty);
            Assert.That(tokens, Is.Empty);
        }

        [TestCase("\"")]
        [TestCase("/*")]
        [TestCase("//")]
        [TestCase("&")]
        [TestCase("|")]
        [TestCase("!")]
        [TestCase("=")]
        [TestCase("& ")]
        [TestCase("| ")]
        [TestCase("& ")]
        [TestCase("| ")]
        public void SyntaxError(string source)
        {
            Assert.Throws<SyntaxErrorException>(() => Scanner.Scan(source).Consume());
        }

        [TestCase(" ",          @"WhiteSpace 1 "" """)]
        [TestCase("!",          @"Not        1 ""!""")]

        #if false
        [TestCase(";\n",        @"Text       1 0  1 "";""",
                                @"NewLine    1 1 -1 ""\n""")]

        [TestCase(";\r",        @"Text       1 0  1 "";""",
                                @"NewLine    1 1 -1 ""\r""")]

        [TestCase(" \t\r\n ",   @"WhiteSpace 2 0  2 "" \t""",
                                @"NewLine    2 1 =1 ""\r\n""",
                                @"WhiteSpace 1 0  1 "" """)]

        [TestCase("\n\n",       @"NewLine    1 1 0 ""\n""",
                                @"NewLine    1 1 0 ""\n""")]

        [TestCase("\r\n",       @"NewLine    2 1 0 ""\r\n""")]

        [TestCase("\r\n\r\n",   @"NewLine    2 1 0 ""\r\n""",
                                @"NewLine    2 1 0 ""\r\n""")]

        [TestCase("\r\r",       @"NewLine    1 1 0 ""\r""",
                                @"NewLine    1 1 0 ""\r""")]

        [TestCase("\n\r\r",     @"NewLine    1 1 0 ""\n""",
                                @"NewLine    1 1 0 ""\r""",
                                @"NewLine    1 1 0 ""\r""")]

        [TestCase("\r\r\r\n\n", @"NewLine    1 1 0 ""\r""",
                                @"NewLine    1 1 0 ""\r""",
                                @"NewLine    2 1 0 ""\r\n""",
                                @"NewLine    1 1 0 ""\n""")]

        [TestCase(" \r \r ",    @"WhiteSpace 1 0  1 "" """,
                                @"NewLine    1 1 =1 ""\r""",
                                @"WhiteSpace 1 0  1 "" """,
                                @"NewLine    1 1 =1 ""\r""",
                                @"WhiteSpace 1 0  1 "" """)]

        [TestCase("//"   , @"SingleLineComment 2 0 2 ""//""")]

        [TestCase("//\r",
            @"SingleLineComment 2 0  2 ""//""",
            @"NewLine           1 1 =1 ""\r""")]

        [TestCase("//\n",
            @"SingleLineComment 2 0 2 ""//""",
            @"NewLine           1 1 =1 ""\n""")]

        [TestCase("//\r\n",
            @"SingleLineComment 2 0 2 ""//""",
            @"NewLine           2 1 =1 ""\r\n""")]

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
            @"WhiteSpace 1 0  1 "" """,
            @"NewLine    1 1 =1 ""\r""",
            @"WhiteSpace 1 0  1 "" """,
            @"NewLine    1 1 =1 ""\r""",
            @"Text       2 0  2 ""42""")]

        // A pre-processing directive always occupies a separate line of
        // source code and always begins with a # character and a
        // pre-processing directive name.

        [TestCase("foo #bar baz",         // not technically valid C#
            @"Text       3 0 3 ""foo""",
            @"WhiteSpace 1 0 1 "" """,
            @"Text       4 0 4 ""#bar""",
            @"WhiteSpace 1 0 1 "" """,
            @"Text       3 0 3 ""baz""")]

        [TestCase("#line 42"            , @"PreprocessorDirective  8 0  8 ""#line 42""")]
        [TestCase("#line 42 / / comment", @"PreprocessorDirective 20 0 20 ""#line 42 / / comment""")]
        [TestCase("#line 42/ /comment"  , @"PreprocessorDirective 18 0 18 ""#line 42/ /comment""")]
        [TestCase("#error 42 / 42"      , @"PreprocessorDirective 14 0 14 ""#error 42 / 42""")]

        [TestCase("  #line 42",
            @"WhiteSpace            2 0  2 ""  """,
            @"PreprocessorDirective 8 0  8 ""#line 42""")]

        [TestCase("#line 42  ",
            @"PreprocessorDirective 8 0  8 ""#line 42""",
            @"WhiteSpace            2 0  2 ""  """)]

        [TestCase("  #line 42  ",
            @"WhiteSpace            2 0  2 ""  """,
            @"PreprocessorDirective 8 0  8 ""#line 42""",
            @"WhiteSpace            2 0  2 ""  """)]

        [TestCase("#line 42  \r",
            @"PreprocessorDirective 8 0  8 ""#line 42""",
            @"WhiteSpace            2 0  2 ""  """,
            @"NewLine               1 1 =1 ""\r""")]

        [TestCase("#line 42  \n",
            @"PreprocessorDirective 8 0  8 ""#line 42""",
            @"WhiteSpace            2 0  2 ""  """,
            @"NewLine               1 1 =1 ""\n""")]

        [TestCase("\r#line 42",
            @"NewLine               1 1 0 ""\r""",
            @"PreprocessorDirective 8 0 8 ""#line 42""")]

        [TestCase("#line 42// comment",
            @"PreprocessorDirective  8  0 8 ""#line 42""",
            @"SingleLineComment     10 0 10 ""// comment""")]

        [TestCase("#line 42 // comment",
            @"PreprocessorDirective  8  0 8 ""#line 42""",
            @"WhiteSpace             1 0  1 "" """,
            @"SingleLineComment     10 0 10 ""// comment""")]

        [TestCase("#error 42 //",
            @"PreprocessorDirective  9 0 9 ""#error 42""",
            @"WhiteSpace             1 0 1 "" """,
            @"SingleLineComment      2 0 2 ""//""")]

        [TestCase("#error 42 /",
            @"PreprocessorDirective 11 0 11 ""#error 42 /""")]

        [TestCase("#error 42 /\r",
            @"PreprocessorDirective 11 0  11 ""#error 42 /""",
            @"NewLine                1 1 -11 ""\r""")]

        [TestCase("#error 42 /\n",
            @"PreprocessorDirective 11 0  11 ""#error 42 /""",
            @"NewLine                1 1 -11 ""\n""")]

        // White space may occur before
        // the # character and between the # character and the directive
        // name.

        [TestCase("# error 42",
            @"PreprocessorDirective 10 0 10 ""# error 42""")]

        // Delimited comments (the /* */ style of comments) are not permitted
        // on source lines containing pre-processing directives.

        [TestCase("/* foo */ #bar /* baz */",
            @"MultiLineComment 9 0 9 ""/* foo */""",
            @"WhiteSpace       1 0 1 "" """,
            @"Text             4 0 4 ""#bar""",
            @"WhiteSpace       1 0 1 "" """,
            @"MultiLineComment 9 0 9 ""/* baz */""")]

        [TestCase("@\"\"",
            @"VerbatimString 3 0 3 ""@\""\""""")]

        [TestCase("@\"foobar\"",
            @"VerbatimString 9 0 9 ""@\""foobar\""""")]

        [TestCase("@\" \"\" foobar \"\" \"",
            @"VerbatimString 17 0 17 ""@\"" \""\"" foobar \""\"" \""""")]

        [TestCase("@\"foo\r\nbar\""    , @"VerbatimString 11 1 =5 ""@\""foo\r\nbar\""""")]
        [TestCase("@\"foo\r\rbar\""    , @"VerbatimString 11 2 =5 ""@\""foo\r\rbar\""""")]
        [TestCase("@\"foo\n\nbar\""    , @"VerbatimString 11 2 =5 ""@\""foo\n\nbar\""""")]
        [TestCase("@\"foo\r\r\nbar\""  , @"VerbatimString 12 2 =5 ""@\""foo\r\r\nbar\""""")]
        [TestCase("@\"foo\n\r\rbar\""  , @"VerbatimString 12 3 =5 ""@\""foo\n\r\rbar\""""")]
        [TestCase("@\"foo\r\n\r\nbar\"", @"VerbatimString 13 2 =5 ""@\""foo\r\n\r\nbar\""""")]
        [TestCase("@\"foo\r\n\""       , @"VerbatimString  8 1 =2 ""@\""foo\r\n\""""")]
        [TestCase("@\"foo\r\r\""       , @"VerbatimString  8 2 =2 ""@\""foo\r\r\""""")]
        [TestCase("@\"foo\n\n\""       , @"VerbatimString  8 2 =2 ""@\""foo\n\n\""""")]
        [TestCase("@\"foo\r\r\n\""     , @"VerbatimString  9 2 =2 ""@\""foo\r\r\n\""""")]
        [TestCase("@\"foo\n\r\r\""     , @"VerbatimString  9 3 =2 ""@\""foo\n\r\r\""""")]
        [TestCase("@\"foo\r\n\r\n\""   , @"VerbatimString 10 2 =2 ""@\""foo\r\n\r\n\""""")]

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

        [TestCase("$@$"                    , @"Text                        3 0  3 ""$@$""")]
        [TestCase("$@\"\""                 , @"InterpolatedVerbatimString  4 0  4 ""$@\""\""""")]
        [TestCase("$@\"foobar\""           , @"InterpolatedVerbatimString 10 0 10 ""$@\""foobar\""""")]
        [TestCase("$@\"foo\\\\bar\""       , @"InterpolatedVerbatimString 12 0 12 ""$@\""foo\\\\bar\""""")]
        [TestCase("$@\"foo{{bar}}baz\""    , @"InterpolatedVerbatimString 17 0 17 ""$@\""foo{{bar}}baz\""""")]
        [TestCase("$@\"foo\"\"bar\"\"baz\"", @"InterpolatedVerbatimString 17 0 17 ""$@\""foo\""\""bar\""\""baz\""""")]

        [TestCase("$@\"foo\r\nbar\""    , @"InterpolatedVerbatimString 12 1 =5 ""$@\""foo\r\nbar\""""")]
        [TestCase("$@\"foo\r\rbar\""    , @"InterpolatedVerbatimString 12 2 =5 ""$@\""foo\r\rbar\""""")]
        [TestCase("$@\"foo\n\nbar\""    , @"InterpolatedVerbatimString 12 2 =5 ""$@\""foo\n\nbar\""""")]
        [TestCase("$@\"foo\r\r\nbar\""  , @"InterpolatedVerbatimString 13 2 =5 ""$@\""foo\r\r\nbar\""""")]
        [TestCase("$@\"foo\n\r\rbar\""  , @"InterpolatedVerbatimString 13 3 =5 ""$@\""foo\n\r\rbar\""""")]
        [TestCase("$@\"foo\r\n\r\nbar\"", @"InterpolatedVerbatimString 14 2 =5 ""$@\""foo\r\n\r\nbar\""""")]
        [TestCase("$@\"foo\r\n\""       , @"InterpolatedVerbatimString  9 1 =2 ""$@\""foo\r\n\""""")]
        [TestCase("$@\"foo\r\r\""       , @"InterpolatedVerbatimString  9 2 =2 ""$@\""foo\r\r\""""")]
        [TestCase("$@\"foo\n\n\""       , @"InterpolatedVerbatimString  9 2 =2 ""$@\""foo\n\n\""""")]
        [TestCase("$@\"foo\r\r\n\""     , @"InterpolatedVerbatimString 10 2 =2 ""$@\""foo\r\r\n\""""")]
        [TestCase("$@\"foo\n\r\r\""     , @"InterpolatedVerbatimString 10 3 =2 ""$@\""foo\n\r\r\""""")]
        [TestCase("$@\"foo\r\n\r\n\""   , @"InterpolatedVerbatimString 11 2 =2 ""$@\""foo\r\n\r\n\""""")]

        [TestCase("$@\"foo\n{\n\"bar\"\n}\nbaz\"",
            @"InterpolatedVerbatimStringStart  8 1 =2 ""$@\""foo\n{""",
            @"NewLine                          1 1 =1 ""\n""",
            @"String                           5 0  5 ""\""bar\""""",
            @"NewLine                          1 1 =1 ""\n""",
            @"InterpolatedVerbatimStringEnd    6 1 =5 ""}\nbaz\""""")]

        [TestCase("$@\"x = {x}, y = {y}\"",
            @"InterpolatedVerbatimStringStart 8 0 8 ""$@\""x = {""",
            @"Text                            1 0 1 ""x""",
            @"InterpolatedVerbatimStringMid   8 0 8 ""}, y = {""",
            @"Text                            1 0 1 ""y""",
            @"InterpolatedVerbatimStringEnd   2 0 2 ""}\""""")]

        [TestCase("$@\"\" // blank",
            @"InterpolatedVerbatimString 4 0 4 ""$@\""\""""",
            @"WhiteSpace                 1 0 1 "" """,
            @"SingleLineComment          8 0 8 ""// blank""")]

        [TestCase("$@\"x = {(x < 0 ? 0 : x)}, y = {y}\"",
            @"InterpolatedVerbatimStringStart 8 0 8 ""$@\""x = {""",
            @"Text                            2 0 2 ""(x""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 ""<""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 ""0""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 ""?""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 ""0""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 "":""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            2 0 2 ""x)""",
            @"InterpolatedVerbatimStringMid   8 0 8 ""}, y = {""",
            @"Text                            1 0 1 ""y""",
            @"InterpolatedVerbatimStringEnd   2 0 2 ""}\""""")]

        [TestCase("$@\"today = { $\"{DateTime.Today:MMM dd, yyyy}\" }\"",
            @"InterpolatedVerbatimStringStart  12 0 12 ""$@\""today = {""",
            @"WhiteSpace                        1 0  1 "" """,
            @"InterpolatedStringStart           3 0  3 ""$\""{""",
            @"Text                             14 0 14 ""DateTime.Today""",
            @"InterpolatedStringEnd            15 0 15 "":MMM dd, yyyy}\""""",
            @"WhiteSpace                        1 0  1 "" """,
            @"InterpolatedVerbatimStringEnd     2 0  2 ""}\""""")]

        [TestCase("Console.WriteLine($@\"|{\"Left\",-7}|{\"Right\",7}|\");",
            @"Text                            18 0 18 ""Console.WriteLine(""",
            @"InterpolatedVerbatimStringStart  5 0  5 ""$@\""|{""",
            @"String                           6 0  6 ""\""Left\""""",
            @"InterpolatedVerbatimStringMid    6 0  6 "",-7}|{""",
            @"String                           7 0  7 ""\""Right\""""",
            @"InterpolatedVerbatimStringEnd    5 0  5 "",7}|\""""",
            @"Text                             2 0  2 "");""")]

        [TestCase("Console.WriteLine($@\"|{foo(12,34),-7}|{bar(56,78),7}|\");",
            @"Text                            18 0 18 ""Console.WriteLine(""",
            @"InterpolatedVerbatimStringStart  5 0  5 ""$@\""|{""",
            @"Text                            10 0 10 ""foo(12,34)""",
            @"InterpolatedVerbatimStringMid    6 0  6 "",-7}|{""",
            @"Text                            10 0 10 ""bar(56,78)""",
            @"InterpolatedVerbatimStringEnd    5 0  5 "",7}|\""""",
            @"Text                             2 0  2 "");""")]

        //
        [TestCase("@$$", @"Text                        3 0  3 ""@$$""")]
        [TestCase("@$\"\"", @"InterpolatedVerbatimString  4 0  4 ""@$\""\""""")]
        [TestCase("@$\"foobar\"", @"InterpolatedVerbatimString 10 0 10 ""@$\""foobar\""""")]
        [TestCase("@$\"foo\\\\bar\"", @"InterpolatedVerbatimString 12 0 12 ""@$\""foo\\\\bar\""""")]
        [TestCase("@$\"foo{{bar}}baz\"", @"InterpolatedVerbatimString 17 0 17 ""@$\""foo{{bar}}baz\""""")]
        [TestCase("@$\"foo\"\"bar\"\"baz\"", @"InterpolatedVerbatimString 17 0 17 ""@$\""foo\""\""bar\""\""baz\""""")]

        [TestCase("@$\"foo\r\nbar\"", @"InterpolatedVerbatimString 12 1 =5 ""@$\""foo\r\nbar\""""")]
        [TestCase("@$\"foo\r\rbar\"", @"InterpolatedVerbatimString 12 2 =5 ""@$\""foo\r\rbar\""""")]
        [TestCase("@$\"foo\n\nbar\"", @"InterpolatedVerbatimString 12 2 =5 ""@$\""foo\n\nbar\""""")]
        [TestCase("@$\"foo\r\r\nbar\"", @"InterpolatedVerbatimString 13 2 =5 ""@$\""foo\r\r\nbar\""""")]
        [TestCase("@$\"foo\n\r\rbar\"", @"InterpolatedVerbatimString 13 3 =5 ""@$\""foo\n\r\rbar\""""")]
        [TestCase("@$\"foo\r\n\r\nbar\"", @"InterpolatedVerbatimString 14 2 =5 ""@$\""foo\r\n\r\nbar\""""")]
        [TestCase("@$\"foo\r\n\"", @"InterpolatedVerbatimString  9 1 =2 ""@$\""foo\r\n\""""")]
        [TestCase("@$\"foo\r\r\"", @"InterpolatedVerbatimString  9 2 =2 ""@$\""foo\r\r\""""")]
        [TestCase("@$\"foo\n\n\"", @"InterpolatedVerbatimString  9 2 =2 ""@$\""foo\n\n\""""")]
        [TestCase("@$\"foo\r\r\n\"", @"InterpolatedVerbatimString 10 2 =2 ""@$\""foo\r\r\n\""""")]
        [TestCase("@$\"foo\n\r\r\"", @"InterpolatedVerbatimString 10 3 =2 ""@$\""foo\n\r\r\""""")]
        [TestCase("@$\"foo\r\n\r\n\"", @"InterpolatedVerbatimString 11 2 =2 ""@$\""foo\r\n\r\n\""""")]

        [TestCase("@$\"foo\n{\n\"bar\"\n}\nbaz\"",
            @"InterpolatedVerbatimStringStart  8 1 =2 ""@$\""foo\n{""",
            @"NewLine                          1 1 =1 ""\n""",
            @"String                           5 0  5 ""\""bar\""""",
            @"NewLine                          1 1 =1 ""\n""",
            @"InterpolatedVerbatimStringEnd    6 1 =5 ""}\nbaz\""""")]

        [TestCase("@$\"x = {x}, y = {y}\"",
            @"InterpolatedVerbatimStringStart 8 0 8 ""@$\""x = {""",
            @"Text                            1 0 1 ""x""",
            @"InterpolatedVerbatimStringMid   8 0 8 ""}, y = {""",
            @"Text                            1 0 1 ""y""",
            @"InterpolatedVerbatimStringEnd   2 0 2 ""}\""""")]

        [TestCase("@$\"\" // blank",
            @"InterpolatedVerbatimString 4 0 4 ""@$\""\""""",
            @"WhiteSpace                 1 0 1 "" """,
            @"SingleLineComment          8 0 8 ""// blank""")]

        [TestCase("@$\"x = {(x < 0 ? 0 : x)}, y = {y}\"",
            @"InterpolatedVerbatimStringStart 8 0 8 ""@$\""x = {""",
            @"Text                            2 0 2 ""(x""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 ""<""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 ""0""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 ""?""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 ""0""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            1 0 1 "":""",
            @"WhiteSpace                      1 0 1 "" """,
            @"Text                            2 0 2 ""x)""",
            @"InterpolatedVerbatimStringMid   8 0 8 ""}, y = {""",
            @"Text                            1 0 1 ""y""",
            @"InterpolatedVerbatimStringEnd   2 0 2 ""}\""""")]

        [TestCase("@$\"today = { $\"{DateTime.Today:MMM dd, yyyy}\" }\"",
            @"InterpolatedVerbatimStringStart  12 0 12 ""@$\""today = {""",
            @"WhiteSpace                        1 0  1 "" """,
            @"InterpolatedStringStart           3 0  3 ""$\""{""",
            @"Text                             14 0 14 ""DateTime.Today""",
            @"InterpolatedStringEnd            15 0 15 "":MMM dd, yyyy}\""""",
            @"WhiteSpace                        1 0  1 "" """,
            @"InterpolatedVerbatimStringEnd     2 0  2 ""}\""""")]

        [TestCase("Console.WriteLine(@$\"|{\"Left\",-7}|{\"Right\",7}|\");",
            @"Text                            18 0 18 ""Console.WriteLine(""",
            @"InterpolatedVerbatimStringStart  5 0  5 ""@$\""|{""",
            @"String                           6 0  6 ""\""Left\""""",
            @"InterpolatedVerbatimStringMid    6 0  6 "",-7}|{""",
            @"String                           7 0  7 ""\""Right\""""",
            @"InterpolatedVerbatimStringEnd    5 0  5 "",7}|\""""",
            @"Text                             2 0  2 "");""")]

        [TestCase("Console.WriteLine(@$\"|{foo(12,34),-7}|{bar(56,78),7}|\");",
            @"Text                            18 0 18 ""Console.WriteLine(""",
            @"InterpolatedVerbatimStringStart  5 0  5 ""@$\""|{""",
            @"Text                            10 0 10 ""foo(12,34)""",
            @"InterpolatedVerbatimStringMid    6 0  6 "",-7}|{""",
            @"Text                            10 0 10 ""bar(56,78)""",
            @"InterpolatedVerbatimStringEnd    5 0  5 "",7}|\""""",
            @"Text                             2 0  2 "");""")]

        //
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
            @"NewLine             2 1 -20 ""\r\n""",
            @"NewLine             2 1   0 ""\r\n""",
            @"Text                6 0   6 ""static""",
            @"WhiteSpace          1 0   1 "" """,
            @"Text                5 0   5 ""class""",
            @"WhiteSpace          1 0   1 "" """,
            @"Text                7 0   7 ""Program""",
            @"NewLine             2 1  =1 ""\r\n""",
            @"Text                1 0   1 ""{""",
            @"NewLine             2 1  =1 ""\r\n""",
            @"WhiteSpace          4 0   4 ""    """,
            @"SingleLineComment  62 0  62 ""// static readonly string s = \""This is a string in a comment\"";""",
            @"NewLine             2 1  =1 ""\r\n""",
            @"WhiteSpace          4 0   4 ""    """,
            @"Text                6 0   6 ""static""",
            @"WhiteSpace          1 0   1 "" """,
            @"Text                4 0   4 ""void""",
            @"WhiteSpace          1 0   1 "" """,
            @"Text                6 0   6 ""Main()""",
            @"NewLine             2 1  =1 ""\r\n""",
            @"WhiteSpace          4 0   4 ""    """,
            @"Text                1 0   1 ""{""",
            @"NewLine             2 1  =1 ""\r\n""",
            @"WhiteSpace          8 0   8 ""        """,
            @"Text               18 0  18 ""Console.WriteLine(""",
            @"String             14 0  14 ""\""Hello world!\""""",
            @"Text                2 0   2 "");""",
            @"NewLine             2 1  =1 ""\r\n""",
            @"WhiteSpace          4 0   4 ""    """,
            @"Text                1 0   1 ""}""",
            @"NewLine             2 1  =1 ""\r\n""",
            @"Text                1 0   1 ""}""",
            @"NewLine             2 1  =1 ""\r\n""")
        ]
        #endif
        public void Scan(string source, params string[] expectations)
        {
            var tokens =
                from t in Scanner.Scan(source)
                select $"{t} {JsonString.Encode(source, t.StartOffset, t.Length)}";

            Assert.That(
                tokens,
                Is.EqualTo(
                    from e in
                    expectations
                        .Select(e =>
                            e.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries)
                             .Fold((knd, oc, txt) => new
                             {
                                 Kind         = Enum.Parse<TokenKind>(knd, true),
                                 OffsetChange = int.Parse(oc, NumberStyles.None, CultureInfo.InvariantCulture),
                                 Text         = txt,
                             }))
                        .Scan(new
                              {
                                  Kind  = TokenKind.WhiteSpace,
                                  Start = 0,
                                  End   = 0,
                                  Text  = (string)null,
                              },
                              (s, e) => new
                              {
                                  e.Kind,
                                  Start = s.End,
                                  End = s.End + e.OffsetChange,
                                  e.Text,
                              })
                        .Skip(1)
                    select $"{e.Kind} [{e.Start}..{e.End}) {e.Text}"));
        }
    }
}
