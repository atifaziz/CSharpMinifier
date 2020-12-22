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
    using System.Text.RegularExpressions;

    public sealed class MinificationOptions
    {
        public static readonly MinificationOptions Default = new(null, false);

        public Func<Token, string, bool>? CommentFilter { get; private set; }
        public bool KeepLeadComment { get; private set; }

        MinificationOptions(Func<Token, string, bool>? commentFilter, bool keepLeadComment)
        {
            CommentFilter = commentFilter;
            KeepLeadComment = keepLeadComment;
        }

        MinificationOptions(MinificationOptions options) :
            this(options.CommentFilter, options.KeepLeadComment) {}

        public MinificationOptions WithCommentFilter(Func<Token, string, bool>? value)
            => CommentFilter == value ? this
             : value == Default.CommentFilter && KeepLeadComment == Default.KeepLeadComment ? Default
             : new MinificationOptions(this) { CommentFilter = value };

        public MinificationOptions OrCommentFilterOf(MinificationOptions other) =>
            (CommentFilter, other.CommentFilter) switch
            {
                (null, null) => this,
                var (left, right) when left == right => this,
                ({}, null) => this,
                (null, {} right) => WithCommentFilter(right),
                ({} left, {} right) => WithCommentFilter((t, s) => left(t, s) || right(t, s)),
            };

        public MinificationOptions FilterImportantComments() =>
            FilterCommentMatching("^/[/*]!");

        public MinificationOptions FilterCommentMatching(string pattern) =>
            FilterCommentMatching(pattern, RegexOptions.None);

        public MinificationOptions FilterCommentMatching(string pattern, RegexOptions options)
            => pattern == null ? throw new ArgumentNullException(nameof(pattern))
             : WithCommentFilter((t, s) => Regex.IsMatch(t.Substring(s), pattern, options));

        public MinificationOptions WithKeepLeadComment(bool value)
            => KeepLeadComment == value ? this
             : value == Default.KeepLeadComment && CommentFilter == Default.CommentFilter ? Default
             : new MinificationOptions(this) { KeepLeadComment = value };

        public bool ShouldFilterComment(Token token, string text) =>
            CommentFilter is {} filter && filter(token, text);
    }

    public static class Minifier
    {
        public static IEnumerable<string> Minify(string source) =>
            Minify(source, MinificationOptions.Default);

        public static IEnumerable<string> Minify(string source, MinificationOptions options) =>
            Minify(source, Environment.NewLine, options);

        public static IEnumerable<string> Minify(string source, string newLine) =>
            Minify(source, newLine, MinificationOptions.Default);

        public static IEnumerable<string> Minify(string source, string newLine,
                                                 MinificationOptions options) =>
            Minify(source, " ", newLine, options, t => t.Substring(source));

        public static IEnumerable<TResult>
            Minify<TResult>(string source,
                TResult space, TResult newLine,
                Func<Token, TResult> resultSelector) =>
            Minify(source, space, newLine, MinificationOptions.Default, resultSelector);

        enum LeadCommentState
        {
            Awaiting,
            Processing,
            Processed,
        }

        public static IEnumerable<TResult>
            Minify<TResult>(string source,
                            TResult space, TResult newLine,
                            MinificationOptions options,
                            Func<Token, TResult> resultSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return _(); IEnumerable<TResult> _()
            {
                static bool IsSpaceOrTab (char ch) => ch is ' ' or '\t';
                static bool IsAsciiLetter(char ch) => (ch & ~0x20) is >= 'A' and <= 'z';
                static bool IsWordChar   (char ch) => char.IsLetter(ch)
                                                   || ch is (>= '0' and <= '9') or '_';

                var lcs = LeadCommentState.Awaiting;
                var lastCh = (char?)null;
                var lastSingleLineCommentLine = (int?)null;

                TResult NewLineWhileResettingLastChar()
                {
                    lastCh = null;
                    return newLine;
                }

                foreach (var t in Scanner.Scan(source))
                {
                    switch (t.Kind)
                    {
                        case TokenKind.NewLine:
                        case TokenKind.WhiteSpace:
                            break;

                        case var k
                            when k.HasTraits(TokenKindTraits.Comment)
                              && options.KeepLeadComment
                              && lcs != LeadCommentState.Processed
                              && (   lastSingleLineCommentLine is null
                                  || lastSingleLineCommentLine is {} ln
                                  && t.Start.Line - ln == 1):
                        {
                            yield return resultSelector(t);
                            if (k == TokenKind.SingleLineComment)
                            {
                                lastSingleLineCommentLine = t.Start.Line;
                                yield return NewLineWhileResettingLastChar();
                                lcs = LeadCommentState.Processing;
                            }
                            else
                            {
                                    lcs = LeadCommentState.Processed;
                            }
                            break;
                        }

                        case var k
                            when k.HasTraits(TokenKindTraits.Comment)
                              && options.ShouldFilterComment(t, source):
                        {
                            yield return resultSelector(t);
                            if (k == TokenKind.SingleLineComment)
                                yield return NewLineWhileResettingLastChar();
                            break;
                        }

                        case var k when k.HasTraits(TokenKindTraits.Comment):
                            continue;

                        default:
                        {
                            lcs = LeadCommentState.Processed;

                            if (t.Kind == TokenKind.PreprocessorDirective)
                            {
                                var tei = t.End.Offset;

                                var si = t.Start.Offset + 1;
                                while (si < tei && IsSpaceOrTab(source[si]))
                                    si++;

                                var ei = si;
                                while (ei < tei && IsAsciiLetter(source[ei]))
                                    ei++;

                                var length = ei - si;

                                if (length == 0
                                    || string.CompareOrdinal("region", 0, source, si, length) != 0
                                    && string.CompareOrdinal("endregion", 0, source, si, length) != 0)
                                {
                                    if (lastCh != null)
                                        yield return newLine;

                                    yield return resultSelector(t);
                                    yield return NewLineWhileResettingLastChar();
                                }
                            }
                            else
                            {
                                if (lastCh is {} lch)
                                {
                                    var ch = source[t.Start.Offset];
                                    if (IsWordChar(ch) && IsWordChar(lch) || ch == lch && (ch == '+' || ch == '-' || ch == '*'))
                                        yield return space;
                                }

                                yield return resultSelector(t);
                                lastCh = source[t.End.Offset - 1];
                            }

                            break;
                        }
                    }
                }
            }
        }
    }
}
