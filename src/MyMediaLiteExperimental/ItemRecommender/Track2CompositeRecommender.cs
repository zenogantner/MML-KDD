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
using MyMediaLite.RatingPrediction;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>Two-stage recommender for KDD Cup</summary>
	public abstract class Track2CompositeRecommender<RatedComponent, RatingComponent> : ItemRecommender
		where RatedComponent  : ItemRecommender, new()
		where RatingComponent : RatingPredictor, new()
	{
		/// <summary>predicts whether an item was rated</summary>
		protected RatedComponent   rated_component;
		/// <summary>predicts how an item was rated</summary>
		protected RatingComponent rating_component;
		
		/// <summary>Default constructor</summary>
		public Track2CompositeRecommender()
		{
			rated_component = new RatedComponent();
			rating_component = new RatingComponent();
		}
		
		/// <inheritdoc/>
		public override double Predict(int user_id, int item_id)
		{
			return rated_component.Predict(user_id, item_id) * rating_component.Predict(user_id, item_id);
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
	}
}

