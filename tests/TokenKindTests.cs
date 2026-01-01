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

using NUnit.Framework;

namespace CSharpMinifier.Tests;

#pragma warning disable IDE0065 // Misplaced using directive
using static TokenKindTraits;
#pragma warning restore IDE0065 // Misplaced using directive

[TestFixture]
public class TokenKindTests
{
    [TestCase(TokenKind.Text                                  , None)]
    [TestCase(TokenKind.WhiteSpace                            , WhiteSpace)]
    [TestCase(TokenKind.NewLine                               , WhiteSpace)]
    [TestCase(TokenKind.SingleLineComment                     , Comment)]
    [TestCase(TokenKind.MultiLineComment                      , Comment)]
    [TestCase(TokenKind.CharLiteral                           , Literal)]
    [TestCase(TokenKind.StringLiteral                         , Literal | String)]
    [TestCase(TokenKind.VerbatimStringLiteral                 , Literal | String | VerbatimString)]
    [TestCase(TokenKind.InterpolatedStringLiteral             , Literal | String | InterpolatedString)]
    [TestCase(TokenKind.InterpolatedStringLiteralStart        , Literal | String | InterpolatedString | InterpolatedStringStart)]
    [TestCase(TokenKind.InterpolatedStringLiteralMid          , Literal | String | InterpolatedString | InterpolatedStringMid)]
    [TestCase(TokenKind.InterpolatedStringLiteralEnd          , Literal | String | InterpolatedString | InterpolatedStringEnd)]
    [TestCase(TokenKind.InterpolatedVerbatimStringLiteral     , Literal | String | InterpolatedString | VerbatimString)]
    [TestCase(TokenKind.InterpolatedVerbatimStringLiteralStart, Literal | String | InterpolatedString | VerbatimString | InterpolatedStringStart)]
    [TestCase(TokenKind.InterpolatedVerbatimStringLiteralMid  , Literal | String | InterpolatedString | VerbatimString | InterpolatedStringMid)]
    [TestCase(TokenKind.InterpolatedVerbatimStringLiteralEnd  , Literal | String | InterpolatedString | VerbatimString | InterpolatedStringEnd)]
    [TestCase(TokenKind.PreprocessorDirective                 , None)]

    public void Traits(TokenKind kind, TokenKindTraits traits)
    {
        Assert.That(kind.Traits            , Is.EqualTo(traits));
        Assert.That(kind.HasTraits(traits) , Is.True);
        Assert.That(kind.HasTraits(~traits), Is.False);
    }
}
