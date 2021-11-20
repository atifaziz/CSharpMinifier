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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CSharpMinifier;
using CSharpMinifier.Internals;

partial class Program
{
    static void GrepCommand(IEnumerable<string> args)
    {
        var help = Ref.Create(false);
        var globDir = Ref.Create((DirectoryInfo?)null);

        var options = new OptionSet(CreateStrictOptionSetArgumentParser())
        {
            Options.Help(help),
            Options.Verbose(Verbose),
            Options.Debug,
            Options.Glob(globDir),
        };

        var tail = new Queue<string>(options.Parse(args));

        if (help)
        {
            Help("grep", options);
            return;
        }

        if (!tail.Any())
            throw new Exception("Missing search regular expression.");

        var pattern = tail.Dequeue();

        foreach (var (path, source) in ReadSources(tail, globDir))
        {
            foreach (var t in from e in Scanner.ParseStrings(source, (t, _, s) => (Token: t, Value: s))
                              where Regex.IsMatch(e.Value, pattern)
                              select e.Token)
            {
                Console.WriteLine($"{path}({t.Start.Line},{t.Start.Column}): {JsonString.Encode(source, t.Start.Offset, t.Length)}");
            }
        }
    }
}
