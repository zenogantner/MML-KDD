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
	/// <summary>wrapper class for item recommenders in KDD Cup track 2</summary>
	public class Track2CompositeRatingRecommender<RatedComponent, RatingComponent> : Track2CompositeRecommender<RatedComponent, RatingComponent>
		where RatedComponent  : ItemRecommender, new()
		where RatingComponent : RatingPredictor, new()		
	{
		/// <inheritdoc/>		
		public override void Train()
		{
			// prepare data
			
			// do training
			rated_component.Train();
			rating_component.Train();
		}		
	}
}

