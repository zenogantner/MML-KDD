// Copyright (C) 2010, 2011 Zeno Gantner
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
using System.Diagnostics;
using MyMediaLite.Correlation;
using MyMediaLite.Data;

namespace MyMediaLite.Diversification
{
	/// <summary>Sequential Diversification</summary>
	/// <remarks>
	/// Cai-Nicolas Ziegler, Sean McNee, Joseph A. Konstan, Georg Lausen:
	/// Improving Recommendation Lists Through Topic Diversification.
	/// WWW 2005
	/// </remarks>
	public class SequentialDiversification
	{
		CorrelationMatrix ItemCorrelations { get; set; }

		/// <summary>Constructor</summary>
		/// <param name="item_correlation">the similarity measure to use for diversification</param>
		public SequentialDiversification(CorrelationMatrix item_correlation)
		{
			ItemCorrelations = item_correlation;
		}

		/// <summary>Diversify an item list</summary>
		/// <param name="item_list">a list of items</param>
		/// <param name="diversification_parameter">the diversification parameter (higher means more diverse)</param>
		/// <returns>a list re-ordered to ensure maximum diversity at the top of the list</returns>
		public IList<int> DiversifySequential(IList<int> item_list, double diversification_parameter)
		{
			Trace.Assert(item_list.Count > 0);

			var item_rank_by_rating = new Dictionary<int, int>();
			for (int i = 0; i < item_list.Count; i++)
				item_rank_by_rating[item_list[i]] = i;

			var diversified_item_list = new List<int>();
			int top_item = item_list[0];
			diversified_item_list.Add(top_item);

			var item_set = new HashSet<int>(item_list);
			item_set.Remove(top_item);
			while (item_set.Count > 0)
			{
				// rank remaining items by diversity
				var items_by_diversity = new List<WeightedItem>();
				foreach (int item_id in item_set)
				{
					double similarity = Similarity(item_id, diversified_item_list, ItemCorrelations);
					items_by_diversity.Add(new WeightedItem(item_id, similarity));
				}
				items_by_diversity.Sort();

				// if too slow: replace by priority queue from C5
				var items_by_merged_rank = new List<WeightedItem>();
				for (int i = 0; i < items_by_diversity.Count; i++)
				{
					int item_id = items_by_diversity[i].item_id;
					// i is the dissimilarity rank
					// TODO adjust for ties
					double score = item_rank_by_rating[item_id] * (1 - diversification_parameter) + i * diversification_parameter;

					items_by_merged_rank.Add(new WeightedItem(item_id, score));
				}
				items_by_merged_rank.Sort();

				int next_item_id = items_by_merged_rank[0].item_id;
				diversified_item_list.Add(next_item_id);
				item_set.Remove(next_item_id);
			}
			return diversified_item_list;
		}

		// TODO think about moving the next two methods to their own class

		/// <summary>Compute similarity between one item and a collection of items</summary>
		/// <param name="item_id">the item ID</param>
		/// <param name="items">a collection of items</param>
		/// <param name="item_correlation">the similarity measure to use</param>
		/// <returns>the similarity between the item and the collection</returns>
		public static double Similarity(int item_id, ICollection<int> items, CorrelationMatrix item_correlation)
		{
			double similarity = 0;
			foreach (int other_item_id in items)
				similarity += item_correlation[item_id, other_item_id];
			return similarity;
		}

		/// <summary>Compute the intra-set similarity of an item collection</summary>
		/// <param name="items">a collection of items</param>
		/// <param name="item_correlation">the similarity measure to use</param>
		/// <returns>the intra-set similarity of the collection</returns>
		public static double Similarity(ICollection<int> items, CorrelationMatrix item_correlation)
		{
			double similarity = 0;
			for (int i = 0; i < items.Count; i++)
				for (int j = i + 1; j < items.Count; j++)
					similarity += item_correlation[i, j];

			return similarity;
		}
	}
}