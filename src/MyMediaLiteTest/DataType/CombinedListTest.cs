// Copyright (C) 2011 Zeno Gantner
//
// This file is part of MyMediaLite.
//
// MyMediaLite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MyMediaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using MyMediaLite.DataType;

namespace MyMediaLiteTest
{
	[TestFixture()]
	public class CombinedListTest
	{
		private IList<int> CreateSequence()
		{
			return new int[] { 1, 3, 5, 7, 9, 2, 4, 6, 8, 10 };
		}

		private IList<int> CreateOddSequence()
		{
			return new int[] { 1, 3, 5, 7, 9 };
		}

		private IList<int> CreateEvenSequence()
		{
			return new int[] { 2, 4, 6, 8, 10 };
		}

		[Test()] public void TestIndex()
		{
			var combined_list = new CombinedList<int>(CreateOddSequence(), CreateEvenSequence());

			var list = CreateSequence();

			for (int i = 0; i < combined_list.Count; i++)
				Assert.AreEqual(list[i], combined_list[i]);
		}

		[Test()] public void TestCount()
		{
			var combined_list = new CombinedList<int>(CreateOddSequence(), CreateEvenSequence());

			Assert.AreEqual(CreateSequence().Count, combined_list.Count);
		}

		[Test()] public void TestIsReadOnly()
		{
			var combined_list = new CombinedList<int>(CreateOddSequence(), CreateEvenSequence());

			Assert.IsTrue(combined_list.IsReadOnly);
		}

		[Test()] public void TestContains()
		{
			var combined_list = new CombinedList<int>(CreateOddSequence(), CreateEvenSequence());

			foreach (int num in CreateEvenSequence())
				Assert.IsTrue(combined_list.Contains(num));

			foreach (int num in CreateOddSequence())
				Assert.IsTrue(combined_list.Contains(num));
		}
	}
}

