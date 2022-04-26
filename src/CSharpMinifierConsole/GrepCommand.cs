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
using System.Text.RegularExpressions;
using CSharpMinifier;
using CSharpMinifier.Internals;

partial class Program
{
    static void GrepCommand(ProgramArguments args)
    {
        var pattern = args.ArgPattern!;

        foreach (var (path, source) in ReadSources(args.ArgFile, args.OptGlobDirInfo))
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
