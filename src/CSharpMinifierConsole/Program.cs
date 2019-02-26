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
    using System.IO;
    using CSharpMinifier;
    using CSharpMinifier.Internals;
    using Mono.Options;

    static partial class Program
    {
        static void Wain(IEnumerable<string> args)
        {
            var help = false;
            var outputTokens = false;
            var tokenFormat = (string)null;

            var options = new OptionSet
            {
                { "?|help|h"        , "prints out the options", _ => help = true },
                { "d|debug"         , "debug break", _ => Debugger.Launch() },
                { "t|tokens"        , "output scanned tokens", _ => outputTokens = true },
                { "tf|token-format=", "tokens format (default = JSON)", v => tokenFormat = v },
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help(options);
                return;
            }

            var source = tail.Count == 0 || tail[0] == "-"
                       ? Console.In.ReadToEnd()
                       : File.ReadAllText(tail[0]);

            var tokens = Scanner.Scan(source);

            if (outputTokens || tokenFormat != null)
            {
                OutputToken(source, tokens, tokenFormat);
            }
            else
            {
                foreach (var s in Minifier.Minify(source, null))
                {
                    if (s == null)
                        Console.WriteLine();
                    else
                        Console.Write(s);
                }
            }
        }

        static void OutputToken(string source, IEnumerable<Token> tokens, string tokenFormat)
        {
            switch (tokenFormat?.ToLowerInvariant())
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

                default:
                    throw new Exception("Unknown token format: " + tokenFormat);
            }
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
                Console.Error.WriteLine(e);
                return 0xbad;
            }
        }
    }
}
