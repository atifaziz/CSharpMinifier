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

namespace CSharpMinifier.Preprocessing
{
    using System;
    using System.Collections.Generic;

    public static class Scanner
    {
        public static IEnumerable<Token> Scan(string expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            return ScanImpl(expression);
        }

        enum State
        {
            Initial,
            IdentifierOrTrue,
            IdentifierOrFalse,
            True,
            False,
            Identifier,
            Ampersand,     // &
            Pipe,          // |
            Equal,         // =
            Bang,          // !
            WhiteSpace,    // space or tab
        }

        static IEnumerable<Token> ScanImpl(string s)
        {
            Token Token(TokenKind kind, int fi, int ei) =>
                new Token(kind, fi, ei);

            var state = State.Initial;
            var si = 0;
            var i = 0;
            for (; i < s.Length; i++)
            {
                var ch = s[i];
            restart:
                switch (state)
                {
                    case State.Initial:
                    {
                        switch (ch)
                        {
                            case ' ':
                            case '\t':
                                si = i;
                                state = State.WhiteSpace;
                                break;
                            case 't':
                                si = i;
                                state = State.IdentifierOrTrue;
                                break;
                            case 'f':
                                si = i;
                                state = State.IdentifierOrFalse;
                                break;
                            case '(':
                                yield return Token(TokenKind.LParen, i, i + 1);
                                break;
                            case ')':
                                yield return Token(TokenKind.RParen, i, i + 1);
                                break;
                            case '&':
                                si = i;
                                state = State.Ampersand;
                                break;
                            case '|':
                                si = i;
                                state = State.Pipe;
                                break;
                            case '!':
                                si = i;
                                state = State.Bang;
                                break;
                            case var c when char.IsLetterOrDigit(c):
                                si = i;
                                state = State.Identifier;
                                break;
                            default:
                                throw new SyntaxErrorException($"Unexpected at {i + 1}: {ch}");
                        }
                        break;
                    }
                    case State.IdentifierOrTrue:
                    {
                        switch (i - si)
                        {
                            case 1 when ch == 'r': break;
                            case 2 when ch == 'u': break;
                            case 3 when ch == 'e':
                                state = State.True;
                                break;
                            default:
                                state = State.Identifier;
                                goto restart;
                        }
                        break;
                    }
                    case State.IdentifierOrFalse:
                    {
                        switch (i - si)
                        {
                            case 1 when ch == 'a': break;
                            case 2 when ch == 'l': break;
                            case 3 when ch == 's': break;
                            case 4 when ch == 'e':
                                state = State.False;
                                break;
                            default:
                                state = State.Identifier;
                                goto restart;
                        }
                        break;
                    }
                    case State.True:
                    case State.False:
                    {
                        if (char.IsLetterOrDigit(ch))
                        {
                            state = State.Identifier;
                            break;
                        }
                        else
                        {
                            yield return Token(state == State.True ? TokenKind.True : TokenKind.False, si, i);
                            state = State.Initial;
                            goto restart;
                        }
                    }
                    case State.Identifier:
                    {
                        if (char.IsLetterOrDigit(ch))
                            break;
                        yield return Token(TokenKind.Identifier, si, i);
                        state = State.Initial;
                        goto restart;
                    }
                    case State.WhiteSpace:
                    {
                        if (ch == ' ' || ch == '\t')
                            break;
                        yield return Token(TokenKind.WhiteSpace, si, i);
                        state = State.Initial;
                        goto restart;
                    }
                    case State.Ampersand:
                    {
                        if (ch != '&')
                            throw new SyntaxErrorException($"Unexpected at {i + 1}: {ch}");
                        yield return Token(TokenKind.And, si, i + 1);
                        state = State.Initial;
                        break;
                    }
                    case State.Pipe:
                    {
                        if (ch != '|')
                            throw new SyntaxErrorException($"Unexpected at {i + 1}: {ch}");
                        yield return Token(TokenKind.Or, si, i + 1);
                        state = State.Initial;
                        break;
                    }
                    case State.Bang:
                    {
                        if (ch == '=')
                        {
                            yield return Token(TokenKind.NotEqual, si, i + 1);
                            state = State.Initial;
                            break;
                        }
                        else
                        {
                            yield return Token(TokenKind.Not, si, i);
                            state = State.Initial;
                            goto restart;
                        }
                    }
                    default:
                        throw new Exception("Internal error due to unhandled state: " + state);
                }
            }

            switch (state)
            {
                case State.IdentifierOrTrue:
                case State.IdentifierOrFalse:
                case State.Identifier: yield return Token(TokenKind.Identifier, si, i); break;
                case State.Initial:
                case State.WhiteSpace:
                    break;
                case State.True: yield return Token(TokenKind.True, si, i); break;
                case State.False:  yield return Token(TokenKind.False, si, i); break;
                case State.Ampersand:
                case State.Pipe:
                case State.Equal:
                case State.Bang:
                    throw new SyntaxErrorException($"Syntax error at {i + 1}.");
                default:
                    throw new Exception("Internal error due to unhandled state: " + state);
            }
        }
    }
}
