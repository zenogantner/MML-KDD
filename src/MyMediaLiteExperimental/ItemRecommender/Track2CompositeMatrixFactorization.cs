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
	public class Track2CompositeMatrixFactorization : Track2CompositeRatingRecommender<BPRMF_KDD, BiasedMatrixFactorization>, IIterativeModel
	{
		/// <summary>Number of iterations</summary>
		public int NumIter    { get { return rated_component.NumIter;    } set { rated_component.NumIter    = value; } }
		/// <summary>Number of factors of the BPR component</summary>
		public int BPR_NumFactors { get { return rated_component.NumFactors; } set { rated_component.NumFactors = value; } }
		/// <summary>Use the first item latent factor as a bias term if set to true (BPR component)</summary>
		public bool BPR_ItemBias { get { return rated_component.ItemBias; } set { rated_component.ItemBias = value; }	}

		/// <summary>Learning rate alpha</summary>
		public double BPR_LearnRate { get {	return rated_component.LearnRate; } set { rated_component.LearnRate = value; } }

		/// <summary>Regularization parameter for positive item factors (BPR component)</summary>
		public double BPR_RegI { get { return rated_component.RegI; } set { rated_component.RegI = value;	} }

		/// <summary>Regularization parameter for negative item factors (BPR component)</summary>
		public double BPR_RegJ { get { return rated_component.RegJ; } set { rated_component.RegJ = value; } }

		/// <summary>Regularization parameter for user factors (BPR component)</summary>
		public double BPR_RegU { get { return rated_component.RegU; } set { rated_component.RegU = value; } }

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

			return string.Format(ni, "Track2CompositeMatrixFactorization bpr_num_factors={0} bpr_item_bias={1} bpr_reg_u={2} bpr_reg_i={3} bpr_reg_j={4} num_iter={5} bpr_learn_rate={6}",
								 BPR_NumFactors, BPR_ItemBias, BPR_RegU, BPR_RegI, BPR_RegJ, NumIter, BPR_LearnRate);

		}
	}
}

