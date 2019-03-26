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

    [Flags]
    public enum TokenKindTraits
    {
        None,
        Comment                 = 0x001,
        WhiteSpace              = 0x002,
        Literal                 = 0x004,
        String                  = 0x008,
        VerbatimString          = 0x010,
        InterpolatedString      = 0x020,
        InterpolatedStringStart = 0x040,
        InterpolatedStringMid   = 0x080,
        InterpolatedStringEnd   = 0x100,
    }
}

namespace CSharpMinifier
{
    using static TokenKindTraits;

    partial class TokenKindExtensions
    {
        static readonly TokenKindTraits[] TraitsByKind =
        {
            // IMPORTANT! Keep the order here in sync with TokenKind. If a
            // TokenKind member is moved, added or removed then this must be
            // changed appropriately as well.

            /* Text                                   */ None,
            /* WhiteSpace                             */ WhiteSpace,
            /* NewLine                                */ WhiteSpace,
            /* SingleLineComment                      */ Comment,
            /* MultiLineComment                       */ Comment,
            /* CharLiteral                            */ Literal,
            /* StringLiteral                          */ Literal | String,
            /* VerbatimStringLiteral                  */ Literal | String | VerbatimString,
            /* InterpolatedStringLiteral              */ Literal | String | InterpolatedString,
            /* InterpolatedStringLiteralStart         */ Literal | String | InterpolatedString | InterpolatedStringStart,
            /* InterpolatedStringLiteralMid           */ Literal | String | InterpolatedString | InterpolatedStringMid,
            /* InterpolatedStringLiteralEnd           */ Literal | String | InterpolatedString | InterpolatedStringEnd,
            /* InterpolatedVerbatimStringLiteral      */ Literal | String | InterpolatedString | VerbatimString,
            /* InterpolatedVerbatimStringLiteralStart */ Literal | String | InterpolatedString | VerbatimString | InterpolatedStringStart,
            /* InterpolatedVerbatimStringLiteralMid   */ Literal | String | InterpolatedString | VerbatimString | InterpolatedStringMid,
            /* InterpolatedVerbatimStringLiteralEnd   */ Literal | String | InterpolatedString | VerbatimString | InterpolatedStringEnd,
            /* PreprocessorDirective                  */ None,
        };
    }
}
