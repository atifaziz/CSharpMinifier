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

namespace CSharpMinifier.Tests
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using NUnit.Framework;

    public class MinificationOptionsTests
    {
        [Test]
        public void Default()
        {
            Assert.That(MinificationOptions.Default, Is.Not.Null);
            Assert.That(MinificationOptions.Default.CommentFilter, Is.Null);
            Assert.That(MinificationOptions.Default.KeepLeadComment, Is.False);
        }

        [Test]
        public void SetCommentFilterToFunction()
        {
            var filter = new Func<Token, string, bool>(delegate { throw new NotImplementedException(); });
            var options = MinificationOptions.Default.WithCommentFilter(filter);
            Assert.That(options, Is.Not.SameAs(MinificationOptions.Default));
            Assert.That(options.CommentFilter, Is.SameAs(filter));
        }

        [Test]
        public void ResetCommentFilterToNull()
        {
            var filter = new Func<Token, string, bool>(delegate { throw new NotImplementedException(); });
            var options = MinificationOptions.Default.WithCommentFilter(filter).WithCommentFilter(null);
            Assert.That(options.CommentFilter, Is.Null);
            Assert.That(options, Is.SameAs(MinificationOptions.Default));
        }

        [Test]
        public void SetCommentFilterToSame()
        {
            var options = MinificationOptions.Default.WithCommentFilter(null);
            Assert.That(options, Is.SameAs(MinificationOptions.Default));
            var filter = new Func<Token, string, bool>(delegate { throw new NotImplementedException(); });

            var options1 = options.WithCommentFilter(filter);
            Assert.That(options1, Is.Not.SameAs(options));

            var options2 = options1.WithCommentFilter(filter);
            Assert.That(options2, Is.SameAs(options1));
        }

        [Test]
        public void CommentMatchingWithNullPattern()
        {
            var e = Assert.Throws<ArgumentNullException>(() =>
                MinificationOptions.Default.FilterCommentMatching(null!));
            Assert.That(e.ParamName, Is.EqualTo("pattern"));

            e = Assert.Throws<ArgumentNullException>(() =>
                MinificationOptions.Default.FilterCommentMatching(null!, RegexOptions.None));
            Assert.That(e.ParamName, Is.EqualTo("pattern"));
        }

        [TestCase(@"^//"     , "foo"      , false)]
        [TestCase(@"^//"     , "/* foo */", false)]
        [TestCase(@"^//"     , "// foo"   , true )]
        [TestCase(@"^///[^/]", "// foo"   , false)]
        [TestCase(@"^///[^/]", "/// foo"  , true )]
        [TestCase(@"^///[^/]", "//// foo" , false)]
        public void CommentMatching(string pattern, string source, bool match)
        {
            var options = MinificationOptions.Default.FilterCommentMatching(pattern);
            var token = Scanner.Scan(source).Single();
            if (options.CommentFilter is not null)
                Assert.That(options.CommentFilter(token, source), Is.EqualTo(match));
            else
                Assert.That(options.CommentFilter, Is.Not.Null);
        }

        [TestCase("foo"       , false)]
        [TestCase("/*  foo */", false)]
        [TestCase("//  foo"   , false)]
        [TestCase("/*! foo */", true )]
        [TestCase("//! foo"   , true )]
        public void ImportantComments(string source, bool match)
        {
            var options = MinificationOptions.Default.FilterImportantComments();
            var token = Scanner.Scan(source).Single();
            Assert.That(options.ShouldFilterComment(token, source), Is.EqualTo(match));
        }

        [Test]
        public void OrCommentFilterOf()
        {
            var options = MinificationOptions.Default.WithCommentFilter(delegate { throw new NotImplementedException(); });
            Assert.That(options, Is.Not.SameAs(MinificationOptions.Default));

            Assert.That(options.OrCommentFilterOf(MinificationOptions.Default),
                        Is.SameAs(options));

            var options2 = MinificationOptions.Default.OrCommentFilterOf(options);
            Assert.That(options2, Is.Not.SameAs(MinificationOptions.Default));
            Assert.That(options2.CommentFilter, Is.SameAs(options.CommentFilter));

            var ar = new[] { true, true , false, false };
            var br = new[] { true, false, true , false };
            var ati = 0;
            var bti = 0;

            var a = MinificationOptions.Default.WithCommentFilter((_, _) => ar[ati++]);
            var b = MinificationOptions.Default.WithCommentFilter((_, _) => br[bti++]);

            var ab = a.OrCommentFilterOf(b);

            foreach (var (af, bf) in ar.Zip(br, ValueTuple.Create))
                Assert.That(ab.ShouldFilterComment(default, string.Empty), Is.EqualTo(af || bf));
        }

        [Test]
        public void SetKeepLeadComment()
        {
            var options = MinificationOptions.Default.WithKeepLeadComment(true);
            Assert.That(options, Is.Not.SameAs(MinificationOptions.Default));
            Assert.That(options.KeepLeadComment, Is.True);
        }

        [Test]
        public void ResetKeepLeadComment()
        {
            var options = MinificationOptions.Default.WithKeepLeadComment(true).WithKeepLeadComment(false);
            Assert.That(options.KeepLeadComment, Is.False);
            Assert.That(options, Is.SameAs(MinificationOptions.Default));
        }

        [Test]
        public void SetKeepLeadCommentToSame()
        {
            var options = MinificationOptions.Default.WithKeepLeadComment(false);
            Assert.That(options, Is.SameAs(MinificationOptions.Default));

            var options1 = options.WithKeepLeadComment(true);
            Assert.That(options1, Is.Not.SameAs(options));

            var options2 = options1.WithKeepLeadComment(true);
            Assert.That(options2, Is.SameAs(options1));
        }
    }
}
