#region Copyright (c) 2019 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
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
    using System.Collections.Generic;
    using System.IO;
    using MonoOptionSet = Mono.Options.OptionSet;

    static class Extensions
    {
        public static IEnumerable<string> Lines(this string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            return _(); IEnumerable<string> _()
            {
                using var reader = new StringReader(input);
                while (reader.ReadLine() is {} line)
                    yield return line;
            }
        }

        public static IEnumerator<string> ReadLines(this TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            while (reader.ReadLine() is {} line)
                yield return line;
        }

        /// <summary>
        /// Trims version build and revision fields if they are both zero or
        /// just the revision if build is non-zero. An additional parameter
        /// specifies the minimum field count (between 2 and 4) in the
        /// resulting version, which prevents trimming even if zero.
        /// </summary>

        public static Version Trim(this Version version, int minFieldCount = 2)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));
            if (minFieldCount < 2 || minFieldCount > 4) throw new ArgumentOutOfRangeException(nameof(minFieldCount), minFieldCount, null);

            if (version.Revision < 0 || version.Build < 0)
            {
                version = new Version(version.Major,
                                      version.Minor,
                                      version.Build    < 0 ? 0 : version.Build,
                                      version.Revision < 0 ? 0 : version.Revision);
            }

            return minFieldCount < 4 && version.Revision == 0
                 ? minFieldCount < 3 && version.Build == 0 ? new Version(version.Major, version.Minor)
                 : new Version(version.Major, version.Minor, version.Build)
                 : version;
        }
    }

    static class OptionSetExtensions
    {
        public static T WriteOptionDescriptionsReturningWriter<T>(this MonoOptionSet options, T writer)
            where T : TextWriter
        {
            options.WriteOptionDescriptions(writer);
            return writer;
        }
    }
}
