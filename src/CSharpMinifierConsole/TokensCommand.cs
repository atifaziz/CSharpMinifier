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
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using CSharpMinifier;
    using CSharpMinifier.Internals;

    partial class Program
    {
        static void TokensCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var globDir = Ref.Create((DirectoryInfo?)null);
            var format = (string?)null;

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                Options.Glob(globDir),
                { "f|format=", "where {FORMAT} is one of: json (default), csv, line", v => format = v.ToLowerInvariant() },
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
                    string? indent = null;
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
                                Console.WriteLine(",");
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

                    static string Encode(string s)
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
    }
}
