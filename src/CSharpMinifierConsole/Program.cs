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
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using CSharpMinifier;
    using CSharpMinifier.Internals;
    using Microsoft.Extensions.FileSystemGlobbing;
    using Mono.Options;
    using OptionSetArgumentParser = System.Func<System.Func<string, Mono.Options.OptionContext, bool>, string, Mono.Options.OptionContext, bool>;

    static partial class Program
    {
        static readonly Ref<bool> Verbose = Ref.Create(false);

        static void Wain(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var globDir = Ref.Create((DirectoryInfo)null);

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                Options.Glob(globDir),
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("min", "[min]", options);
                return;
            }

            var command = tail.FirstOrDefault();
            var commandArgs = tail.Skip(1);

            switch (command)
            {
                case "min"   : Wain(commandArgs); break;
                case "help"  : HelpCommand(commandArgs); break;
                case "tokens": TokensCommand(commandArgs); break;
                case "glob"  : GlobCommand(commandArgs); break;
                default      : DefaultCommand(); break;
            }

            void DefaultCommand()
            {
                foreach (var (_, source) in ReadSources(tail, globDir))
                {
                    var nl = false;
                    foreach (var s in Minifier.Minify(source, null))
                    {
                        if (nl = s == null)
                            Console.WriteLine();
                        else
                            Console.Write(s);
                    }
                    if (!nl)
                        Console.WriteLine();
                }
            }
        }

        static void HelpCommand(IEnumerable<string> args)
        {
            switch (args.FirstOrDefault())
            {
                case null:
                case string command when command == "help":
                    Help("help", new Mono.Options.OptionSet());
                    break;
                case string command:
                    Wain(new [] { command, "--help" });
                        break;
            }
        }

        static void GlobCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var globDir = Ref.Create((DirectoryInfo)null);

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                Options.Glob(globDir)
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("glob", options);
                return;
            }

            foreach (var (p, _) in ReadSources(tail, globDir, () => (string)null, _ => null))
                Console.WriteLine(p);
        }

        static void TokensCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var globDir = Ref.Create((DirectoryInfo)null);
            var format = (string)null;

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                Options.Glob(globDir),
                { "f|format=", "output format: json|csv|line; default = json)", v => format = v.ToLowerInvariant() },
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("tokens", options);
                return;
            }

            var isMultiMode = tail.Count > 1;

            switch (format)
            {
                case null:
                case "json":
                {
                    string indent = null;
                    if (isMultiMode)
                    {
                        Console.WriteLine("[");
                        indent = new string(' ', 4);
                    }

                    var i = 0;
                    foreach (var (path, source) in ReadSources(tail, globDir))
                    {
                        if (isMultiMode)
                        {
                            if (i > 0)
                                Console.WriteLine(",");
                            Console.WriteLine("  {");
                            Console.WriteLine("    \"file\": " + JsonString.Encode(path) + ",");
                            Console.WriteLine("    \"tokens\": [");
                        }
                        else
                        {
                            Console.WriteLine("[");
                        }

                        var tokens = Scanner.Scan(source);
                        var j = 0;

                        foreach (var token in tokens)
                        {
                            if (j > 0)
                                Console.WriteLine($",");
                            Console.WriteLine($"{indent}  {{");
                            Console.WriteLine($"{indent}    \"kind\": \"{token.Kind}\",");
                            Console.WriteLine($"{indent}    \"span\": [[{token.Start.Offset}, {token.Start.Line}, {token.Start.Column}], [{token.End.Offset}, {token.End.Line}, {token.End.Column}]],");
                            Console.WriteLine($"{indent}    \"text\": {JsonString.Encode(source, token.Start.Offset, token.Length)}");
                            Console.Write($"{indent}  }}");
                            j++;
                        }

                        if (j > 0)
                            Console.WriteLine();
                        Console.WriteLine($"{indent}]");

                        if (isMultiMode)
                        {
                            Console.Write("  }");
                            i++;
                        }
                    }

                    if (isMultiMode)
                    {
                        Console.WriteLine();
                        Console.WriteLine("]");
                    }

                    break;
                }
                case "line":
                {
                    foreach (var (path, source) in ReadSources(tail))
                    {
                        var lines =
                            from token in Scanner.Scan(source)
                            select new object[]
                            {
                                token.Kind,
                                $"{token.Start.Offset}/{token.Start.Line}:{token.Start.Column}...{token.End.Offset}/{token.End.Line}:{token.End.Column}",
                                JsonString.Encode(source, token.Start.Offset, token.Length),
                            }
                            into fs
                            select isMultiMode ? fs.Append(JsonString.Encode(path)) : fs
                            into fs
                            select string.Join(" ", fs);

                        foreach (var line in lines)
                            Console.WriteLine(line);
                    }
                    break;
                }
                case "csv":
                {
                    Console.WriteLine(
                        "token,text," +
                        "start_offset,end_offset," +
                        "start_line,start_column,end_line,end_column"
                        + (isMultiMode ? ",file" : null));

                    foreach (var (path, source) in ReadSources(tail))
                    {
                        var rows =
                            from t in Scanner.Scan(source)
                            select new object[]
                            {
                                t.Kind,
                                Encode(t.Substring(source)),
                                t.Start.Offset.ToString(CultureInfo.InvariantCulture),
                                t.End.Offset.ToString(CultureInfo.InvariantCulture),
                                t.Start.Line.ToString(CultureInfo.InvariantCulture),
                                t.Start.Column.ToString(CultureInfo.InvariantCulture),
                                t.End.Line.ToString(CultureInfo.InvariantCulture),
                                t.End.Column.ToString(CultureInfo.InvariantCulture),
                            }
                            into fs
                            select isMultiMode ? fs.Append(Encode(path)) : fs
                            into fs
                            select string.Join(",", fs);

                        foreach (var row in rows)
                            Console.WriteLine(row);
                    }

                    string Encode(string s)
                    {
                        const string quote = "\"";
                        const string quotequote = quote + quote;

                        var json = JsonString.Encode(s);
                        return quote
                             + json.Substring(1, json.Length - 2)
                                   .Replace(quote, quotequote)
                             + quote;
                    }

                    break;
                }
            }
        }

        static IEnumerable<(string File, string Source)>
            ReadSources(IEnumerable<string> files, DirectoryInfo rootDir = null)
        {
            var stdin = Lazy.Create(() => Console.In.ReadToEnd());
            return ReadSources(files, rootDir, () => stdin.Value, File.ReadAllText);
        }

        static IEnumerable<(string File, T Source)>
            ReadSources<T>(IEnumerable<string> files,
                           DirectoryInfo rootDir,
                           Func<T> stdin, Func<string, T> reader)
        {
            if (rootDir != null)
            {
                var matcher = new Matcher();
                using (var e = files.GetEnumerator())
                {
                    if (!e.MoveNext())
                        yield return ("STDIN", stdin());

                    do
                    {
                        if (string.IsNullOrEmpty(e.Current))
                            continue;

                        if (e.Current[0] == '!')
                            matcher.AddExclude(e.Current.Substring(1));
                        else
                            matcher.AddInclude(e.Current);
                    }
                    while (e.MoveNext());
                }

                foreach (var r in matcher.GetResultsInFullPath(rootDir.FullName))
                    yield return (Path.GetRelativePath(rootDir.FullName, r), reader(r));
            }
            else
            {
                using (var e = files.GetEnumerator())
                {
                    if (!e.MoveNext())
                        yield return ("STDIN", stdin());

                    do
                    {
                        if (string.IsNullOrEmpty(e.Current))
                            continue;
                        yield return (e.Current, reader(e.Current));
                    }
                    while (e.MoveNext());
                }
            }
        }

        static class Options
        {
            public static Option Help(Ref<bool> value) =>
                new ActionOption("?|help|h", "prints out the options", _ => value.Value = true);

            public static Option Verbose(Ref<bool> value) =>
                new ActionOption("verbose|v", "enable additional output", _ => value.Value = true);

            public static readonly Option Debug =
                new ActionOption("d|debug", "debug break", vs => Debugger.Launch());

            public static Option Glob(Ref<DirectoryInfo> value) =>
                new ActionOption("glob=", "glob base directory", vs => value.Value = new DirectoryInfo(vs.Last()));
        }

        static OptionSetArgumentParser CreateStrictOptionSetArgumentParser()
        {
            var hasTailStarted = false;
            return (impl, arg, context) =>
            {
                if (hasTailStarted) // once a tail, always a tail
                    return false;

                var isOption = impl(arg, context);
                if (!isOption && !hasTailStarted)
                {
                    if (arg.Length > 1 && arg[0] == '-')
                        throw new Exception("Invalid argument: " + arg);

                    hasTailStarted = true;
                }

                return isOption;
            };
        }

        static int Main(string[] args)
        {
            try
            {
                Wain(args);
                return 0;
            }
            catch (Exception e)
            {
                if (Verbose)
                    Console.Error.WriteLine(e);
                else
                    Console.Error.WriteLine(e.GetBaseException().Message);
                return 0xbad;
            }
        }
    }
}
