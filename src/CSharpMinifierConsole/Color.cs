#region Copyright (c) 2010 Atif Aziz. All rights reserved.
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

namespace CSharpMinifierConsole
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Text.RegularExpressions;

    [DebuggerDisplay("Foreground = {Foreground}, Background = {Background}")]
    readonly struct Color : IEquatable<Color>
    {
        public ConsoleColor? Foreground { get; }
        public ConsoleColor? Background { get; }

        public Color(ConsoleColor? foreground, ConsoleColor? background = null) : this()
        {
            Foreground = foreground;
            Background = background;
        }

        public void Do(Action<ConsoleColor> onForeground, Action<ConsoleColor> onBackground)
        {
            if (Background is ConsoleColor bg) onBackground(bg);
            if (Foreground is ConsoleColor fg) onForeground(fg);
        }

        public bool Equals(Color other) =>
            Foreground == other.Foreground && Background == other.Background;

        public override bool Equals(object obj) =>
            obj is Color color && Equals(color);

        public override int GetHashCode() =>
            unchecked((Foreground.GetHashCode() * 397) ^ Background.GetHashCode());

        public static bool operator ==(Color a, Color b) => a.Equals(b);
        public static bool operator !=(Color a, Color b) => !(a == b);

        public static Color Console
        {
            get => new Color(System.Console.ForegroundColor,
                System.Console.BackgroundColor);
            set => value.Do(fg => System.Console.ForegroundColor = fg,
                bg => System.Console.BackgroundColor = bg);
        }

        public static Color Parse(string input)
        {
            Color color;
            if (input.Length > 0 && IsHexChar(input[0])
                                 && (input.Length == 1 || (input.Length == 2 && IsHexChar(input[1]))))
            {
                var n = int.Parse(input, NumberStyles.HexNumber);
                color = new Color((ConsoleColor) (n & 0xf), (ConsoleColor) (n >> 4));
            }
            else
            {
                var tokens = input.Split('/', 2);
                color = new Color(ParseConsoleColor(tokens[0]),
                    tokens.Length > 1 ? ParseConsoleColor(tokens[1]) : null);
            }
            return color;
        }

        static bool IsHexChar(char ch) => (ch >= '0' && ch <= '9')
                                          || (ch >= 'a' && ch <= 'f')
                                          || (ch >= 'A' && ch <= 'F');

        static ConsoleColor? ParseConsoleColor(string input)
        {
            if (input.Length == 0) return null;
            if (!Regex.IsMatch(input, " *[a-zA-Z]+ *", RegexOptions.CultureInvariant))
                throw new FormatException("Color name syntax error.");
            return input.Length > 0
                ? Enum.Parse<ConsoleColor>(input, true)
                : (ConsoleColor?)null;
        }
    }
}
