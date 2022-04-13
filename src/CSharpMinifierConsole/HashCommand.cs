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
using System.Diagnostics.CodeAnalysis;
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

    static int HashCommand(ProgramArguments args)
    {
        var globDir = args.OptGlob is { } glob ? new DirectoryInfo(glob) : null;
        var comparand
            = args.OptCompare is { } compare
            ? TryParseHexadecimalString(compare, out var hc) ? hc
            : throw new Exception("Hash comparand is not a valid hexadecimal string.")
            : null;
        var algoName
            = args.OptAlgo is var algo && HashAlgorithmNames.TryGetValue(algo, out var name)
            ? name
            : new HashAlgorithmName(algo);
        var format
            = args.OptFormat is { } fs
            ? Enum.TryParse<HashOutputFormat>(fs, true, out var fv)
              && Enum.IsDefined(typeof(HashOutputFormat), fv) ? fv
            : throw new Exception("Invalid hash format.")
            : HashOutputFormat.Hexadecimal;
        var commentFilterPattern = args.OptCommentFilterPattern;
        var keepLeadComment = args.OptKeepLeadComment;
        var keepImportantComment = args.OptKeepImportantComment;

        byte[] hash;
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using (var ha = IncrementalHash.CreateHash(algoName))
        {
            byte[]? buffer = null;
            foreach (var (_, source) in ReadSources(args.ArgFile, globDir))
            {
                foreach (var s in from s in Minifier.Minify(source, commentFilterPattern,
                                                                    keepLeadComment,
                                                                    keepImportantComment)
                                  where s != null
                                  select s)
                {
                    if (args.OptVerbose)
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
            e => e.Name!, // assume above hash algorithm names are always defined
            StringComparer.OrdinalIgnoreCase);

    static bool TryParseHexadecimalString(string s, [NotNullWhen(true)]out byte[]? result)
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
