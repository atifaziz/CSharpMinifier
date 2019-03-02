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
    using Mono.Options;
    using OptionSetArgumentParser = System.Func<System.Func<string, Mono.Options.OptionContext, bool>, string, Mono.Options.OptionContext, bool>;

    static partial class Program
    {
        static readonly Ref<bool> Verbose = Ref.Create(false);

        static void Wain(IEnumerable<string> args)
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
                Help("min", "[min]", options);
                return;
            }

            var command = tail.FirstOrDefault();
            var commandArgs = tail.Skip(1);

            switch (command)
            {
                case "tokens": TokensCommand(commandArgs); break;
                case "min"   : DefaultCommand(commandArgs); break;
                default      : DefaultCommand(tail); break;
            }
        }

        static void DefaultCommand(IEnumerable<string> tail)
        {
            var source = ReadSources(tail).Take(2).ToArray().First();

            foreach (var s in Minifier.Minify(source, null))
            {
                if (s == null)
                    Console.WriteLine();
                else
                    Console.Write(s);
            }
        }

        static void TokensCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var format = (string)null;

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                { "f|format=", "output format: json|csv|line; default = json)", v => format = v },
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("tokens", options);
                return;
            }

            var source = ReadSources(tail).Take(2).ToArray().First();
            var tokens = Scanner.Scan(source);
            switch (format?.ToLowerInvariant())
            {
                case null:
                case "json":
                {
                    var i = 0;

                    Console.WriteLine("[");

                    foreach (var token in tokens)
                    {
                        if (i > 0)
                            Console.WriteLine(",  ");
                        Console.Write($@"  {{
    ""kind"": ""{token.Kind}"",
    ""span"": [[{token.Start.Offset}, {token.Start.Line}, {token.Start.Column}], [{token.End.Offset}, {token.End.Line}, {token.End.Column}]],
    ""text"": {JsonString.Encode(source, token.Start.Offset, token.Length)}
  }}");
                        i++;
                    }

                    if (i > 0)
                        Console.WriteLine();
                    Console.WriteLine("]");
                    break;
                }

                case "line":
                {
                    foreach (var token in tokens)
                        Console.WriteLine($@"{token.Kind} {token.Start.Offset}/{token.Start.Line}:{token.Start.Column}...{token.End.Offset}/{token.End.Line}:{token.End.Column} {JsonString.Encode(source, token.Start.Offset, token.Length)}");
                    break;
                }

                case "csv":
                {
                    const string quote = "\"";
                    const string quotequote = quote + quote;

                    Console.WriteLine("token,start_offset,end_offset,start_line,start_column,end_line,end_column,Text");

                    var rows =
                        from t in tokens
                        let text = JsonString.Encode(t.Substring(source))
                        select
                            string.Join(",",
                                t.Kind,
                                t.Start.Offset.ToString(CultureInfo.InvariantCulture),
                                t.End.Offset.ToString(CultureInfo.InvariantCulture),
                                t.Start.Line.ToString(CultureInfo.InvariantCulture),
                                t.Start.Column.ToString(CultureInfo.InvariantCulture),
                                t.End.Line.ToString(CultureInfo.InvariantCulture),
                                t.End.Column.ToString(CultureInfo.InvariantCulture),
                                quote + text.Substring(1, text.Length - 2).Replace(quote, quotequote) + quote);
                    foreach (var row in rows)
                        Console.WriteLine(row);

                    break;
                }

                default:
                    throw new Exception("Unknown token format: " + format);
            }
        }

        static IEnumerable<string> ReadSources(IEnumerable<string> files)
        {
            using (var e = files.GetEnumerator())
            {
                if (!e.MoveNext() || e.Current == "-")
                    yield return Console.In.ReadToEnd();
                else
                    yield return File.ReadAllText(e.Current);

                if (e.MoveNext())
                    throw new NotImplementedException("Processing of more than one source is currently not implemented.");
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
