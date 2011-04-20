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
using MyMediaLite.Data;
using MyMediaLite.RatingPrediction;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>Two-stage recommender for KDD Cup</summary>
	public abstract class Track2CompositeRecommender<RatedComponentType, RatingComponentType> : ItemRecommender, ITrack2CompositeRecommender<RatedComponentType, RatingComponentType>
		where RatedComponentType  : ItemRecommender, new()
		where RatingComponentType : RatingPredictor, new()
	{
		/// <summary>predicts whether an item was rated</summary>
		public RatedComponentType RatedComponent { get; set; }
		/// <summary>predicts how an item was rated</summary>
		public RatingComponentType RatingComponent { get; set; }

		/// <inheritdoc/>
		public IRatings Ratings
		{
			get { return ratings; }
			set {
				ratings = value;
				RatedComponent.Feedback = CreateFeedback(ratings);
			}
		}
		private IRatings ratings;

		/// <inheritdoc/>
		public override PosOnlyFeedback Feedback
		{
			get {
				return RatedComponent.Feedback;
			}
			set {
				throw new NotSupportedException();
			}
		}

		/// <summary>Default constructor</summary>
		public Track2CompositeRecommender()
		{
			RatedComponent = new RatedComponentType();
			RatingComponent = new RatingComponentType();
		}

		/// <inheritdoc/>
		public override double Predict(int user_id, int item_id)
		{
			return RatedComponent.Predict(user_id, item_id) * RatingComponent.Predict(user_id, item_id);
		}

		/// <inheritdoc/>
		public override void LoadModel(string filename)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void SaveModel(string filename)
		{
			throw new NotImplementedException();
		}

		/// <summary>Create positive-only feedback from rating data</summary>
		/// <param name="ratings">the rating data</param>
		/// <returns>the positive-only feedback</returns>
		static protected PosOnlyFeedback CreateFeedback(IRatings ratings)
		{
			var feedback = new PosOnlyFeedback();

			for (int i = 0; i < ratings.Count; i++)
				feedback.Add(ratings.Users[i], ratings.Items[i]);

			return feedback;
		}
	}
}

