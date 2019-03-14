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

    partial struct Token
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
