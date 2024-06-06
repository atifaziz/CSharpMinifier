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
    using System.Linq;
    using NUnit.Framework;

    [TestFixture]
#pragma warning disable CA1052 // Static holder types should be Static or NotInheritable
    public class TokenTests
#pragma warning restore CA1052 // Static holder types should be Static or NotInheritable
    {
        public class Substring
        {
            [TestCase("as")]
            [TestCase("by")]
            [TestCase("do")]
            [TestCase("if")]
            [TestCase("in")]
            [TestCase("is")]
            [TestCase("on")]
            [TestCase("add")]
            [TestCase("for")]
            [TestCase("get")]
            [TestCase("int")]
            [TestCase("let")]
            [TestCase("new")]
            [TestCase("out")]
            [TestCase("ref")]
            [TestCase("set")]
            [TestCase("try")]
            [TestCase("var")]
            [TestCase("base")]
            [TestCase("bool")]
            [TestCase("byte")]
            [TestCase("case")]
            [TestCase("char")]
            [TestCase("else")]
            [TestCase("enum")]
            [TestCase("from")]
            [TestCase("goto")]
            [TestCase("into")]
            [TestCase("join")]
            [TestCase("lock")]
            [TestCase("long")]
            [TestCase("null")]
            [TestCase("this")]
            [TestCase("true")]
            [TestCase("uint")]
            [TestCase("void")]
            [TestCase("when")]
            [TestCase("alias")]
            [TestCase("async")]
            [TestCase("await")]
            [TestCase("break")]
            [TestCase("catch")]
            [TestCase("class")]
            [TestCase("const")]
            [TestCase("event")]
            [TestCase("false")]
            [TestCase("fixed")]
            [TestCase("float")]
            [TestCase("group")]
            [TestCase("sbyte")]
            [TestCase("short")]
            [TestCase("throw")]
            [TestCase("ulong")]
            [TestCase("using")]
            [TestCase("value")]
            [TestCase("where")]
            [TestCase("while")]
            [TestCase("yield")]
            [TestCase("double")]
            [TestCase("equals")]
            [TestCase("extern")]
            [TestCase("global")]
            [TestCase("nameof")]
            [TestCase("object")]
            [TestCase("params")]
            [TestCase("public")]
            [TestCase("remove")]
            [TestCase("return")]
            [TestCase("sealed")]
            [TestCase("select")]
            [TestCase("sizeof")]
            [TestCase("static")]
            [TestCase("string")]
            [TestCase("struct")]
            [TestCase("switch")]
            [TestCase("typeof")]
            [TestCase("unsafe")]
            [TestCase("ushort")]
            [TestCase("checked")]
            [TestCase("decimal")]
            [TestCase("default")]
            [TestCase("dynamic")]
            [TestCase("finally")]
            [TestCase("foreach")]
            [TestCase("orderby")]
            [TestCase("partial")]
            [TestCase("private")]
            [TestCase("virtual")]
            [TestCase("abstract")]
            [TestCase("continue")]
            [TestCase("delegate")]
            [TestCase("explicit")]
            [TestCase("implicit")]
            [TestCase("internal")]
            [TestCase("operator")]
            [TestCase("override")]
            [TestCase("readonly")]
            [TestCase("volatile")]
            [TestCase("ascending")]
            [TestCase("interface")]
            [TestCase("namespace")]
            [TestCase("protected")]
            [TestCase("unchecked")]
            [TestCase("descending")]
            [TestCase("stackalloc")]
            public void KeywordPool(string keyword)
            {
                var source = $"\"\"{keyword}\"\"";
                var tokens = Scanner.Scan(source);
                var token = tokens.Single(t => t.Kind == TokenKind.Text);

                var k1 = token.Substring(source);
                Assert.That(k1, Is.EqualTo(keyword));
                Assert.That(k1, Is.Not.SameAs(keyword));

                var k2 = token.Substring(source);
                Assert.That(k2, Is.EqualTo(keyword));
                Assert.That(k2, Is.Not.SameAs(keyword));

                Assert.That(k1, Is.SameAs(k2));
            }

            [TestCase("iS")]
            [TestCase("aDD")]
            [TestCase("fOR")]
            [TestCase("gET")]
            [TestCase("iNT")]
            [TestCase("lET")]
            [TestCase("nEW")]
            [TestCase("oUT")]
            [TestCase("rEF")]
            [TestCase("sET")]
            [TestCase("tRY")]
            [TestCase("vAR")]
            [TestCase("baSE")]
            [TestCase("boOL")]
            [TestCase("byTE")]
            [TestCase("caSE")]
            [TestCase("chAR")]
            [TestCase("elSE")]
            [TestCase("enUM")]
            [TestCase("fROM")]
            [TestCase("gOTO")]
            [TestCase("iNTO")]
            [TestCase("jOIN")]
            [TestCase("loCK")]
            [TestCase("loNG")]
            [TestCase("nULL")]
            [TestCase("thIS")]
            [TestCase("trUE")]
            [TestCase("uINT")]
            [TestCase("vOID")]
            [TestCase("wHEN")]
            [TestCase("alIAS")]
            [TestCase("asYNC")]
            [TestCase("awAIT")]
            [TestCase("bREAK")]
            [TestCase("caTCH")]
            [TestCase("clASS")]
            [TestCase("coNST")]
            [TestCase("eVENT")]
            [TestCase("faLSE")]
            [TestCase("fiXED")]
            [TestCase("flOAT")]
            [TestCase("gROUP")]
            [TestCase("sbYTE")]
            [TestCase("shORT")]
            [TestCase("thROW")]
            [TestCase("ulONG")]
            [TestCase("usING")]
            [TestCase("vALUE")]
            [TestCase("wheRE")]
            [TestCase("whiLE")]
            [TestCase("yIELD")]
            [TestCase("dOUBLE")]
            [TestCase("eqUALS")]
            [TestCase("exTERN")]
            [TestCase("gLOBAL")]
            [TestCase("nAMEOF")]
            [TestCase("oBJECT")]
            [TestCase("paRAMS")]
            [TestCase("puBLIC")]
            [TestCase("remOVE")]
            [TestCase("retURN")]
            [TestCase("seaLED")]
            [TestCase("selECT")]
            [TestCase("siZEOF")]
            [TestCase("staTIC")]
            [TestCase("strING")]
            [TestCase("stRUCT")]
            [TestCase("swITCH")]
            [TestCase("tYPEOF")]
            [TestCase("unSAFE")]
            [TestCase("usHORT")]
            [TestCase("cHECKED")]
            [TestCase("decIMAL")]
            [TestCase("defAULT")]
            [TestCase("dyNAMIC")]
            [TestCase("fiNALLY")]
            [TestCase("foREACH")]
            [TestCase("oRDERBY")]
            [TestCase("paRTIAL")]
            [TestCase("prIVATE")]
            [TestCase("vIRTUAL")]
            [TestCase("aBSTRACT")]
            [TestCase("cONTINUE")]
            [TestCase("dELEGATE")]
            [TestCase("eXPLICIT")]
            [TestCase("imPLICIT")]
            [TestCase("inTERNAL")]
            [TestCase("opERATOR")]
            [TestCase("ovERRIDE")]
            [TestCase("rEADONLY")]
            [TestCase("vOLATILE")]
            [TestCase("aSCENDING")]
            [TestCase("iNTERFACE")]
            [TestCase("nAMESPACE")]
            [TestCase("pROTECTED")]
            [TestCase("uNCHECKED")]
            [TestCase("dESCENDING")]
            [TestCase("sTACKALLOC")]
            public void Unique(string word)
            {
                var source = $"\"\"{word}\"\"";
                var tokens = Scanner.Scan(source);
                var token = tokens.Single(t => t.Kind == TokenKind.Text);

                var s1 = token.Substring(source);
                Assert.That(s1, Is.EqualTo(word));
                Assert.That(s1, Is.Not.SameAs(word));

                var s2 = token.Substring(source);
                Assert.That(s2, Is.EqualTo(word));
                Assert.That(s2, Is.Not.SameAs(word));

                Assert.That(s1, Is.Not.SameAs(s2));
            }

            [TestCase("\r")]
            [TestCase("\n")]
            [TestCase("\r\n")]
            public void NewLinePool(string chars)
            {
                var source = $"foo{chars}bar";
                var tokens = Scanner.Scan(source);
                var token = tokens.Single(t => t.Kind == TokenKind.NewLine);

                var s1 = token.Substring(source);
                Assert.That(s1, Is.EqualTo(chars));
                Assert.That(s1, Is.Not.SameAs(chars));

                var s2 = token.Substring(source);
                Assert.That(s2, Is.EqualTo(chars));
                Assert.That(s2, Is.Not.SameAs(chars));

                Assert.That(s1, Is.SameAs(s2));
            }

            [TestCase(' ', 256)]
            [TestCase('\t', 32)]
            public void WhiteSpacePool(char ch, int max)
            {
                for (var repeat = 0; repeat < 1; repeat++)
                {
                    for (var count = 1; count <= max; count++)
                    {
                        var ws = new string(ch, count);
                        var source = $"foo{ws}bar";
                        var tokens = Scanner.Scan(source);
                        var token = tokens.Single(t => t.Kind == TokenKind.WhiteSpace);

                        var s1 = token.Substring(source);
                        Assert.That(s1, Is.EqualTo(ws));
                        Assert.That(s1, Is.Not.SameAs(ws));

                        var s2 = token.Substring(source);
                        Assert.That(s2, Is.EqualTo(ws));
                        Assert.That(s2, Is.Not.SameAs(ws));

                        Assert.That(s1, Is.SameAs(s2));
                    }
                }
            }

            [TestCase(' ', 257)]
            [TestCase('\t', 33)]
            public void UniqueWhiteSpace(char ch, int count)
            {
                var ws = new string(ch, count);
                var source = $"foo{ws}bar";
                var tokens = Scanner.Scan(source);
                var token = tokens.Single(t => t.Kind == TokenKind.WhiteSpace);

                var s1 = token.Substring(source);
                Assert.That(s1, Is.EqualTo(ws));
                Assert.That(s1, Is.Not.SameAs(ws));

                var s2 = token.Substring(source);
                Assert.That(s2, Is.EqualTo(ws));
                Assert.That(s2, Is.Not.SameAs(ws));

                Assert.That(s1, Is.Not.SameAs(s2));
            }

            [TestCase(" \t", true)]
            [TestCase(" \t \t", false)]
            public void MixedWhiteSpace(string ws, bool prime)
            {
                if (prime)
                {
                    var spaces = new string(' ', ws.Length);
                    var source = $"foo{spaces}bar";
                    Assert.That(Scanner.Scan(source)
                                       .Single(t => t.Kind == TokenKind.WhiteSpace)
                                       .Substring(source),
                                Is.EqualTo(spaces));
                }

                {
                    var source = $"foo{ws}bar";
                    var tokens = Scanner.Scan(source);
                    var token = tokens.Single(t => t.Kind == TokenKind.WhiteSpace);

                    var s1 = token.Substring(source);
                    Assert.That(s1, Is.EqualTo(ws));
                    Assert.That(s1, Is.Not.SameAs(ws));

                    var s2 = token.Substring(source);
                    Assert.That(s2, Is.EqualTo(ws));
                    Assert.That(s2, Is.Not.SameAs(ws));

                    Assert.That(s1, Is.Not.SameAs(s2));
                }
            }

            [Test]
            public void Empty()
            {
                var token = default(Token);
                var s = token.Substring(string.Empty);
                Assert.That(s, Is.SameAs(string.Empty));
            }
        }
    }
}
