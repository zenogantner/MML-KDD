// Copyright (C) 2010 Steffen Rendle, Zeno Gantner
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MyMediaLite.data;
using MyMediaLite.data_type;
using MyMediaLite.item_recommender;
using MyMediaLite.rating_predictor;
using MyMediaLite.util;


namespace MyMediaLite.eval
{
    /// <summary>Class that contains static methods for item prediction</summary>
    public static class ItemPrediction
    {
		// TODO there are too many different versions of this method interface - we should simplify the API

		/// <summary>
		/// Write item predictions (scores) for all users to a file
		/// </summary>
		/// <param name="engine">the <see cref="RecommenderEngine"/> to use for making the predictions</param>
		/// <param name="train">a user-wise <see cref="SparseBooleanMatrix"/> containing the items already observed</param>
		/// <param name="relevant_items">the list of candidate items</param>
		/// <param name="num_predictions">the number of items to return per user, -1 if there should be no limit</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="filename">the name of the file to write to</param>
		static public void WritePredictions(
			RecommenderEngine engine,
			SparseBooleanMatrix train,
			ICollection<int> relevant_items,
			int num_predictions,
		    EntityMapping user_mapping, EntityMapping item_mapping,
			string filename)
		{
			if (filename.Equals("-"))
				WritePredictions(engine, train, relevant_items, num_predictions, user_mapping, item_mapping, Console.Out);
			else
				using ( StreamWriter writer = new StreamWriter(filename) )
				{
					WritePredictions(engine, train, relevant_items, num_predictions, user_mapping, item_mapping, writer);
				}
		}

		/// <summary>Write item predictions (scores) to a file</summary>
		/// <param name="engine">the <see cref="RecommenderEngine"/> to use for making the predictions</param>
		/// <param name="train">a user-wise <see cref="SparseBooleanMatrix"/> containing the items already observed</param>
		/// <param name="relevant_users">a list of users to make recommendations for</param>
		/// <param name="relevant_items">the list of candidate items</param>
		/// <param name="num_predictions">the number of items to return per user, -1 if there should be no limit</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="filename">the name of the file to write to</param>
		static public void WritePredictions(
			RecommenderEngine engine,
			SparseBooleanMatrix train,
		    IList<int> relevant_users,
			ICollection<int> relevant_items,
			int num_predictions,
		    EntityMapping user_mapping, EntityMapping item_mapping,
			string filename)
		{
			if (filename.Equals("-"))
				WritePredictions(engine, train, relevant_users, relevant_items, num_predictions, user_mapping, item_mapping, Console.Out);
			else
				using ( StreamWriter writer = new StreamWriter(filename) )
				{
					WritePredictions(engine, train, relevant_users, relevant_items, num_predictions, user_mapping, item_mapping, writer);
				}
		}

		/// <summary>
		/// Write item predictions (scores) for all users to a TextWriter object
		/// </summary>
		/// <param name="engine">the <see cref="RecommenderEngine"/> to use for making the predictions</param>
		/// <param name="train">a user-wise <see cref="SparseBooleanMatrix"/> containing the items already observed</param>
		/// <param name="relevant_items">the list of candidate items</param>
		/// <param name="num_predictions">the number of items to return per user, -1 if there should be no limit</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="writer">the <see cref="TextWriter"/> to write to</param>
		static public void WritePredictions(
			RecommenderEngine engine,
			SparseBooleanMatrix train,
			ICollection<int> relevant_items,
			int num_predictions,
		    EntityMapping user_mapping, EntityMapping item_mapping,
			TextWriter writer)
		{
			List<int> relevant_users = new List<int>(user_mapping.InternalIDs);
			WritePredictions(engine, train, relevant_users, relevant_items, num_predictions, user_mapping, item_mapping, writer);
		}

		/// <summary>Write item predictions (scores) to a TextWriter object</summary>
		/// <param name="engine">the <see cref="RecommenderEngine"/> to use for making the predictions</param>
		/// <param name="train">a user-wise <see cref="SparseBooleanMatrix"/> containing the items already observed</param>
		/// <param name="relevant_users">a list of users to make recommendations for</param>
		/// <param name="relevant_items">the list of candidate items</param>
		/// <param name="num_predictions">the number of items to return per user, -1 if there should be no limit</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="writer">the <see cref="TextWriter"/> to write to</param>
		static public void WritePredictions(
			RecommenderEngine engine,
			SparseBooleanMatrix train,
		    IList<int> relevant_users,
			ICollection<int> relevant_items,
			int num_predictions,
		    EntityMapping user_mapping, EntityMapping item_mapping,
			TextWriter writer)
		{
			foreach (int user_id in relevant_users)
			{
				HashSet<int> ignore_items = train[user_id];
				WritePredictions(engine, user_id, relevant_items, ignore_items, num_predictions, user_mapping, item_mapping, writer);
			}
		}

		/// <summary>
		/// Write item predictions (scores) to a TextWriter object
		/// </summary>
		/// <param name="engine">the <see cref="RecommenderEngine"/> to use for making the predictions</param>
		/// <param name="user_id">the ID of the user to make recommendations for</param>
		/// <param name="relevant_items">the list of candidate items</param>
		/// <param name="ignore_items">a list of items for which no predictions should be made</param>
		/// <param name="num_predictions">the number of items to return per user, -1 if there should be no limit</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="writer">the <see cref="TextWriter"/> to write to</param>
		static public void WritePredictions(
			RecommenderEngine engine,
            int user_id,
		    ICollection<int> relevant_items,
		    ICollection<int> ignore_items,
			int num_predictions,
		    EntityMapping user_mapping, EntityMapping item_mapping,
		    TextWriter writer)
		{
			NumberFormatInfo ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

            List<WeightedItem> score_list = new List<WeightedItem>();
            foreach (int item_id in relevant_items)
                score_list.Add( new WeightedItem(item_id, engine.Predict(user_id, item_id)));

			score_list.Sort(); // TODO actually a heap would be enough
			score_list.Reverse();

			int prediction_count = 0;

			foreach (var wi in score_list)
			{
				// TODO move up the ignore_items check
				if (!ignore_items.Contains(wi.item_id) && wi.weight > double.MinValue)
				{
					writer.WriteLine("{0}\t{1}\t{2}",
					                 user_mapping.ToOriginalID(user_id), item_mapping.ToOriginalID(wi.item_id),
					                 wi.weight.ToString(ni));
					prediction_count++;
				}

				if (prediction_count == num_predictions)
					break;
			}
		}

		/// <summary>
		/// predict items for a specific users
		/// </summary>
		/// <param name="engine">the <see cref="RecommenderEngine"/> object to use for the predictions</param>
		/// <param name="user_id">the user ID</param>
		/// <param name="max_item_id">the maximum item ID</param>
		/// <returns>a list sorted list of item IDs</returns>
		static public int[] PredictItems(RecommenderEngine engine, int user_id, int max_item_id)
		{
            List<WeightedItem> result = new List<WeightedItem>();
            for (int item_id = 0; item_id < max_item_id + 1; item_id++)
                result.Add( new WeightedItem(item_id, engine.Predict(user_id, item_id)));

			result.Sort();
			result.Reverse();

            int[] return_array = new int[max_item_id + 1];
            for (int i = 0; i < return_array.Length; i++)
            	return_array[i] = result[i].item_id;

            return return_array;
		}

		static public int[] PredictItems(RecommenderEngine engine, int user_id, ICollection<int> relevant_items)
		{
            List<WeightedItem> result = new List<WeightedItem>();

            foreach (int item_id in relevant_items)
                result.Add( new WeightedItem(item_id, engine.Predict(user_id, item_id)));

			result.Sort();
			result.Reverse();

            int[] return_array = new int[result.Count];
            for (int i = 0; i < return_array.Length; i++)
            	return_array[i] = result[i].item_id;
            return return_array;
		}
    }
}