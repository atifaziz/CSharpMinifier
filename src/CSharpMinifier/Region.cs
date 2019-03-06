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
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public sealed class Region
    {
        IReadOnlyList<Token> _tokens;

        internal Region(string message, string endMessage, List<Token> tokens)
        {
            Message    = message    ?? throw new ArgumentNullException(nameof(message));
            EndMessage = endMessage ?? throw new ArgumentNullException(nameof(endMessage));
            _tokens    = tokens     ?? throw new ArgumentNullException(nameof(tokens));
        }

        public string Message    { get; }
        public string EndMessage { get; }

        public IReadOnlyList<Token> Tokens
            => _tokens is List<Token> tokens
             ? _tokens = new ReadOnlyCollection<Token>(tokens)
             : _tokens;

        public override string ToString() => Message;
    }
}
