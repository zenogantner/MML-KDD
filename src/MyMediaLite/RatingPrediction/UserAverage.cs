// Copyright (C) 2010 Zeno Gantner, Steffen Rendle
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

using System.Collections.Generic;
using MyMediaLite.Data;

namespace MyMediaLite.RatingPrediction
{
	/// <summary>Uses the average rating value of a user for predictions</summary>
	/// <remarks>
	/// This engine does NOT support online updates.
	/// </remarks>
	public class UserAverage : EntityAverage
	{
		///
		public override void Train()
		{
			base.Train(Ratings.Users, Ratings.MaxUserID);
		}

		///
		public override bool CanPredict(int user_id, int item_id)
		{
			return (user_id <= Ratings.MaxUserID);
		}

		///
		public override double Predict(int user_id, int item_id)
		{
			if (user_id < entity_averages.Count)
				return entity_averages[user_id];
			else
				return global_average;
		}

		///
		public override string ToString()
		{
			return "UserAverage";
		}
	}
}