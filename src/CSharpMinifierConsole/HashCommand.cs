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
    using System.Security.Cryptography;
    using System.Text;

    partial class Program
    {
        enum HashOutputFormat
        {
            Hexadecimal,
            Base32,
            Json,
        }

        static int HashCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var globDir = Ref.Create((DirectoryInfo)null);
            var comparand = (byte[])null;
            var algoName = HashAlgorithmName.SHA256;
            var format = HashOutputFormat.Hexadecimal;
            var commentFilterPattern = Ref.Create((string) null);
            var keepLeadComment = Ref.Create(false);
            var keepImportantComment = Ref.Create(false);

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                Options.Glob(globDir),
                Options.CommentFilterPattern(commentFilterPattern),
                Options.KeepLeadComment(keepLeadComment),
                Options.KeepImportantComments(keepImportantComment),
                { "c|compare=", "set non-zero exit code if {HASH} (in hexadecimal) is different",
                    v => comparand = TryParseHexadecimalString(v, out var hc) ? hc
                                   : throw new Exception("Hash comparand is not a valid hexadecimal string.")
                },
                { "a|algo=", $"hash algorithm to use (default = {algoName})",
                    v => algoName = HashAlgorithmNames.TryGetValue(v, out var name)
                                  ? name
                                  : new HashAlgorithmName(v) },
                { "f|format=", "output hash format; where {FORMAT} is one of: " +
                               "hexadecimal (default), base32 (Crockford), json",
                    v => format = Enum.TryParse<HashOutputFormat>(v, true, out var f)
                                  && Enum.IsDefined(typeof(HashOutputFormat), f) ? f
                                : throw new Exception("Invalid hash format.")
                }
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("hash", options);
                return 0;
            }

            byte[] hash;
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            using (var ha = IncrementalHash.CreateHash(algoName))
            {
                byte[] buffer = null;
                foreach (var (_, source) in ReadSources(tail, globDir))
                {
                    foreach (var s in from s in Minifier.Minify(source, commentFilterPattern,
                                                                        keepLeadComment,
                                                                        keepImportantComment)
                                      where s != null
                                      select s)
                    {
                        if (Verbose)
                            Console.Error.Write(s);
                        var desiredBufferLength = utf8.GetByteCount(s);
                        Array.Resize(ref buffer, Math.Max(desiredBufferLength, buffer?.Length ?? 0));
                        var actualBufferLength = utf8.GetBytes(s, 0, s.Length, buffer, 0);
                        ha.AppendData(buffer, 0, actualBufferLength);
                    }
                }

                hash = ha.GetHashAndReset();
            }

            switch (format)
            {
                case HashOutputFormat.Hexadecimal:
                {
                    Console.WriteLine(BitConverter.ToString(hash)
                                                  .Replace("-", string.Empty)
                                                  .ToLowerInvariant());
                    break;
                }
                case HashOutputFormat.Base32:
                {
                    Console.WriteLine(Crockbase32.Encode(hash));
                    break;
                }
                case HashOutputFormat.Json:
                {
                    Console.WriteLine(
                        "[" + string.Join(",",
                                  from b in hash
                                  select b.ToString(CultureInfo.InvariantCulture))
                            + "]");
                    break;
                }
            }

            if (comparand == null)
                return 0;

            return comparand.SequenceEqual(hash) ? 0 : 1;
        }

        static readonly Dictionary<string, HashAlgorithmName> HashAlgorithmNames =
            Enumerable.ToDictionary(
                new[]
                {
                    HashAlgorithmName.MD5,
                    HashAlgorithmName.SHA1,
                    HashAlgorithmName.SHA256,
                    HashAlgorithmName.SHA384,
                    HashAlgorithmName.SHA512,
                },
                e => e.Name,
                StringComparer.OrdinalIgnoreCase);

        static bool TryParseHexadecimalString(string s, out byte[] result)
        {
            result = default;

            if (s.Length % 2 != 0)
                return false;

            var bytes = new byte[s.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(s.AsSpan(i * 2, 2), NumberStyles.HexNumber, null, out var b))
                    return false;
                bytes[i] = b;
            }

            result = bytes;
            return true;
        }
    }
}
