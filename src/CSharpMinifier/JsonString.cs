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

namespace CSharpMinifier.Internals
{
    using System;
    using System.Text;

    /// <summary>
    /// This type supports the infrastructure and is not intended to be used
    /// directly from your code.
    /// </summary>

    public static class JsonString
    {
        [ThreadStatic] static StringBuilder _threadLocalStringBuilder;

        public static string Encode(string s) =>
            Encode(s, ref _threadLocalStringBuilder);

        public static string Encode(string s, int index, int length) =>
            Encode(s, index, length, ref _threadLocalStringBuilder);

        public static string Encode(string s, ref StringBuilder sb) =>
            Encode(s, 0, s.Length, ref sb);

        public static string Encode(string s, int index, int length, ref StringBuilder sb)
        {
            if (sb == null)
                sb = new StringBuilder();
            else
                sb.Length = 0;

            sb = sb.Append('\"');

            var endIndex = index + length;
            for (var i = index; i < endIndex; i++)
            {
                var ch = s[i];
                switch (ch)
                {
                    case '"':
                    case '\'':
                    case '\\': sb.Append('\\').Append(ch); break;
                    default:
                        if (ch < ControlChars.Length)
                            sb.Append(ControlChars[ch]);
                        else
                            sb.Append(ch);
                        break;
                }
            }

            return sb.Append('\"').ToString();
        }

        static readonly string[] ControlChars =
        {
            @"\u0000", // NUL
            @"\u0001", // SOH
            @"\u0002", // STX
            @"\u0003", // ETX
            @"\u0004", // EOT
            @"\u0005", // ENQ
            @"\u0006", // ACK
            @"\u0007", // BEL
            @"\b"    , // BS
            @"\t"    , // HT
            @"\n"    , // LF
            @"\u000b", // VT
            @"\f"    , // FF
            @"\r"    , // CR
            @"\u000e", // SO
            @"\u000f", // SI
            @"\u0010", // DLE
            @"\u0011", // DC1
            @"\u0012", // DC2
            @"\u0013", // DC3
            @"\u0014", // DC4
            @"\u0015", // NAK
            @"\u0016", // SYN
            @"\u0017", // ETB
            @"\u0018", // CAN
            @"\u0019", // EM
            @"\u001a", // SUB
            @"\u001b", // ESC
            @"\u001c", // FS
            @"\u001d", // GS
            @"\u001e", // RS
            @"\u001f", // US
        };
    }
}
