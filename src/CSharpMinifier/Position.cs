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

    public readonly record struct Position
    {
        readonly int _line;
        readonly int _column;

        public int Offset { get; }
        public int Line => _line + 1;
        public int Column => _column + 1;

        public Position(int offset, int line, int column)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, null);
            if (line   < 1) throw new ArgumentOutOfRangeException(nameof(line), line, null);
            if (column < 1) throw new ArgumentOutOfRangeException(nameof(column), column, null);

            Offset  = offset;
            _line   = line - 1;
            _column = column - 1;
        }

        public override string ToString() => $"{Offset}/{Line}:{Column}";
    }
}
