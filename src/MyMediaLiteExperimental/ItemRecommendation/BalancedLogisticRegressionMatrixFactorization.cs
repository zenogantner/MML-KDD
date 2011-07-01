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
using MyMediaLite.DataType;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>Matrix factorization model optimized for balanced logistic regression.</summary>
	public class BalancedLogisticRegressionMatrixFactorization : BPRMF_KDD
	{
		/// <inheritdoc/>
		protected override void UpdateFactors(int u, int i, int j, bool update_u, bool update_i, bool update_j)
		{
			double i_gradient = 1 - Predict(u, i);
			double j_gradient = -Predict(u, j);

			// adjust bias terms
			if (update_i)
			{
				double bias_update = i_gradient - BiasReg * item_bias[i];
				item_bias[i] += learn_rate * bias_update;
			}

			if (update_j)
			{
				double bias_update = j_gradient - BiasReg * item_bias[j];
				item_bias[j] += learn_rate * bias_update;
			}

			// adjust factors
			for (int f = 0; f < num_factors; f++)
			{
				double w_uf = user_factors[u, f];
				double h_if = item_factors[i, f];
				double h_jf = item_factors[j, f];

				if (update_u)
				{
					double uif_update = h_if * i_gradient - reg_u * w_uf;
					user_factors[u, f] = w_uf + learn_rate * uif_update;

					double ujf_update = h_jf * j_gradient - reg_u * w_uf;
					user_factors[u, f] = w_uf + learn_rate * ujf_update;
				}

				if (update_i)
				{
					double if_update = w_uf * i_gradient - reg_i * h_if;
					item_factors[i, f] = h_if + learn_rate * if_update;
				}

				if (update_j)
				{
					double jf_update = -w_uf  * j_gradient - reg_j * h_jf;
					item_factors[j, f] = h_jf + learn_rate * jf_update;
				}
			}
		}

		/// <inheritdoc/>
		public override double ComputeLoss()
		{
			double log_likelihood = 0;

			var u_counter = new int[MaxUserID + 1];
			var i_counter = new int[MaxItemID + 1];
			var j_counter = new int[MaxItemID + 1];

			{
				int u, i, j;

				for (int x = 0; x <= MaxUserID; x++) // doing this |U| times is rather arbitrary
				{
					SampleTriple(out u, out i, out j);
					log_likelihood += Math.Log(Predict(u, i));
					log_likelihood += Math.Log(1 - Predict(u, j));

					u_counter[u]++;
					i_counter[i]++;
					j_counter[j]++;
				}
			}

			double complexity = 0;
			for (int u = 0; u <= MaxUserID; u++)
				complexity += u_counter[u] * RegU * Math.Pow(VectorUtils.EuclideanNorm(user_factors.GetRow(u)), 2);
			for (int i = 0; i <= MaxItemID; i++)
			{
				complexity += i_counter[i] * RegI * Math.Pow(VectorUtils.EuclideanNorm(item_factors.GetRow(i)), 2);
				complexity += i_counter[i] * BiasReg * Math.Pow(item_bias[i], 2);
			}
			for (int j = 0; j <= MaxItemID; j++)
			{
				complexity += j_counter[j] * RegJ * Math.Pow(VectorUtils.EuclideanNorm(item_factors.GetRow(j)), 2);
				complexity += j_counter[j] * BiasReg * Math.Pow(item_bias[j], 2);
			}

			return -log_likelihood + 0.5 * complexity;
		}

		/// <inheritdoc/>
		public override double Predict(int user_id, int item_id)
		{
			double score = item_bias[item_id] + MatrixUtils.RowScalarProduct(user_factors, user_id, item_factors, item_id);
			return 1 / (1 + Math.Exp(-score));
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "BalancedLogisticRegressionMatrixFactorzation num_factors={0} bias_reg={1} reg_u={2} reg_i={3} reg_j={4} num_iter={5} bold_driver={6} learn_rate={7} init_mean={8} init_stdev={8}",
								 num_factors, BiasReg, reg_u, reg_i, reg_j, NumIter, BoldDriver, learn_rate, InitMean, InitStdev);
		}
	}
}