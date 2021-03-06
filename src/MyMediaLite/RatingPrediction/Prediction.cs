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
using System.Globalization;
using System.IO;
using MyMediaLite.Data;
using MyMediaLite.RatingPrediction;

namespace MyMediaLite.RatingPrediction
{
	/// <summary>Class that contains static methods for rating prediction</summary>
	public class Prediction
	{
		// TODO as soon as we drop support for Mono 2.6, use default arguments

		/// <summary>Rate a given set of instances and write it to a TextWriter</summary>
		/// <param name="recommender">rating predictor</param>
		/// <param name="ratings">test cases</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="line_format">a format string specifying the line format; {0} is the user ID, {1} the item ID, {2} the rating</param>
		/// <param name="writer">the TextWriter to write the predictions to</param>
		public static void WritePredictions(
			IRatingPredictor recommender,
			IRatings ratings,
			IEntityMapping user_mapping, IEntityMapping item_mapping,
		    string line_format,
			TextWriter writer)
		{
			for (int index = 0; index < ratings.Count; index++)
				writer.WriteLine(line_format,
								 user_mapping.ToOriginalID(ratings.Users[index]),
								 item_mapping.ToOriginalID(ratings.Items[index]),
								 recommender.Predict(ratings.Users[index], ratings.Items[index]).ToString(CultureInfo.InvariantCulture));
		}

		/// <summary>Rate a given set of instances and write it to a file</summary>
		/// <param name="recommender">rating predictor</param>
		/// <param name="ratings">test cases</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="line_format">a format string specifying the line format; {0} is the user ID, {1} the item ID, {2} the rating</param>
		/// <param name="filename">the name of the file to write the predictions to</param>
		public static void WritePredictions(
			IRatingPredictor recommender,
			IRatings ratings,
			IEntityMapping user_mapping, IEntityMapping item_mapping,
		    string line_format,
			string filename)
		{
			if (filename.Equals("-"))
				WritePredictions(recommender, ratings, user_mapping, item_mapping, line_format, Console.Out);
			else
				using ( var writer = new StreamWriter(filename) )
					WritePredictions(recommender, ratings, user_mapping, item_mapping, line_format, writer);
		}

		/// <summary>Rate a given set of instances and write it to a file</summary>
		/// <param name="recommender">rating predictor</param>
		/// <param name="ratings">test cases</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="filename">the name of the file to write the predictions to</param>
		public static void WritePredictions(
			IRatingPredictor recommender,
			IRatings ratings,
			IEntityMapping user_mapping, IEntityMapping item_mapping,
			string filename)
		{
			WritePredictions(recommender, ratings, user_mapping, item_mapping, "{0}\t{1}\t{2}", filename);
		}

		/// <summary>Rate a given set of instances and write it to a TextWriter</summary>
		/// <param name="recommender">rating predictor</param>
		/// <param name="ratings">test cases</param>
		/// <param name="user_mapping">an <see cref="EntityMapping"/> object for the user IDs</param>
		/// <param name="item_mapping">an <see cref="EntityMapping"/> object for the item IDs</param>
		/// <param name="writer">the TextWriter to write the predictions to</param>
		public static void WritePredictions(
			IRatingPredictor recommender,
			IRatings ratings,
			IEntityMapping user_mapping, IEntityMapping item_mapping,
			TextWriter writer)
		{
			WritePredictions(recommender, ratings, user_mapping, item_mapping, "{0}\t{1}\t{2}", writer);
		}
	}
}
