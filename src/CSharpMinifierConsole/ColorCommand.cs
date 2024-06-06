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

using System;
using System.Linq;
using CSharpMinifier;

partial class Program
{
    static void ColorCommand(ProgramArguments args)
    {
        var showLineEndings = args.OptEol;

        var defaultColor = Color.Console;

        var magenta = new Color(ConsoleColor.Magenta, defaultColor.Background);
        var black   = new Color(ConsoleColor.Black  , ConsoleColor.DarkGray);
        var green   = new Color(ConsoleColor.Green  , defaultColor.Background);
        var yellow  = new Color(ConsoleColor.Yellow , defaultColor.Background);
        var cyan    = new Color(ConsoleColor.Cyan   , defaultColor.Background);
        var blue    = new Color(ConsoleColor.Blue   , defaultColor.Background);
        var gray    = new Color(ConsoleColor.Gray   , defaultColor.Background);

        var defaultPalette = new(TokenKind TokenKind, Color)[]
        {
            (TokenKind.Text                                  , gray),
            (TokenKind.WhiteSpace                            , defaultColor),
            (TokenKind.NewLine                               , black),
            (TokenKind.SingleLineComment                     , green),
            (TokenKind.MultiLineComment                      , green),
            (TokenKind.StringLiteral                         , yellow),
            (TokenKind.VerbatimStringLiteral                 , yellow),
            (TokenKind.InterpolatedStringLiteral             , magenta),
            (TokenKind.InterpolatedStringLiteralStart        , magenta),
            (TokenKind.InterpolatedStringLiteralMid          , magenta),
            (TokenKind.InterpolatedStringLiteralEnd          , magenta),
            (TokenKind.InterpolatedVerbatimStringLiteral     , magenta),
            (TokenKind.InterpolatedVerbatimStringLiteralStart, magenta),
            (TokenKind.InterpolatedVerbatimStringLiteralMid  , magenta),
            (TokenKind.InterpolatedVerbatimStringLiteralEnd  , magenta),
            (TokenKind.CharLiteral                           , cyan),
            (TokenKind.PreprocessorDirective                 , blue),
        };

        var colorByTokenKind = new Color[defaultPalette.Max(e => (int)e.TokenKind) + 1];
        foreach (var (kind, color) in defaultPalette)
            colorByTokenKind[(int)kind] = color;

        try
        {
            foreach (var (_, source) in ReadSources(args.ArgFile, args.OptGlobDirInfo))
            {
                foreach (var token in Scanner.Scan(source))
                {
                    Color.Console = colorByTokenKind[(int)token.Kind];
                    var text = source.Substring(token.Start.Offset, token.Length);
                    if (token.Kind == TokenKind.NewLine)
                    {
                        if (showLineEndings)
                            Console.Write(text.Replace("\r", "[CR]", StringComparison.Ordinal)
                                              .Replace("\n", "[LF]", StringComparison.Ordinal));
                        Color.Console = defaultColor;
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.Write(text);
                    }
                }
            }
        }
        finally
        {
            Color.Console = defaultColor;
        }
    }
}
