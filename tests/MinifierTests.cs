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
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using MoreLinq;
    using NUnit.Framework;

    [TestFixture]
    public class MinifierTests
    {
        [Test]
        public void MinifyNullSource()
        {
            var e = Assert.Throws<ArgumentNullException>(() => Minifier.Minify(null!));
            Debug.Assert(e is not null);
            Assert.That(e.ParamName, Is.EqualTo("source"));
        }

        [Test]
        public void Minify()
        {
            const string source = @"
#line 1
# line 1
/* https://unlicense.org/
 *
 * This is free and unencumbered software released into the public domain.
 *
 * Anyone is free to copy, modify, publish, use, compile, sell, or
 * distribute this software, either in source code form or as a compiled
 * binary, for any purpose, commercial or non-commercial, and by any
 * means.
 *
 * In jurisdictions that recognize copyright laws, the author or authors
 * of this software dedicate any and all copyright interest in the
 * software to the public domain. We make this dedication for the benefit
 * of the public at large and to the detriment of our heirs and
 * successors. We intend this dedication to be an overt act of
 * relinquishment in perpetuity of all present and future rights to this
 * software under copyright law.
 *
 * THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * For more information, please refer to <http://unlicense.org>
 */

#region!
#! // not really valid
#endregion!

#region Imports
using System;
#endregion

static class Program
{
    static void Main()
    {
        const string alphabetText =
#if UPPER
            ""THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.""
#else // !UPPER
            ""The quick brown fox jumps over the lazy dog.""
#endif
            ;

        Console.WriteLine(@""
            Lorem ipsum dolor sit amet, consectetur adipiscing elit.
            Quisque ut sem massa.
            In at fringilla ipsum.
            Phasellus ut urna pretium felis porttitor euismod mattis sed augue."");
    }
}";

            var minified = Minifier.Minify(NormalizeLineEndings(source), "\n")
                                   .ToDelimitedString(string.Empty);

            const string expected =
                "#line 1\n" +
                "# line 1\n" +
                "#!\n" +
                "using System;" +
                "static class Program{" +
                "static void Main(){" +
                "const string alphabetText=\n" +
                "#if UPPER\n" +
                "\"THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.\"\n" +
                "#else\n" +
                "\"The quick brown fox jumps over the lazy dog.\"\n" +
                "#endif\n" +
                ";" +
                "Console.WriteLine(" + "@\"\n" +
                "            Lorem ipsum dolor sit amet, consectetur adipiscing elit.\n" +
                "            Quisque ut sem massa.\n" +
                "            In at fringilla ipsum.\n" +
                "            Phasellus ut urna pretium felis porttitor euismod mattis sed augue.\"" +
                ");" +
                "}" +
                "}";

            Assert.That(minified, Is.EqualTo(expected));
        }

        [Test]
        public void MinifyWithCommentFilter()
        {
            const string source = @"
/* https://unlicense.org/
 *
 * This is free and unencumbered software released into the public domain.
 *
 * Anyone is free to copy, modify, publish, use, compile, sell, or
 * distribute this software, either in source code form or as a compiled
 * binary, for any purpose, commercial or non-commercial, and by any
 * means.
 *
 * In jurisdictions that recognize copyright laws, the author or authors
 * of this software dedicate any and all copyright interest in the
 * software to the public domain. We make this dedication for the benefit
 * of the public at large and to the detriment of our heirs and
 * successors. We intend this dedication to be an overt act of
 * relinquishment in perpetuity of all present and future rights to this
 * software under copyright law.
 *
 * THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * For more information, please refer to <http://unlicense.org>
 */

#region Imports
using System;
#endregion

    //! This is a VERY IMPORTANT comment!

static class Program
{
    /*! internal */ static void Main()
    {
        const string alphabetText =
#if UPPER
            ""THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.""
#else // !UPPER
            ""The quick brown fox jumps over the lazy dog.""
#endif
            ;

        Console.WriteLine(@""
            Lorem ipsum dolor sit amet, consectetur adipiscing elit.
            Quisque ut sem massa.
            In at fringilla ipsum.
            Phasellus ut urna pretium felis porttitor euismod mattis sed augue."");
    }
}";

            var options = MinificationOptions.Default.FilterCommentMatching(@"^(//!|/\*!)");
            var minified = Minifier.Minify(NormalizeLineEndings(source), "\n", options)
                                   .ToDelimitedString(string.Empty);

            const string expected =
                "using System;" +
                "//! This is a VERY IMPORTANT comment!\n" +
                "static class Program{" +
                "/*! internal */static void Main(){" +
                "const string alphabetText=\n" +
                "#if UPPER\n" +
                "\"THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.\"\n" +
                "#else\n" +
                "\"The quick brown fox jumps over the lazy dog.\"\n" +
                "#endif\n" +
                ";" +
                "Console.WriteLine(" + "@\"\n" +
                "            Lorem ipsum dolor sit amet, consectetur adipiscing elit.\n" +
                "            Quisque ut sem massa.\n" +
                "            In at fringilla ipsum.\n" +
                "            Phasellus ut urna pretium felis porttitor euismod mattis sed augue.\"" +
                ");" +
                "}" +
                "}";

            Assert.That(minified, Is.EqualTo(expected));
        }

        [Test]
        public void MinifyWhileKeepingLeadMultiLineComment()
        {
            const string source = @"
/* https://unlicense.org/
 *
 * This is free and unencumbered software released into the public domain.
 *
 * Anyone is free to copy, modify, publish, use, compile, sell, or
 * distribute this software, either in source code form or as a compiled
 * binary, for any purpose, commercial or non-commercial, and by any
 * means.
 *
 * In jurisdictions that recognize copyright laws, the author or authors
 * of this software dedicate any and all copyright interest in the
 * software to the public domain. We make this dedication for the benefit
 * of the public at large and to the detriment of our heirs and
 * successors. We intend this dedication to be an overt act of
 * relinquishment in perpetuity of all present and future rights to this
 * software under copyright law.
 *
 * THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * For more information, please refer to <http://unlicense.org>
 */

#region Imports
using System;
#endregion

static class Program
{
    static void Main()
    {
        const string alphabetText =
#if UPPER
            ""THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.""
#else // !UPPER
            ""The quick brown fox jumps over the lazy dog.""
#endif
            ;

        Console.WriteLine(@""
            Lorem ipsum dolor sit amet, consectetur adipiscing elit.
            Quisque ut sem massa.
            In at fringilla ipsum.
            Phasellus ut urna pretium felis porttitor euismod mattis sed augue."");
    }
}";

            var options = MinificationOptions.Default.WithKeepLeadComment(true);
            var minified = Minifier.Minify(NormalizeLineEndings(source), "\n", options)
                                   .ToDelimitedString(string.Empty);

            const string expected =
                "/* https://unlicense.org/\n" +
                " *\n" +
                " * This is free and unencumbered software released into the public domain.\n" +
                " *\n" +
                " * Anyone is free to copy, modify, publish, use, compile, sell, or\n" +
                " * distribute this software, either in source code form or as a compiled\n" +
                " * binary, for any purpose, commercial or non-commercial, and by any\n" +
                " * means.\n" +
                " *\n" +
                " * In jurisdictions that recognize copyright laws, the author or authors\n" +
                " * of this software dedicate any and all copyright interest in the\n" +
                " * software to the public domain. We make this dedication for the benefit\n" +
                " * of the public at large and to the detriment of our heirs and\n" +
                " * successors. We intend this dedication to be an overt act of\n" +
                " * relinquishment in perpetuity of all present and future rights to this\n" +
                " * software under copyright law.\n" +
                " *\n" +
                " * THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND,\n" +
                " * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF\n" +
                " * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.\n" +
                " * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR\n" +
                " * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,\n" +
                " * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR\n" +
                " * OTHER DEALINGS IN THE SOFTWARE.\n" +
                " *\n" +
                " * For more information, please refer to <http://unlicense.org>\n" +
                " */" +
                "using System;" +
                "static class Program{" +
                "static void Main(){" +
                "const string alphabetText=\n" +
                "#if UPPER\n" +
                "\"THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.\"\n" +
                "#else\n" +
                "\"The quick brown fox jumps over the lazy dog.\"\n" +
                "#endif\n" +
                ";" +
                "Console.WriteLine(" + "@\"\n" +
                "            Lorem ipsum dolor sit amet, consectetur adipiscing elit.\n" +
                "            Quisque ut sem massa.\n" +
                "            In at fringilla ipsum.\n" +
                "            Phasellus ut urna pretium felis porttitor euismod mattis sed augue.\"" +
                ");" +
                "}" +
                "}";

            Assert.That(minified, Is.EqualTo(expected));
        }

        [Test]
        public void MinifyWhileKeepingLeadSingleLineComments()
        {
            const string source = @"
// https://unlicense.org/
//
// This is free and unencumbered software released into the public domain.
//
// Anyone is free to copy, modify, publish, use, compile, sell, or
// distribute this software, either in source code form or as a compiled
// binary, for any purpose, commercial or non-commercial, and by any
// means.
//
// In jurisdictions that recognize copyright laws, the author or authors
// of this software dedicate any and all copyright interest in the
// software to the public domain. We make this dedication for the benefit
// of the public at large and to the detriment of our heirs and
// successors. We intend this dedication to be an overt act of
// relinquishment in perpetuity of all present and future rights to this
// software under copyright law.
//
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
// For more information, please refer to <http://unlicense.org>

// This comment with a blank line before should not appear as part of
// the lead comment!

#region Imports
using System;
#endregion

static class Program
{
    static void Main()
    {
        const string alphabetText =
#if UPPER
            ""THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.""
#else // !UPPER
            ""The quick brown fox jumps over the lazy dog.""
#endif
            ;

        Console.WriteLine(@""
            Lorem ipsum dolor sit amet, consectetur adipiscing elit.
            Quisque ut sem massa.
            In at fringilla ipsum.
            Phasellus ut urna pretium felis porttitor euismod mattis sed augue."");
    }
}";

            var options = MinificationOptions.Default.WithKeepLeadComment(true);
            var minified = Minifier.Minify(NormalizeLineEndings(source), "\n", options)
                                   .ToDelimitedString(string.Empty);

            const string expected =
                "// https://unlicense.org/\n" +
                "//\n" +
                "// This is free and unencumbered software released into the public domain.\n" +
                "//\n" +
                "// Anyone is free to copy, modify, publish, use, compile, sell, or\n" +
                "// distribute this software, either in source code form or as a compiled\n" +
                "// binary, for any purpose, commercial or non-commercial, and by any\n" +
                "// means.\n" +
                "//\n" +
                "// In jurisdictions that recognize copyright laws, the author or authors\n" +
                "// of this software dedicate any and all copyright interest in the\n" +
                "// software to the public domain. We make this dedication for the benefit\n" +
                "// of the public at large and to the detriment of our heirs and\n" +
                "// successors. We intend this dedication to be an overt act of\n" +
                "// relinquishment in perpetuity of all present and future rights to this\n" +
                "// software under copyright law.\n" +
                "//\n" +
                "// THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND,\n" +
                "// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF\n" +
                "// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.\n" +
                "// IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR\n" +
                "// OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,\n" +
                "// ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR\n" +
                "// OTHER DEALINGS IN THE SOFTWARE.\n" +
                "//\n" +
                "// For more information, please refer to <http://unlicense.org>\n" +
                "using System;" +
                "static class Program{" +
                "static void Main(){" +
                "const string alphabetText=\n" +
                "#if UPPER\n" +
                "\"THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.\"\n" +
                "#else\n" +
                "\"The quick brown fox jumps over the lazy dog.\"\n" +
                "#endif\n" +
                ";" +
                "Console.WriteLine(" + "@\"\n" +
                "            Lorem ipsum dolor sit amet, consectetur adipiscing elit.\n" +
                "            Quisque ut sem massa.\n" +
                "            In at fringilla ipsum.\n" +
                "            Phasellus ut urna pretium felis porttitor euismod mattis sed augue.\"" +
                ");" +
                "}" +
                "}";

            Assert.That(minified, Is.EqualTo(expected));
        }

        static string NormalizeLineEndings(string s) =>
            Regex.Replace(s, @"\r?\n", "\n").Replace('\r', '\n');
    }
}
