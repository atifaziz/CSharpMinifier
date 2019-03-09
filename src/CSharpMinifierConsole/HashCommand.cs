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
    using CSharpMinifier;

    partial class Program
    {
        static int HashCommand(IEnumerable<string> args)
        {
            var help = Ref.Create(false);
            var globDir = Ref.Create((DirectoryInfo)null);
            var hashComparand = (byte[])null;

            var options = new OptionSet(CreateStrictOptionSetArgumentParser())
            {
                Options.Help(help),
                Options.Verbose(Verbose),
                Options.Debug,
                Options.Glob(globDir),
                { "c|compare=", "set non-zero exit code if {HASH} is different",
                    v =>
                    {
                        if (!TryParseHexadecimalString(v, out hashComparand))
                            throw new Exception("Hash comparand is not a valid hexadecimal string.");
                    }
                },
            };

            var tail = options.Parse(args);

            if (help)
            {
                Help("hash", options);
                return 0;
            }

            byte[] hash;
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            using (var ha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = null;
                foreach (var (_, source) in ReadSources(tail, globDir))
                {
                    foreach (var s in from s in Minifier.Minify(source, newLine: null)
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

            Console.WriteLine(BitConverter.ToString(hash)
                                          .Replace("-", string.Empty)
                                          .ToLowerInvariant());

            if (hashComparand == null)
                return 0;

            return hashComparand.SequenceEqual(hash) ? 0 : 1;
        }

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
