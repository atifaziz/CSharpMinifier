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
    using System.Linq;
    using NUnit.Framework;
    using static TokenKindTraits;

    [TestFixture]
    public class TokenKindTests
    {
        [TestCase(TokenKind.Text                                  , None)]
        [TestCase(TokenKind.WhiteSpace                            , WhiteSpace)]
        [TestCase(TokenKind.NewLine                               , WhiteSpace)]
        [TestCase(TokenKind.SingleLineComment                     , Comment)]
        [TestCase(TokenKind.MultiLineComment                      , Comment)]
        [TestCase(TokenKind.CharLiteral                           , Literal)]
        [TestCase(TokenKind.StringLiteral                         , Literal | TokenKindTraits.String)]
        [TestCase(TokenKind.VerbatimStringLiteral                 , Literal | TokenKindTraits.String | VerbatimString)]
        [TestCase(TokenKind.InterpolatedStringLiteral             , Literal | TokenKindTraits.String | InterpolatedString)]
        [TestCase(TokenKind.InterpolatedStringLiteralStart        , Literal | TokenKindTraits.String | InterpolatedString | InterpolatedStringStart)]
        [TestCase(TokenKind.InterpolatedStringLiteralMid          , Literal | TokenKindTraits.String | InterpolatedString | InterpolatedStringMid)]
        [TestCase(TokenKind.InterpolatedStringLiteralEnd          , Literal | TokenKindTraits.String | InterpolatedString | InterpolatedStringEnd)]
        [TestCase(TokenKind.InterpolatedVerbatimStringLiteral     , Literal | TokenKindTraits.String | InterpolatedString | VerbatimString)]
        [TestCase(TokenKind.InterpolatedVerbatimStringLiteralStart, Literal | TokenKindTraits.String | InterpolatedString | VerbatimString | InterpolatedStringStart)]
        [TestCase(TokenKind.InterpolatedVerbatimStringLiteralMid  , Literal | TokenKindTraits.String | InterpolatedString | VerbatimString | InterpolatedStringMid)]
        [TestCase(TokenKind.InterpolatedVerbatimStringLiteralEnd  , Literal | TokenKindTraits.String | InterpolatedString | VerbatimString | InterpolatedStringEnd)]
        [TestCase(TokenKind.PreprocessorDirective                 , None)]

        public void Traits(TokenKind kind, TokenKindTraits traits)
        {
            Assert.That(kind.GetTraits()       , Is.EqualTo(traits));
            Assert.That(kind.HasTraits(traits) , Is.True);
            Assert.That(kind.HasTraits(~traits), Is.False);
        }

        [Test]
        public void InvalidKind()
        {
            var kinds = (TokenKind[])Enum.GetValues(typeof(TokenKind));

            var min = kinds.Min();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                (min - 1).GetTraits());

            var max = kinds.Max();
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                (max + 1).GetTraits());
        }
    }
}
