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
using System.Globalization;
using MyMediaLite.RatingPrediction;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>Simple baseline model that combines BPR and ItemAverage</summary>
	public class BaselineCompositeRatingBPR : Track2CompositeRatingRecommender<BPRMF_KDD, ItemAverage>, IIterativeModel
	{
		/// <summary>Number of iterations for training the BPR component</summary>
		public int NumIter    { get { return rated_component.NumIter;    } set { rated_component.NumIter    = value; } }
		/// <summary>Number of factors of the BPR component</summary>
		public int NumFactors { get { return rated_component.NumFactors; } set { rated_component.NumFactors = value; } }
		/// <summary>Use the first item latent factor as a bias term if set to true</summary>
		public bool ItemBias { get { return rated_component.ItemBias; } set { rated_component.ItemBias = value; }	}

		/// <summary>Learning rate alpha</summary>
		public double LearnRate { get {	return rated_component.LearnRate; } set { rated_component.LearnRate = value; } }

		/// <summary>Regularization parameter for positive item factors</summary>
		public double RegI { get { return rated_component.RegI; } set { rated_component.RegI = value;	} }

		/// <summary>Regularization parameter for negative item factors</summary>
		public double RegJ { get { return rated_component.RegJ; } set { rated_component.RegJ = value; } }

		/// <summary>Regularization parameter for user factors</summary>
		public double RegU { get { return rated_component.RegU; } set { rated_component.RegU = value; } }

		/// <inheritdoc/>
		public double ComputeFit() { return rated_component.ComputeFit(); }

		/// <inheritdoc/>
		public void Iterate()
		{
			rated_component.Iterate();
		}

		/// <inheritdoc/>
		public override string ToString ()
		{
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			return string.Format(ni, "BaselineCompositeRatingBPR num_factors={0} item_bias={1} reg_u={2} reg_i={3} reg_j={4} num_iter={5} learn_rate={6}",
								 NumFactors, ItemBias, RegU, RegI, RegJ, NumIter, LearnRate);

		}
	}
}

