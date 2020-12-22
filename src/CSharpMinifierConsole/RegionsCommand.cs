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

namespace CSharpMinifierConsole
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using CSharpMinifier;

    partial class Program
    {
        static void RegionsCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var globDir = Ref.Create((DirectoryInfo?)null);
            var grep = (string?)null;
            var isRegex = false;
            var ignoreCase = false;

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                Options.Glob(globDir),
                { "grep=", "search region message matching {PATTERN}", v => grep = v },
                { "e"    , "use regular expression for search", v => isRegex = true },
                { "i"    , "ignore search case", v => ignoreCase = true },
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("regions", options);
                return;
            }

            var last = (string?)null;

            var regexOptions
                = ignoreCase
                ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                : RegexOptions.None;

            var comparison
                = ignoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            foreach (var (_, source) in ReadSources(tail, globDir))
            {
                var regions =
                    from r in Scanner.ScanRegions(source)
                    where grep == null
                       || (isRegex ? Regex.IsMatch(r.Message, grep, regexOptions)
                                   : r.Message.Contains(grep, comparison))
                    select r;

                foreach (var region in regions)
                {
                    foreach (var token in region.Tokens)
                        Console.Write(last = token.Substring(source));
                }
            }

            if (!string.IsNullOrEmpty(last) && last.Last() is { } ch
                                            && ch != '\r'
                                            && ch != '\n')
            {
                Console.WriteLine();
            }
        }
    }
}
