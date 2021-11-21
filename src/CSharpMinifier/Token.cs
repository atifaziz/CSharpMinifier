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

    public enum TokenKind
    {
        // IMPORTANT! Keep the order here in sync with TraitsByTokenKind
        // member of TokenKind. If a member is moved, added or removed then
        // TokenKind.TraitsByTokenKind must be changed appropriately as well.

        Text,
        WhiteSpace,
        NewLine,
        SingleLineComment,
        MultiLineComment,
        CharLiteral,
        StringLiteral,
        VerbatimStringLiteral,
        InterpolatedStringLiteral,
        InterpolatedStringLiteralStart,
        InterpolatedStringLiteralMid,
        InterpolatedStringLiteralEnd,
        InterpolatedVerbatimStringLiteral,
        InterpolatedVerbatimStringLiteralStart,
        InterpolatedVerbatimStringLiteralMid,
        InterpolatedVerbatimStringLiteralEnd,
        PreprocessorDirective,
    }

    public static partial class TokenKindExtensions
    {
        public static TokenKindTraits GetTraits(this TokenKind kind)
        {
            var i = (int)kind;
            return i >= 0 && i < TraitsByKind.Length
                 ? TraitsByKind[i]
                 : throw new ArgumentOutOfRangeException(nameof(kind));
        }

        public static bool HasTraits(this TokenKind kind, TokenKindTraits traits) =>
            (kind.GetTraits() & traits) == traits;
    }

    public readonly record struct Token(TokenKind Kind, Position Start, Position End)
    {
        public int Length => End.Offset - Start.Offset;

        [Obsolete("Use " + nameof(TokenKindExtensions) + "." + nameof(TokenKindExtensions.GetTraits) + " instead.")]
        public TokenKindTraits Traits => Kind.GetTraits();

        public override string ToString() =>
            $"{Kind} [{Start}..{End})";
    }

    public static class TokenExtensions
    {
        public static string Substring(this Token token, string source) =>
            SubstringPool.GetOrCreate(source, token.Start.Offset, token.Length);
    }
}
