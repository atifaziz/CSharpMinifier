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

namespace CSharpMinifier.Preprocessing
{
    using System;

    public enum TokenKind
    {
        WhiteSpace,
        Identifier,
        True,
        False,
        And,
        Or,
        LParen,
        RParen,
        Not,
        NotEqual,
    }

    public readonly struct Token : IEquatable<Token>
    {
        public readonly TokenKind Kind;
        public readonly int StartOffset;
        public readonly int EndOffset;

        public Token(TokenKind kind, int startOffset, int endOffset) =>
            (Kind, StartOffset, EndOffset) = (kind, startOffset, endOffset);

        public int Length => EndOffset - StartOffset;

        public bool Equals(Token other) =>
            Kind == other.Kind
            && StartOffset.Equals(other.StartOffset)
            && EndOffset.Equals(other.EndOffset);

        public override bool Equals(object obj) =>
            obj is Token other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((((int) Kind * 397) ^ StartOffset.GetHashCode()) * 397) ^ EndOffset.GetHashCode());

        public static bool operator ==(Token left, Token right) => left.Equals(right);
        public static bool operator !=(Token left, Token right) => !left.Equals(right);

        public override string ToString() =>
            $"{Kind} [{StartOffset}..{EndOffset})";
    }

    public static class TokenExtensions
    {
        public static string Substring(this Token token, string source) =>
            SubstringPool.GetOrCreate(source, token.StartOffset, token.Length);
    }
}
