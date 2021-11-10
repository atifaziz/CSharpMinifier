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

namespace CSharpMinifier.Tests
{
    using System;
    using System.Diagnostics;
    using NUnit.Framework;

    [TestFixture]
    public class PositionTests
    {
        [Test]
        public void Default()
        {
            var pos = new Position();
            Assert.That(pos.Offset, Is.EqualTo(0));
            Assert.That(pos.Line, Is.EqualTo(1));
            Assert.That(pos.Column, Is.EqualTo(1));
        }

        [Test]
        public void InvalidOffset()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Position(-42, 1, 1));
            Debug.Assert(e is not null);
            Assert.That(e.ParamName, Is.EqualTo("offset"));
            Assert.That(e.ActualValue, Is.EqualTo(-42));
        }

        [Test]
        public void InvalidLine()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Position(0, -42, 1));
            Debug.Assert(e is not null);
            Assert.That(e.ParamName, Is.EqualTo("line"));
            Assert.That(e.ActualValue, Is.EqualTo(-42));
        }

        [Test]
        public void InvalidColumn()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Position(0, 1, -42));
            Debug.Assert(e is not null);
            Assert.That(e.ParamName, Is.EqualTo("column"));
            Assert.That(e.ActualValue, Is.EqualTo(-42));
        }

        [Test]
        public void Equality()
        {
            var pos1 = new Position(42, 4, 2);
            Assert.That(pos1.Equals(pos1), Is.True);
            Assert.That(pos1.Equals((object) pos1), Is.True);

            var pos2 = new Position(42, 4, 2);
            Assert.That(pos1.Equals(pos2), Is.True);
            Assert.That(pos1.Equals((object) pos2), Is.True);
        }

        [Test]
        public void Inequality()
        {
            var pos = new Position(42, 4, 2);
            Assert.That(pos.Equals(default(Position)), Is.False);
            Assert.That(pos.Equals((object) default(Position)), Is.False);
        }

        [Test]
        public void InequalityWithNull()
        {
            var pos = new Position(42, 4, 2);
            Assert.That(pos.Equals(null), Is.False);
        }

        [Test]
        public void InequalityWithAnotherType()
        {
            var pos = new Position(42, 4, 2);
            Assert.That(pos.Equals(new object()), Is.False);
        }
    }
}
