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

namespace CSharpMinifier
{
    using System.Diagnostics.CodeAnalysis;

    static class SubstringPool
    {
        const string Str =
            "\0" +
            "\u0001\u0002\u0003\u0004\u0005\u0006" +
            "\a\b\t\n\v\f\r" +
            "\u000e\u000f\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f" +
            " !\"#$%&'()*+,-./" +
            "0123456789" +
            ":;<=>?@" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "[\\]^_`" +
            "abcdefghijklmnopqrstuvwxyz" +
            "{|}~";

        static readonly string[] Singletons;
        const string CrLf = "\r\n";
        static readonly string[] Spaces = new string[256];
        static readonly string[] Tabs = new string[32];

        static class Keywords
        {
            public const string As = "as";
            public const string By = "by";
            public const string Do = "do";
            public const string If = "if";
            public const string In = "in";
            public const string Is = "is";
            public const string On = "on";

            public const string Add = "add";
            public const string For = "for";
            public const string Get = "get";
            public const string Int = "int";
            public const string Let = "let";
            public const string New = "new";
            public const string Out = "out";
            public const string Ref = "ref";
            public const string Set = "set";
            public const string Try = "try";
            public const string Var = "var";

            public const string Base = "base";
            public const string Bool = "bool";
            public const string Byte = "byte";
            public const string Case = "case";
            public const string Char = "char";
            public const string Else = "else";
            public const string Enum = "enum";
            public const string From = "from";
            public const string Goto = "goto";
            public const string Into = "into";
            public const string Join = "join";
            public const string Lock = "lock";
            public const string Long = "long";
            public const string Null = "null";
            public const string This = "this";
            public const string True = "true";
            public const string Uint = "uint";
            public const string Void = "void";
            public const string When = "when";

            public const string Alias = "alias";
            public const string Async = "async";
            public const string Await = "await";
            public const string Break = "break";
            public const string Catch = "catch";
            public const string Class = "class";
            public const string Const = "const";
            public const string Event = "event";
            public const string False = "false";
            public const string Fixed = "fixed";
            public const string Float = "float";
            public const string Group = "group";
            public const string Sbyte = "sbyte";
            public const string Short = "short";
            public const string Throw = "throw";
            public const string Ulong = "ulong";
            public const string Using = "using";
            public const string Value = "value";
            public const string Where = "where";
            public const string While = "while";
            public const string Yield = "yield";

            public const string Double = "double";
            public new const string Equals = "equals";
            public const string Extern = "extern";
            public const string Global = "global";
            public const string NameOf = "nameof";
            public const string Object = "object";
            public const string Params = "params";
            public const string Public = "public";
            public const string Remove = "remove";
            public const string Return = "return";
            public const string Sealed = "sealed";
            public const string Select = "select";
            public const string SizeOf = "sizeof";
            public const string Static = "static";
            public const string String = "string";
            public const string Struct = "struct";
            public const string Switch = "switch";
            public const string TypeOf = "typeof";
            public const string Unsafe = "unsafe";
            public const string Ushort = "ushort";

            public const string Checked = "checked";
            public const string Decimal = "decimal";
            public const string Default = "default";
            public const string Dynamic = "dynamic";
            public const string Finally = "finally";
            public const string ForEach = "foreach";
            public const string OrderBy = "orderby";
            public const string Partial = "partial";
            public const string Private = "private";
            public const string Virtual = "virtual";

            public const string Abstract = "abstract";
            public const string Continue = "continue";
            public const string Delegate = "delegate";
            public const string Explicit = "explicit";
            public const string Implicit = "implicit";
            public const string Internal = "internal";
            public const string Operator = "operator";
            public const string Override = "override";
            public const string Readonly = "readonly";
            public const string Volatile = "volatile";

            public const string Ascending = "ascending";
            public const string Interface = "interface";
            public const string Namespace = "namespace";
            public const string Protected = "protected";
            public const string Unchecked = "unchecked";

            public const string Descending = "descending";
            public const string Stackalloc = "stackalloc";
        }

        static SubstringPool()
        {
            Singletons = new string[Str.Length];
            foreach (var ch in Str)
                Singletons[ch] = ch.ToString();
        }

        public static string GetOrCreate(string buffer, int offset, int length)
        {
            char ch;
            switch (length)
            {
                case 0: return string.Empty;
                case 1 when (ch = buffer[offset]) < Singletons.Length:
                    return Singletons[ch];
                case 2 when buffer[offset] == '\r' && buffer[offset + 1] == '\n':
                    return CrLf;
                case 2:
                {
                    var ch1 = buffer[offset];
                    var ch2 = buffer[offset + 1];
                    switch (ch1)
                    {
                        case 'a': if (ch2 == 's') return Keywords.As; break;
                        case 'b': if (ch2 == 'y') return Keywords.By; break;
                        case 'd': if (ch2 == 'o') return Keywords.Do; break;
                        case 'i':
                            switch (ch2)
                            {
                                case 'f': return Keywords.If;
                                case 'n': return Keywords.In;
                                case 's': return Keywords.Is;
                            }
                            break;
                        case 'o': if (ch2 == 'n') return Keywords.On; break;
                    }
                    break;
                }
                case 3:
                {
                    var ch1 = buffer[offset];
                    var ch2 = buffer[offset + 1];
                    var ch3 = buffer[offset + 2];
                    switch (ch1)
                    {
                        case 'a': if (ch2 == 'd' && ch3 == 'd') return Keywords.Add; break;
                        case 'f': if (ch2 == 'o' && ch3 == 'r') return Keywords.For; break;
                        case 'g': if (ch2 == 'e' && ch3 == 't') return Keywords.Get; break;
                        case 'i': if (ch2 == 'n' && ch3 == 't') return Keywords.Int; break;
                        case 'l': if (ch2 == 'e' && ch3 == 't') return Keywords.Let; break;
                        case 'n': if (ch2 == 'e' && ch3 == 'w') return Keywords.New; break;
                        case 'o': if (ch2 == 'u' && ch3 == 't') return Keywords.Out; break;
                        case 'r': if (ch2 == 'e' && ch3 == 'f') return Keywords.Ref; break;
                        case 's': if (ch2 == 'e' && ch3 == 't') return Keywords.Set; break;
                        case 't': if (ch2 == 'r' && ch3 == 'y') return Keywords.Try; break;
                        case 'v': if (ch2 == 'a' && ch3 == 'r') return Keywords.Var; break;
                    }
                    break;
                }
                case 4:
                {
                    var ch1 = buffer[offset];
                    var ch2 = buffer[offset + 1];
                    var ch3 = buffer[offset + 2];
                    var ch4 = buffer[offset + 3];
                    switch (ch1)
                    {
                        case 'b':
                            switch (ch2)
                            {
                                case 'a': if (ch3 == 's' && ch4 == 'e') return Keywords.Base; break;
                                case 'o': if (ch3 == 'o' && ch4 == 'l') return Keywords.Bool; break;
                                case 'y': if (ch3 == 't' && ch4 == 'e') return Keywords.Byte; break;
                            }
                            break;
                        case 'c':
                            switch (ch2)
                            {
                                case 'a': if (ch3 == 's' && ch4 == 'e') return Keywords.Case; break;
                                case 'h': if (ch3 == 'a' && ch4 == 'r') return Keywords.Char; break;
                            }
                            break;
                        case 'e':
                            switch (ch2)
                            {
                                case 'l': if (ch3 == 's' && ch4 == 'e') return Keywords.Else; break;
                                case 'n': if (ch3 == 'u' && ch4 == 'm') return Keywords.Enum; break;
                            }
                            break;
                        case 'f': if (ch2 == 'r' && ch3 == 'o' && ch4 == 'm') return Keywords.From; break;
                        case 'g': if (ch2 == 'o' && ch3 == 't' && ch4 == 'o') return Keywords.Goto; break;
                        case 'i': if (ch2 == 'n' && ch3 == 't' && ch4 == 'o') return Keywords.Into; break;
                        case 'j': if (ch2 == 'o' && ch3 == 'i' && ch4 == 'n') return Keywords.Join; break;
                        case 'l':
                            if (ch2 == 'o')
                            {
                                switch (ch3)
                                {
                                    case 'c': if (ch4 == 'k') return Keywords.Lock; break;
                                    case 'n': if (ch4 == 'g') return Keywords.Long; break;
                                }
                            }
                            break;
                        case 'n': if (ch2 == 'u' && ch3 == 'l' && ch4 == 'l') return Keywords.Null; break;
                        case 't':
                            switch (ch2)
                            {
                                case 'r': if (ch3 == 'u' && ch4 == 'e') return Keywords.True; break;
                                case 'h': if (ch3 == 'i' && ch4 == 's') return Keywords.This; break;
                            }
                            break;
                        case 'u': if (ch2 == 'i' && ch3 == 'n' && ch4 == 't') return Keywords.Uint; break;
                        case 'v': if (ch2 == 'o' && ch3 == 'i' && ch4 == 'd') return Keywords.Void; break;
                        case 'w': if (ch2 == 'h' && ch3 == 'e' && ch4 == 'n') return Keywords.When; break;
                    }
                    break;
                }
                case 5:
                {
                    var ch1 = buffer[offset];
                    var ch2 = buffer[offset + 1];
                    var ch3 = buffer[offset + 2];
                    var ch4 = buffer[offset + 3];
                    var ch5 = buffer[offset + 4];
                    switch (ch1)
                    {
                        case 'a':
                            switch (ch2)
                            {
                                case 'l': if (ch3 == 'i' && ch4 == 'a' && ch5 == 's') return Keywords.Alias; break;
                                case 's': if (ch3 == 'y' && ch4 == 'n' && ch5 == 'c') return Keywords.Async; break;
                                case 'w': if (ch3 == 'a' && ch4 == 'i' && ch5 == 't') return Keywords.Await; break;
                            }
                            break;
                        case 'b': if (ch2 == 'r' && ch3 == 'e' && ch4 == 'a' && ch5 == 'k') return Keywords.Break; break;
                        case 'c':
                            switch (ch2)
                            {
                                case 'a': if (ch3 == 't' && ch4 == 'c' && ch5 == 'h') return Keywords.Catch; break;
                                case 'l': if (ch3 == 'a' && ch4 == 's' && ch5 == 's') return Keywords.Class; break;
                                case 'o': if (ch3 == 'n' && ch4 == 's' && ch5 == 't') return Keywords.Const; break;
                            }
                            break;
                        case 'e': if (ch2 == 'v' && ch3 == 'e' && ch4 == 'n' && ch5 == 't') return Keywords.Event; break;
                        case 'f':
                            switch (ch2)
                            {
                                case 'a': if (ch3 == 'l' && ch4 == 's' && ch5 == 'e') return Keywords.False; break;
                                case 'i': if (ch3 == 'x' && ch4 == 'e' && ch5 == 'd') return Keywords.Fixed; break;
                                case 'l': if (ch3 == 'o' && ch4 == 'a' && ch5 == 't') return Keywords.Float; break;
                            }
                            break;
                        case 'g': if (ch2 == 'r' && ch3 == 'o' && ch4 == 'u' && ch5 == 'p') return Keywords.Group; break;
                        case 's':
                            switch (ch2)
                            {
                                case 'b': if (ch3 == 'y' && ch4 == 't' && ch5 == 'e') return Keywords.Sbyte; break;
                                case 'h': if (ch3 == 'o' && ch4 == 'r' && ch5 == 't') return Keywords.Short; break;
                            }
                            break;
                        case 't': if (ch2 == 'h' && ch3 == 'r' && ch4 == 'o' && ch5 == 'w') return Keywords.Throw; break;
                        case 'u':
                            switch (ch2)
                            {
                                case 'l': if (ch3 == 'o' && ch4 == 'n' && ch5 == 'g') return Keywords.Ulong; break;
                                case 's': if (ch3 == 'i' && ch4 == 'n' && ch5 == 'g') return Keywords.Using; break;
                            }
                            break;
                        case 'v': if (ch2 == 'a' && ch3 == 'l' && ch4 == 'u' && ch5 == 'e') return Keywords.Value; break;
                        case 'w':
                            if (ch2 == 'h')
                            {
                                switch (ch3)
                                {
                                    case 'e': if (ch4 == 'r' && ch5 == 'e') return Keywords.Where; break;
                                    case 'i': if (ch4 == 'l' && ch5 == 'e') return Keywords.While; break;
                                }
                            }
                            break;
                        case 'y': if (ch2 == 'i' && ch3 == 'e' && ch4 == 'l' && ch5 == 'd') return Keywords.Yield; break;
                    }
                    break;
                }
                case 6:
                {
                    var ch1 = buffer[offset];
                    switch (ch1)
                    {
                        case 'd': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Double, 1, 5) == 0) return Keywords.Double; break;
                        case 'e': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Equals, 1, 5) == 0) return Keywords.Equals;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Extern, 1, 5) == 0) return Keywords.Extern; break;
                        case 'g': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Global, 1, 5) == 0) return Keywords.Global; break;
                        case 'n': if (string.CompareOrdinal(buffer, offset + 1, Keywords.NameOf, 1, 5) == 0) return Keywords.NameOf; break;
                        case 'o': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Object, 1, 5) == 0) return Keywords.Object; break;
                        case 'p': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Params, 1, 5) == 0) return Keywords.Params;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Public, 1, 5) == 0) return Keywords.Public;
                                  break;
                        case 'r': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Remove, 1, 5) == 0) return Keywords.Remove;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Return, 1, 5) == 0) return Keywords.Return;
                                  break;
                        case 's': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Sealed, 1, 5) == 0) return Keywords.Sealed;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Select, 1, 5) == 0) return Keywords.Select;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.SizeOf, 1, 5) == 0) return Keywords.SizeOf;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Static, 1, 5) == 0) return Keywords.Static;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.String, 1, 5) == 0) return Keywords.String;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Struct, 1, 5) == 0) return Keywords.Struct;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Switch, 1, 5) == 0) return Keywords.Switch;
                                  break;
                        case 't': if (string.CompareOrdinal(buffer, offset + 1, Keywords.TypeOf, 1, 5) == 0) return Keywords.TypeOf; break;
                        case 'u': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Unsafe, 1, 5) == 0) return Keywords.Unsafe;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Ushort, 1, 5) == 0) return Keywords.Ushort;
                                  break;
                    }
                    break;
                }
                case 7:
                {
                    var ch1 = buffer[offset];
                    switch (ch1)
                    {
                        case 'c': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Checked, 1, 6) == 0) return Keywords.Checked; break;
                        case 'd': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Decimal, 1, 6) == 0) return Keywords.Decimal;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Default, 1, 6) == 0) return Keywords.Default;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Dynamic, 1, 6) == 0) return Keywords.Dynamic;
                                  break;
                        case 'f': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Finally, 1, 6) == 0) return Keywords.Finally;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.ForEach, 1, 6) == 0) return Keywords.ForEach;
                                  break;
                        case 'o': if (string.CompareOrdinal(buffer, offset + 1, Keywords.OrderBy, 1, 6) == 0) return Keywords.OrderBy; break;
                        case 'p': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Partial, 1, 6) == 0) return Keywords.Partial;
                                  if (string.CompareOrdinal(buffer, offset + 1, Keywords.Private, 1, 6) == 0) return Keywords.Private;
                                  break;
                        case 'v': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Virtual, 1, 6) == 0) return Keywords.Virtual; break;
                    }
                    break;
                }
                case 8:
                {
                    var ch1 = buffer[offset];
                    switch (ch1)
                    {
                        case 'a': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Abstract, 1, 7) == 0) return Keywords.Abstract; break;
                        case 'c': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Continue, 1, 7) == 0) return Keywords.Continue; break;
                        case 'd': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Delegate, 1, 7) == 0) return Keywords.Delegate; break;
                        case 'e': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Explicit, 1, 7) == 0) return Keywords.Explicit; break;
                        case 'i': if (string.CompareOrdinal(buffer, offset + 2, Keywords.Implicit, 2, 6) == 0) return Keywords.Implicit;
                                  if (string.CompareOrdinal(buffer, offset + 2, Keywords.Internal, 2, 6) == 0) return Keywords.Internal;
                                  break;
                        case 'o': if (string.CompareOrdinal(buffer, offset + 2, Keywords.Operator, 2, 6) == 0) return Keywords.Operator;
                                  if (string.CompareOrdinal(buffer, offset + 2, Keywords.Override, 2, 6) == 0) return Keywords.Override;
                                  break;
                        case 'r': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Readonly, 1, 7) == 0) return Keywords.Readonly; break;
                        case 'v': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Volatile, 1, 7) == 0) return Keywords.Volatile; break;
                    }
                    break;
                }
                case 9:
                {
                    var ch1 = buffer[offset];
                    switch (ch1)
                    {
                        case 'a': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Ascending, 1, 8) == 0) return Keywords.Ascending; break;
                        case 'i': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Interface, 1, 8) == 0) return Keywords.Interface; break;
                        case 'n': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Namespace, 1, 8) == 0) return Keywords.Namespace; break;
                        case 'p': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Protected, 1, 8) == 0) return Keywords.Protected; break;
                        case 'u': if (string.CompareOrdinal(buffer, offset + 1, Keywords.Unchecked, 1, 8) == 0) return Keywords.Unchecked; break;
                    }
                    break;
                }
                case 10:
                {
                    if (string.CompareOrdinal(buffer, offset, Keywords.Descending, 0, length) == 0) return Keywords.Descending;
                    if (string.CompareOrdinal(buffer, offset, Keywords.Stackalloc, 0, length) == 0) return Keywords.Stackalloc;
                    break;
                }

            }

            return TrySegmentRun(buffer, offset, length, ' ', Spaces, out var spaces) ? spaces
                 : TrySegmentRun(buffer, offset, length, '\t', Tabs, out var tabs) ? tabs
                 : buffer.Substring(offset, length);
        }

        static bool TrySegmentRun(string buffer, int offset, int length,
                                    char ch, string?[] runs, [NotNullWhen(true)] out string? result)
        {
            if (length <= runs.Length && buffer[offset] == ch)
            {
                ref var run = ref runs[length - 1];

                if (run is { } r)
                {
                    if (string.CompareOrdinal(r, 0, buffer, offset, length) == 0)
                    {
                        result = r;
                        return true;
                    }
                }
                else if (buffer.All(ch, offset, length))
                {
                    result = run ??= new string(ch, length);
                    return true;
                }
            }

            result = default;
            return false;
        }

        static bool All(this string s, char ch, int offset, int length)
        {
            for (var i = 0; i < length; i++)
                if (s[offset + i] != ch)
                    return false;
            return true;
        }
    }
}
