namespace CSharpMinifierConsole
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using MonoOptionSet = Mono.Options.OptionSet;

    partial class Program
    {
        static readonly Lazy<FileVersionInfo> CachedVersionInfo = Lazy.Create(() => FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location));
        static FileVersionInfo VersionInfo => CachedVersionInfo.Value;

        static void Help(string command, MonoOptionSet options) =>
            Help(command, command, options);

        static void Help(string id, string command, MonoOptionSet options)
        {
            var opts = Lazy.Create(() => options.WriteOptionDescriptionsReturningWriter(new StringWriter { NewLine = Environment.NewLine }).ToString());
            var logo = Lazy.Create(() => new StringBuilder().AppendLine($"{VersionInfo.ProductName} (version {new Version(VersionInfo.FileVersion).Trim(3)})")
                                                            .AppendLine(VersionInfo.LegalCopyright.Replace("\u00a9", "(C)"))
                                                            .ToString());

            using var stream = GetManifestResourceStream($"help.{id ?? command}.txt");
            using var reader = new StreamReader(stream);
            using var e = reader.ReadLines();
            while (e.MoveNext())
            {
                var line = Regex.Replace(e.Current, @"\$([A-Z][A-Z_]*)\$",
                                         m => m.Groups[1].Value switch
                                         {
                                             "NAME"    => "csmin",
                                             "COMMAND" => command,
                                             "LOGO"    => logo.Value,
                                             "OPTIONS" => opts.Value,
                                             _         => string.Empty
                                         });

                if (line.Length > 0 && line[line.Length - 1] == '\n')
                    Console.Write(line);
                else
                    Console.WriteLine(line);
            }
        }

        static string LoadTextResource(string name, Encoding encoding = null) =>
            LoadTextResource(typeof(Program), name, encoding);

        static string LoadTextResource(Type type, string name, Encoding encoding = null)
        {
            using var stream = type != null
                             ? GetManifestResourceStream(type, name)
                             : GetManifestResourceStream(null, name);
            Debug.Assert(stream != null);
            using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        static Stream GetManifestResourceStream(string name) =>
            GetManifestResourceStream(typeof(Program), name);

        static Stream GetManifestResourceStream(Type type, string name) =>
            type != null ? type.Assembly.GetManifestResourceStream(type, name)
                         : Assembly.GetCallingAssembly().GetManifestResourceStream(name);
    }
}
