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
        Text,
        WhiteSpace,
        SingleLineComment,
        MultiLineComment,
        StringLiteral,
        VerbatimStringLiteral,
        InterpolatedStringLiteral,
        InterpolatedStringStart,
        InterpolatedStringMid,
        InterpolatedStringEnd,
        InterpolatedVerbatimStringLiteral,
        InterpolatedVerbatimStringStart,
        InterpolatedVerbatimStringMid,
        InterpolatedVerbatimStringEnd,
        CharLiteral,
        PreprocessorDirective,
    }

    public readonly struct Token : IEquatable<Token>
    {
        public readonly TokenKind Kind;
        public readonly Position Start;
        public readonly Position End;

        public Token(TokenKind kind, Position start, Position end) =>
            (Kind, Start, End) = (kind, start, end);

        public int Length => End.Offset - Start.Offset;

        public bool Equals(Token other) =>
            Kind == other.Kind && Start.Equals(other.Start) && End.Equals(other.End);

        public override bool Equals(object obj) =>
            obj is Token other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((((int)Kind * 397) ^ Start.GetHashCode()) * 397) ^ End.GetHashCode());

        public static bool operator ==(Token left, Token right) => left.Equals(right);
        public static bool operator !=(Token left, Token right) => !left.Equals(right);

        public override string ToString() =>
            $"{Kind} [{Start}..{End})";
    }
}
