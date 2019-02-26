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
    using System.Collections.Generic;
    using System.Linq;

    public static class Minifier
    {
        public static IEnumerable<string> Minify(string source) =>
            Minify(source, Environment.NewLine);

        public static IEnumerable<string> Minify(string source, string newLine) =>
            Minify(source, " ", newLine, t => source.Substring(t.Start.Offset, t.Length));

        public static IEnumerable<TResult>
            Minify<TResult>(string source,
                            TResult space, TResult newLine,
                            Func<Token, TResult> resultSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return _(); IEnumerable<TResult> _()
            {
                var tokens =
                    from t in Scanner.Scan(source)
                    where t.Kind != TokenKind.MultiLineComment
                       && t.Kind != TokenKind.SingleLineComment
                       && t.Kind != TokenKind.WhiteSpace
                    select t;

                bool IsWordChar(char ch) =>
                    char.IsLetter(ch) || ch >= '0' && ch <= '9' || ch == '_';

                var lastCh = (char?)null;
                foreach (var t in tokens)
                {
                    if (lastCh is char lch && IsWordChar(lch) && IsWordChar(source[t.Start.Offset]))
                        yield return space;

                    yield return resultSelector(t);

                    if (t.Kind == TokenKind.PreprocessorDirective)
                    {
                        yield return newLine;
                        lastCh = null;
                    }
                    else
                    {
                        lastCh = source[t.End.Offset - 1];
                    }
                }
            }
        }
    }
}
