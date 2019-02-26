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

    public class SyntaxErrorException : Exception
    {
        public SyntaxErrorException() {}

        public SyntaxErrorException(string message) :
            base(message) {}

        public SyntaxErrorException(string message, Exception inner) :
            base(message, inner) {}
    }
}
