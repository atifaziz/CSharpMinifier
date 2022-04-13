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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CSharpMinifier;
using Microsoft.Extensions.FileSystemGlobbing;

static partial class Program
{
    static int Wain(ProgramArguments args, out bool verbose)
    {
        verbose = args.OptVerbose;

        var result = 0;

        switch (args)
        {
            case { CmdHelp   : true }: HelpCommand(args); break;
            case { CmdTokens : true }: TokensCommand(args); break;
            case { CmdGrep   : true }: GrepCommand(args); break;
            case { CmdHash   : true }: result = HashCommand(args); break;
            case { CmdRegions: true }: RegionsCommand(args); break;
            case { CmdColor  : true } or { CmdColour: true }:
                                       ColorCommand(args); break;
            case { CmdGlob   : true }: GlobCommand(args); break;
            case { CmdMin    : true }: DefaultCommand(); break;
            default                  : DefaultCommand(); break;
        }

        return result;

        void DefaultCommand()
        {
            var globDir = args.OptGlob is { } glob ? new DirectoryInfo(glob) : null;
            var validate = args.OptValidate;
            var commentFilterPattern = args.OptCommentFilterPattern;
            var keepLeadComment = args.OptKeepLeadComment;
            var keepImportantComment = args.OptKeepImportantComment;

            const string validatorExecutableName = "csval";

            var validator = Lazy.Create(() =>
                   RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                 ? FindProgramPath(validatorExecutableName)
                 : validatorExecutableName);

            foreach (var (_, source) in ReadSources(args.ArgFile, globDir))
            {
                Minify(source, Console.Out);

                if (validate && !Validate(stdin => Minify(source, stdin)))
                    throw new Exception("Minified version is invalid.");
            }

            void Minify(string source, TextWriter output)
            {
                var nl = false;
                foreach (var s in Minifier.Minify(source, commentFilterPattern,
                                                          keepLeadComment,
                                                          keepImportantComment))
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

                using var process = Process.Start(psi)!;

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

    static class Minifier
    {
        public static IEnumerable<string> Minify(string source,
            string? commentFilterPattern = null,
            bool keepLeadComment = false,
            bool keepImportantComment = false)
        {
            var options = MinificationOptions.Default
                                             .WithKeepLeadComment(keepLeadComment);

            if (commentFilterPattern is {} s)
                options = options.FilterCommentMatching(s);

            if (keepImportantComment)
                options = options.OrCommentFilterOf(MinificationOptions.Default.FilterImportantComments());

            return CSharpMinifier.Minifier.Minify(source, newLine: string.Empty, options);
        }
    }

    static string FindProgramPath(string program)
    {
        var fileName = Path.GetFileName(program);

        var envPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        var paths =
            from p in envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            select p.Length > 0 && p[0] == '~'
                 ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Personal), p.Substring(1))
                 : p
            into p
            select Path.Join(p, fileName) into p
            where File.Exists(p)
            select p;

        return paths.FirstOrDefault() ?? program;
    }

    static void HelpCommand(ProgramArguments _)
    {
        Console.Out.WriteLine("This command is now obsolete. Re-run with -h or --help.");
    }

    static IEnumerable<(string File, string Source)>
        ReadSources(IEnumerable<string> files, DirectoryInfo? rootDir = null)
    {
        var stdin = Lazy.Create(() => Console.In.ReadToEnd());
        return ReadSources(files, rootDir, () => stdin.Value, File.ReadAllText);
    }

    static IEnumerable<(string File, T Source)>
        ReadSources<T>(IEnumerable<string> files,
                       DirectoryInfo? rootDir,
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
            using var e = files.GetEnumerator();

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

    static int Main(string[] args)
    {
        var verbose = false;

        try
        {
            return ProgramArguments.CreateParser()
                                   .WithVersion(ThisAssembly.Info.FileVersion)
                                   .Parse(args)
                                   .Match(args => Wain(args, out verbose),
                                          r => ShowHelp(r.Help),
                                          r => Print(Console.Out, 0, r.Version),
                                          r => Print(Console.Error, 1, r.Usage));
        }
        catch (Exception e)
        {
            if (verbose)
                Console.Error.WriteLine(e);
            else
                Console.Error.WriteLine(e.GetBaseException().Message);
            return 0xbad;
        }

        static int ShowHelp(string help)
        {
            var logo = $"{ThisAssembly.Info.Product} (version {new Version(ThisAssembly.Info.FileVersion).Trim(3)})"
                     + Environment.NewLine
                     + ThisAssembly.Info.Copyright.Replace("\u00a9", "(C)");
            return Print(Console.Out, 0, help.Replace("$LOGO$", logo));
        }

        static int Print(TextWriter writer, int exitCode, string message)
        {
            writer.WriteLine(message);
            return exitCode;
        }
    }
}
