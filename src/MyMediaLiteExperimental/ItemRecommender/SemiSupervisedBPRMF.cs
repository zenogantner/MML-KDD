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
using System.Collections.Generic;

namespace MyMediaLite.ItemRecommendation
{
	public class SemiSupervisedBPRMF : BPRMF
	{
		public HashSet<int> TestUsers { get; set; }

		public HashSet<int> TestItems { get; set; }

		public double NonTestModifier { get; set; }

		protected override void UpdateFactors(int u, int i, int j, bool update_u, bool update_i, bool update_j)
		{
			double x_uij = Predict(u, i) - Predict(u, j);

			double learn_rate_u = learn_rate;
			double learn_rate_i = learn_rate;
			double learn_rate_j = learn_rate;

			if (! TestUsers.Contains(u))
				learn_rate_u *= NonTestModifier;

			if (! TestItems.Contains(i))
				learn_rate_i *= NonTestModifier;

			if (! TestItems.Contains(j))
				learn_rate_j *= NonTestModifier;

			// adjust bias terms
			if (update_i)
			{
				double bias_update = 1.0 / (1 + Math.Exp(x_uij)) - BiasReg * item_bias[i];
				item_bias[i] += learn_rate * bias_update;
			}

			if (update_j)
			{
				double bias_update = 1.0 / (1 + Math.Exp(x_uij)) - BiasReg * item_bias[j];
				item_bias[j] -= learn_rate * bias_update;
			}

			// adjust factors
			for (int f = 0; f < num_factors; f++)
			{
				double w_uf = user_factors[u, f];
				double h_if = item_factors[i, f];
				double h_jf = item_factors[j, f];

				if (update_u)
				{
					double uf_update = (h_if - h_jf) / (1 + Math.Exp(x_uij)) - reg_u * w_uf;
					user_factors[u, f] = w_uf + learn_rate_u * uf_update;
				}

				if (update_i)
				{
					double if_update = w_uf / (1 + Math.Exp(x_uij)) - reg_i * h_if;
					item_factors[i, f] = h_if + learn_rate_i * if_update;
				}

				if (update_j)
				{
					double jf_update = -w_uf / (1 + Math.Exp(x_uij)) - reg_j * h_jf;
					item_factors[j, f] = h_jf + learn_rate_j * jf_update;
				}
			}
		}

	}
}
