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
    using CSharpMinifier;
    using CSharpMinifier.Internals;

    partial class Program
    {
        static void ServeCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("serve", options);
                return;
            }

            string line;
            while ((line = Console.In.ReadLine()) != null)
            {
                line = line.Trim();

                if (line.Length == 0)
                    continue;

                if (JsonStringParser.TryParse(line, out _, out var str) == JsonStringParser.ParseResult.Success)
                {
                    Console.WriteLine(JsonString.Encode(string.Join(null, Minifier.Minify(str, "\n"))));
                }
                else
                {
                    Console.Error.WriteLine("Invalid line.");
                }
            }
        }
    }
}
