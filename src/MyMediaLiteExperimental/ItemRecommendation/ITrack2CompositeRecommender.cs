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

using MyMediaLite.Data;
using MyMediaLite.RatingPrediction;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>Composite recommender interface</summary>
	public interface ITrack2CompositeRecommender
	{
		/// <summary>The ratings dataset</summary>
		IRatings Ratings { get; set; }

		/// <summary>predicts whether an item was rated</summary>
		ItemRecommender RatedComponent { get; set; }

		/// <summary>predicts how an item was rated</summary>
		RatingPredictor RatingComponent { get; set; }

	}
}

