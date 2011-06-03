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
using System.Linq;
using MyMediaLite.DataType;
using MyMediaLite.Util;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>BPRMF with frequency-adjusted sampling, prototype for KDD Cup 2011</summary>
	public class BPRMF_KDD : BPRMF
	{
		// TODO offer this data structure from Feedback
		/// <summary>array of user IDs of positive user-item pairs</summary>
		protected int[] users;
		/// <summary>array of item IDs of positive user-item pairs</summary>
		protected int[] items;

		// TODO move up in hierarchy
		/// <summary>Modification factor for the learning rate of users to weight them higher or lower than items</summary>
		public double UserLearnRateModifier { get; set; }

		/// <summary>If true, sample users uniformly</summary>
		//public bool UniformUserSampling { get; set; }

		/// <summary>Weight for test users</summary>
		public double TestUserWeight { get; set; }

		/// <summary>Use bold driver heuristics for learning rate adaption</summary>
		/// <remarks>
		/// See
		/// Rainer Gemulla, Peter J. Haas, Erik Nijkamp, Yannis Sismanis:
		/// Large-Scale Matrix Factorization with Distributed Stochastic Gradient Descent
		/// 2011
		/// </remarks>
		public bool BoldDriver { set; get; }

		/// <summary>Loss for the last iteration, used by bold driver heuristics</summary>
		double last_loss = double.NegativeInfinity;

		/// <summary>array of user components of triples to use for approximate loss computation</summary>
		int[] loss_sample_u;
		/// <summary>array of user components of triples to use for approximate loss computation</summary>
		int[] loss_sample_i;
		/// <summary>array of user components of triples to use for approximate loss computation</summary>
		int[] loss_sample_j;

		/// <summary>Default constructor</summary>
		public BPRMF_KDD()
		{
			UserLearnRateModifier = 1;
			//UniformUserSampling = false;
		}

		/// <inheritdoc/>
		public override void Train()
		{
			// prepare helper data structures for training
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

			// suppress using user_neg_items in BPRMF
			FastSamplingMemoryLimit = 0;

			InitModel();

			if (BoldDriver)
			{
				int num_sample_triples = (int) Math.Sqrt(Feedback.MaxUserID) * 100; // TODO make configurable
				Console.Error.WriteLine("loss_num_sample_triples={0}", num_sample_triples);

				loss_sample_u = new int[num_sample_triples];
				loss_sample_i = new int[num_sample_triples];
				loss_sample_j = new int[num_sample_triples];

				int u, i, j;
				for (int c = 0; c < num_sample_triples; c++)
				{
					SampleTriple(out u, out i, out j);
					loss_sample_u[c] = u;
					loss_sample_i[c] = i;
					loss_sample_j[c] = j;
				}


				last_loss = ComputeLoss();
			}

			for (int i = 0; i < NumIter; i++)
				Iterate();
		}

		/// <inheritdoc/>
		public override void Iterate()
		{
			base.Iterate();

			if (BoldDriver)
			{
				double loss = ComputeLoss();

				if (loss > last_loss)
					LearnRate *= 0.5;
				else if (loss < last_loss)
					LearnRate *= 1.1;

				last_loss = loss;

				var ni = new NumberFormatInfo();
				ni.NumberDecimalDigits = '.';
				Console.Error.WriteLine(string.Format(ni, "loss {0} learn_rate {1} ", loss, LearnRate));
			}
		}

		/// <inheritdoc/>
		protected override void SampleTriple(out int u, out int i, out int j)
		{
			{
				// sample user from positive user-item pairs
				int index = random.Next(0, items.Length - 1);
				u = users[index];
				i = items[index];
			}

			// sample negative item
			do
				j = items[random.Next(0, items.Length - 1)];
			while (Feedback.UserMatrix[u, j]);
		}

		/// <summary>Compute approximate loss</summary>
		/// <returns>the approximate loss</returns>
		public virtual double ComputeLoss()
		{
			double ranking_loss = 0;
			for (int c = 0; c < loss_sample_u.Length; c++)
			{
				double x_uij = Predict(loss_sample_u[c], loss_sample_i[c]) - Predict(loss_sample_u[c], loss_sample_j[c]);
				ranking_loss += 1 / (1 + Math.Exp(x_uij));
			}

			double complexity = 0;
			for (int c = 0; c < loss_sample_u.Length; c++)
			{
				complexity += RegU * Math.Pow(VectorUtils.EuclideanNorm(user_factors.GetRow(loss_sample_u[c])), 2);
				complexity += RegI * Math.Pow(VectorUtils.EuclideanNorm(item_factors.GetRow(loss_sample_i[c])), 2);
				complexity += RegJ * Math.Pow(VectorUtils.EuclideanNorm(item_factors.GetRow(loss_sample_j[c])), 2);
				complexity += BiasReg * Math.Pow(item_bias[loss_sample_i[c]], 2);
				complexity += BiasReg * Math.Pow(item_bias[loss_sample_j[c]], 2);
			}

			return ranking_loss + 0.5 * complexity;
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			return string.Format(ni, "BPRMF_KDD num_factors={0} bias_reg={1} reg_u={2} reg_i={3} reg_j={4} num_iter={5} bold_driver={6} learn_rate={7} init_mean={8} init_stdev={8}",
								 num_factors, BiasReg, reg_u, reg_i, reg_j, NumIter, BoldDriver, learn_rate, InitMean, InitStdev);
		}
	}
}

