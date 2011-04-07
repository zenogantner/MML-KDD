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
using MyMediaLite.Data;

namespace MyMediaLite.ItemRecommendation
{
	public abstract class AttributeCounter : ItemRecommender, IKDDCupRecommender
	{
		Dictionary<int, int>[] attribute_count_by_user;

		/// <inheritdoc/>
		public KDDCupItems ItemInfo { get; set; }

		/// <summary>Get attributes for an item</summary>
		/// <param name="item_id">the item ID</param>
		/// <returns>a list of attribute IDs</returns>
		protected abstract IList<int> GetAttributes(int item_id);

		/// <inheritdoc/>
		public void InitModel()
		{
			attribute_count_by_user = new Dictionary<int, int>[Feedback.MaxUserID + 1];
			for (int i = 0; i < attribute_count_by_user.Length; i++)
				attribute_count_by_user[i] = new Dictionary<int, int>();
		}

		/// <inheritdoc/>
		public override double Predict(int user_id, int item_id)
		{
			IList<int> attributes = GetAttributes(item_id);
			
			int sum = 0;
			int a_count;
			foreach (int a in attributes)
				if (attribute_count_by_user[user_id].TryGetValue(a, out a_count))
					sum += a_count;

			return (double) sum / attributes.Count;
		}

		/// <inheritdoc/>
		public override void Train()
		{
			//base.Train();
			InitModel();

			foreach (int u in Feedback.UserMatrix.NonEmptyRowIDs)
				foreach (int i in Feedback.UserMatrix[u])
					foreach (int a in GetAttributes(i))
						if (attribute_count_by_user[u].ContainsKey(a))
							attribute_count_by_user[u][a]++;
						else
							attribute_count_by_user[u][a] = 1;
		}

		/// <inheritdoc/>
		public override void SaveModel(string filename)
		{
			// do nothing
		}

		/// <inheritdoc/>
		public override void LoadModel(string filename)
		{
			// do nothing
		}
	}
}

