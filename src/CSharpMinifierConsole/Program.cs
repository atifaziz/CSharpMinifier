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
    using System.Linq;
    using System.Runtime.InteropServices;
    using CSharpMinifier;
    using Microsoft.Extensions.FileSystemGlobbing;
    using Mono.Options;
    using OptionSetArgumentParser = System.Func<System.Func<string, Mono.Options.OptionContext, bool>, string, Mono.Options.OptionContext, bool>;

    static partial class Program
    {
        static readonly Ref<bool> Verbose = Ref.Create(false);

        static int Wain(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var globDir = Ref.Create((DirectoryInfo)null);
            var validate = false;

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                Options.Glob(globDir),
                { "validate", "validate minified output", _ => validate = true },
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("min", "[min]", options);
                return 0;
            }

            var command = tail.FirstOrDefault();
            var commandArgs = tail.Skip(1);
            var result = 0;

            switch (command)
            {
                case "min"    : Wain(commandArgs); break;
                case "help"   : HelpCommand(commandArgs); break;
                case "tokens" : TokensCommand(commandArgs); break;
                case "grep"   : GrepCommand(commandArgs); break;
                case "hash"   : result = HashCommand(commandArgs); break;
                case "regions": RegionsCommand(commandArgs); break;
                case "color"  :
                case "colour" : ColorCommand(commandArgs); break;
                case "glob"   : GlobCommand(commandArgs); break;
                default       : DefaultCommand(); break;
            }

            return result;

            void DefaultCommand()
            {
                const string validatorExecutableName = "csval";

                var validator = Lazy.Create(() =>
                       RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                     ? FindProgramPath(validatorExecutableName)
                     : validatorExecutableName);

                foreach (var (_, source) in ReadSources(tail, globDir))
                {
                    Minify(source, Console.Out);

                    if (validate && !Validate(stdin => Minify(source, stdin)))
                        throw new Exception("Minified version is invalid.");
                }

                void Minify(string source, TextWriter output)
                {
                    var nl = false;
                    foreach (var s in Minifier.Minify(source, null))
                    {
                        if (nl = s == null)
                            output.WriteLine();
                        else
                            output.Write(s);
                    }
                    if (!nl)
                        output.WriteLine();
                }

                bool Validate(Action<TextWriter> minificationAction)
                {
                    var psi = new ProcessStartInfo(validator.Value)
                    {
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        RedirectStandardInput  = true,
                    };

                    psi.ArgumentList.Add("--langversion=latest");

                    using (var process = Process.Start(psi))
                    {
                        process.OutputDataReceived += (_, ea) =>
                        {
                            if (ea.Data != null)
                                Console.Error.WriteLine(ea.Data);
                        };

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        var stdin = process.StandardInput;
                        minificationAction(stdin);
                        stdin.Flush();
                        stdin.Close();

                        process.WaitForExit();

                        return process.ExitCode == 0;
                    }
                }
            }
        }

        static string FindProgramPath(string program)
        {
            var fileName = Path.GetFileName(program);

            var paths =
                from p in Environment.GetEnvironmentVariable("PATH")
                                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                select p.Length > 0 && p[0] == '~'
                     ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Personal), p.Substring(1))
                     : p
                into p
                select Path.Join(p, fileName) into p
                where File.Exists(p)
                select p;

            return paths.FirstOrDefault() ?? program;
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
                new ActionOption("glob=", "interpret file path as glob pattern with {DIRECTORY} as base", vs => value.Value = new DirectoryInfo(vs.Last()));
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
                return Wain(args);
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
