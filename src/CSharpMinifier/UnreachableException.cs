#region Copyright (c) 2024 Atif Aziz. All rights reserved.
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

#pragma warning disable CA1064 // Exceptions should be public (by-design)
    sealed class UnreachableException(string? message, Exception? inner)
        : Exception(message ?? DefaultMessage, inner)
#pragma warning restore CA1064 // Exceptions should be public
    {
        const string DefaultMessage = "The program executed an instruction that was thought to be unreachable.";
        public UnreachableException() : this(null) { }
        public UnreachableException(string? message) : this(message, null) { }
    }
}
