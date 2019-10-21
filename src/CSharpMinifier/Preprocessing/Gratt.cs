#region Copyright (c) 2019 Atif Aziz. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
#endregion

namespace Gratt
{
    using System;
    using System.Collections.Generic;
    using Unit = System.ValueTuple;

    static partial class Parser
    {
        public static TResult
            Parse<TKind, TToken, TPrecedence, TResult>(
                TPrecedence initialPrecedence,
                Func<TKind, TToken, Func<TToken, Parser<Unit, TKind, TToken, TPrecedence, TResult>, TResult>> prefixFunction,
                Func<TKind, TToken, (TPrecedence, Func<TToken, TResult, Parser<Unit, TKind, TToken, TPrecedence, TResult>, TResult>)?> infixFunction,
                IEnumerable<(TKind, TToken)> lexer) =>
            Parse(default(Unit), initialPrecedence,
                  (k, t, s) => prefixFunction(k, t), (k, t, s) => infixFunction(k, t), lexer);

        public static TResult
            Parse<TKind, TToken, TPrecedence, TResult>(
                TPrecedence initialPrecedence, IComparer<TPrecedence> precedenceComparer,
                IEqualityComparer<TKind> kindEqualityComparer,
                Func<TKind, TToken, Func<TToken, Parser<Unit, TKind, TToken, TPrecedence, TResult>, TResult>> prefixFunction,
                Func<TKind, TToken, (TPrecedence, Func<TToken, TResult, Parser<Unit, TKind, TToken, TPrecedence, TResult>, TResult>)?> infixFunction,
                IEnumerable<(TKind, TToken)> lexer) =>
            Parse(default(Unit), initialPrecedence, precedenceComparer, kindEqualityComparer,
                  (k, t, s) => prefixFunction(k, t), (k, t, s) => infixFunction(k, t), lexer);

        public static TResult
            Parse<TState, TKind, TToken, TPrecedence, TResult>(
                TState state,
                TPrecedence initialPrecedence,
                Func<TKind, TToken, TState, Func<TToken, Parser<TState, TKind, TToken, TPrecedence, TResult>, TResult>> prefixFunction,
                Func<TKind, TToken, TState, (TPrecedence, Func<TToken, TResult, Parser<TState, TKind, TToken, TPrecedence, TResult>, TResult>)?> infixFunction,
                IEnumerable<(TKind, TToken)> lexer) =>
            Parse(state, initialPrecedence, Comparer<TPrecedence>.Default, EqualityComparer<TKind>.Default,
                  prefixFunction, infixFunction, lexer);

        public static TResult
            Parse<TState, TKind, TToken, TPrecedence, TResult>(
                TState state,
                TPrecedence initialPrecedence, IComparer<TPrecedence> precedenceComparer,
                IEqualityComparer<TKind> kindEqualityComparer,
                Func<TKind, TToken, TState, Func<TToken, Parser<TState, TKind, TToken, TPrecedence, TResult>, TResult>> prefixFunction,
                Func<TKind, TToken, TState, (TPrecedence, Func<TToken, TResult, Parser<TState, TKind, TToken, TPrecedence, TResult>, TResult>)?> infixFunction,
                IEnumerable<(TKind, TToken)> lexer)
        {
            var parser =
                new Parser<TState, TKind, TToken, TPrecedence, TResult>(state,
                                                                        precedenceComparer,
                                                                        kindEqualityComparer,
                                                                        prefixFunction, infixFunction,
                                                                        lexer.GetEnumerator());
            return parser.Parse(initialPrecedence);
        }
    }

    partial class Parser<TState, TKind, TToken, TPrecedence, TResult>
    {
        readonly IComparer<TPrecedence> _precedenceComparer;
        readonly IEqualityComparer<TKind> _tokenEqualityComparer;
        readonly Func<TKind, TToken, TState, Func<TToken, Parser<TState, TKind, TToken, TPrecedence, TResult>, TResult>> _prefixFunction;
        readonly Func<TKind, TToken, TState, (TPrecedence, Func<TToken, TResult, Parser<TState, TKind, TToken, TPrecedence, TResult>, TResult>)?> _infixFunction;
        (bool, TKind, TToken) _next;
        IEnumerator<(TKind, TToken)> _enumerator;

        internal Parser(TState state,
                        IComparer<TPrecedence> precedenceComparer,
                        IEqualityComparer<TKind> tokenEqualityComparer,
                        Func<TKind, TToken, TState, Func<TToken, Parser<TState, TKind, TToken, TPrecedence, TResult>, TResult>> prefixFunction,
                        Func<TKind, TToken, TState, (TPrecedence, Func<TToken, TResult, Parser<TState, TKind, TToken, TPrecedence, TResult>, TResult>)?> infixFunction,
                        IEnumerator<(TKind, TToken)> lexer)
        {
            State = state;
            _precedenceComparer = precedenceComparer;
            _tokenEqualityComparer = tokenEqualityComparer;
            _prefixFunction = prefixFunction;
            _infixFunction = infixFunction;
            _enumerator = lexer;
        }

        public TState State { get; set; }

        public TResult Parse(TPrecedence precedence)
        {
            var read = TryRead();
            if (!read.HasValue)
                throw new ParseException("Unexpected end of input.");
            var (kind, token) = read.Value;
            var prefix = _prefixFunction(kind, token, State);
            var left = prefix(token, this);

            var peeked = TryPeek();

            while (peeked.HasValue)
            {
                (kind, token) = peeked.Value;
                switch (_infixFunction(kind, token, State))
                {
                    case var (p, infix) when _precedenceComparer.Compare(precedence, p) < 0:
                        TryRead();
                        left = infix(token, left, this);
                        peeked = TryPeek();
                        break;
                    default:
                        return left;
                }
            }

            return left;
        }

        public bool Match(TKind kind)
        {
            switch (TryPeek())
            {
                case var (k, _) when _tokenEqualityComparer.Equals(k, kind): Read(); return true;
                default: return false;
            }
        }

        public (TKind, TToken) Read()
        {
            switch (TryPeek())
            {
                case var (kind, token): TryRead(); return (kind, token);
                default: throw new InvalidOperationException();
            }
        }

        public TToken Read(TKind kind, Func<TKind, (TKind, TToken)?, Exception> onError)
        {
            switch (TryPeek())
            {
                case var (k, t) when _tokenEqualityComparer.Equals(k, kind): Read(); return t;
                case var peeked: throw onError(kind, peeked);
            }
        }

        public (TKind, TToken)? TryPeek()
        {
            switch (TryRead())
            {
                case var (kind, token): Unread(kind, token); return (kind, token);
                default: return default;
            }
        }

        public (TKind, TToken)? TryRead()
        {
            switch (_next)
            {
                case (true, var kind, var token):
                    _next = default;
                    return (kind, token);
                default:
                    switch (_enumerator)
                    {
                        case null: return default;
                        case var e:
                            if (!e.MoveNext())
                            {
                                _enumerator.Dispose();
                                _enumerator = null;
                                return default;
                            }
                            var (kind, token) = e.Current;
                            return (kind, token);
                    }
            }
        }

        void Unread(TKind kind, TToken token) => _next = _next switch
        {
            (true, _, _) => throw new InvalidOperationException(),
            _ => (true, kind, token)
        };
    }

    #if !PRATT_NO_SERIALIZABLE
    [Serializable]
    #endif
    partial class ParseException : Exception
    {
        public ParseException() {}
        public ParseException(string message) : base(message) {}
        public ParseException(string message, Exception inner) : base(message, inner) {}
    }
}
