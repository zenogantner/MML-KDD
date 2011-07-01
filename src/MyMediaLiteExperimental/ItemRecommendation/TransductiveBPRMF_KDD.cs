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
using System.Globalization;
using MyMediaLite.Util;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>Transductive BPRMF with frequency-adjusted sampling, prototype for KDD Cup 2011</summary>
	public class TransductiveBPRMF_KDD : TransductiveBPRMF
	{
		// TODO offer this data structure from Feedback
		/// <summary>array of user IDs of positive user-item pairs</summary>
		protected int[] users;
		/// <summary>array of item IDs of positive user-item pairs</summary>
		protected int[] items;

		// TODO move up in hierarchy
		/// <summary>Modification factor for the learning rate of users to weight them higher or lower than items</summary>
		public double UserLearnRateModifier { get; set; }

		/// <summary>Weight for test users</summary>
		public double TestUserWeight { get; set; }

		/// <summary>Default constructor</summary>
		public TransductiveBPRMF_KDD()
		{
			UserLearnRateModifier = 1;
		}

		/// <inheritdoc/>
		protected override void InitModel()
		{
			base.InitModel();

			users = new int[Feedback.Count];
			items = new int[Feedback.Count];

			int index = 0;
			foreach (int user_id in Feedback.UserMatrix.NonEmptyRowIDs)
				foreach (int item_id in Feedback.UserMatrix[user_id])
				{
					users[index] = user_id;
					items[index] = item_id;

					index++;
				}
		}

		/// <inheritdoc/>
		public override void Train()
		{
			// prepare helper data structures for training
			user_pos_items = new List<IList<int>>();
			for (int u = 0; u < MaxUserID + 1; u++)
				user_pos_items.Add(new List<int>(Feedback.UserMatrix[u]));

			// suppress using user_neg_items in BPRMF
			FastSamplingMemoryLimit = 0;

			base.Train();
		}

		/// <inheritdoc/>
		protected override void SampleTriple(out int u, out int i, out int j)
		{
			// sample user from positive user-item pairs
			int index = random.Next(0, items.Length - 1);
			u = users[index];
			i = items[index];

			// sample negative item
			do
				j = items[random.Next(0, items.Length - 1)];
			while (Feedback.UserMatrix[u, j]);
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture,
			                     "SemiSupervisedBPRMF_KDD num_factors={0} bias_reg={1} reg_u={2} reg_i={3} reg_j={4} non_test_modifier={5} num_iter={6} learn_rate={7} init_mean={8} init_stdev={9}",
								 num_factors, BiasReg, reg_u, reg_i, reg_j, NonTestModifier, NumIter, learn_rate, InitMean, InitStdev);
		}
	}
}

