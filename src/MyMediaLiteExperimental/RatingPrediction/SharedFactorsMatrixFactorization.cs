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
using System.IO;
using MyMediaLite.Data;
using MyMediaLite.DataType;
using MyMediaLite.Taxonomy;
using MyMediaLite.Util;

namespace MyMediaLite.RatingPrediction
{
	/// <summary>Matrix factorization engine with explicit user and item bias that takes item relations into account</summary>
	public class SharedFactorsMatrixFactorization : BiasedMatrixFactorization, IKDDCupRecommender
	{
		/// <inheritdoc/>
		public KDDCupItems ItemInfo { get; set; }

		/// <inheritdoc/>
		protected override void InitModel()
		{
			base.InitModel();
		}

		/// <inheritdoc/>
		protected override void Iterate(IList<int> rating_indices, bool update_user, bool update_item)
		{
			double rating_range_size = MaxRating - MinRating;

			foreach (int index in rating_indices)
			{
				int u = ratings.Users[index];
				int i = ratings.Items[index];

				double dot_product = global_bias + user_bias[u] + item_bias[i];
				for (int f = 0; f < num_factors; f++)
					dot_product += user_factors[u, f] * item_factors[i, f];
				double sig_dot = 1 / (1 + Math.Exp(-dot_product));

				double p = MinRating + sig_dot * rating_range_size;
				double err = ratings[index] - p;

				double gradient_common = err * sig_dot * (1 - sig_dot) * rating_range_size;

				// Adjust biases
				if (update_user)
					user_bias[u] += learn_rate * (gradient_common - bias_regularization * user_bias[u]);
				if (update_item)
					item_bias[i] += learn_rate * (gradient_common - bias_regularization * item_bias[i]);

				// Adjust latent factors
				for (int f = 0; f < num_factors; f++)
				{
				 	double u_f = user_factors[u, f];
					double i_f = item_factors[i, f];

					if (update_user)
					{
						double delta_u = gradient_common * i_f - regularization * u_f;
						MatrixUtils.Inc(user_factors, u, f, learn_rate * delta_u);
						// this is faster (190 vs. 260 seconds per iteration on Netflix w/ k=30) than
						//    user_factors[u, f] += learn_rate * delta_u;
					}
					if (update_item)
					{
						double delta_i = gradient_common * u_f - regularization * i_f;
						MatrixUtils.Inc(item_factors, i, f, learn_rate * delta_i);
						// item_factors[i, f] += learn_rate * delta_i;
					}
				}
			}
		}

		/// <inheritdoc/>
		public override double Predict(int user_id, int item_id)
		{
			if (user_id >= user_factors.dim1 || item_id >= item_factors.dim1)
				return MinRating + ( 1 / (1 + Math.Exp(-global_bias)) ) * (MaxRating - MinRating);

			double score = global_bias + user_bias[user_id] + item_bias[item_id];

			// U*V
			for (int f = 0; f < num_factors; f++)
				score += user_factors[user_id, f] * item_factors[item_id, f];

			return MinRating + ( 1 / (1 + Math.Exp(-score)) ) * (MaxRating - MinRating);
		}

		/// <inheritdoc/>
		public override void SaveModel(string filename)
		{
			throw new NotImplementedException();
			
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			using ( StreamWriter writer = Recommender.GetWriter(filename, this.GetType()) )
			{
				writer.WriteLine(global_bias.ToString(ni));
				VectorUtils.WriteVector(writer, user_bias);
				IMatrixUtils.WriteMatrix(writer, user_factors);
				VectorUtils.WriteVector(writer, item_bias);
				IMatrixUtils.WriteMatrix(writer, item_factors);
			}
		}

		/// <inheritdoc/>
		public override void LoadModel(string filename)
		{
			throw new NotImplementedException();
			
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			using ( StreamReader reader = Recommender.GetReader(filename, this.GetType()) )
			{
				var bias = double.Parse(reader.ReadLine(), ni);

				ICollection<double> user_bias = VectorUtils.ReadVector(reader);
				var user_factors = (Matrix<double>) IMatrixUtils.ReadMatrix(reader, new Matrix<double>(0, 0));
				ICollection<double> item_bias = VectorUtils.ReadVector(reader);
				var item_factors = (Matrix<double>) IMatrixUtils.ReadMatrix(reader, new Matrix<double>(0, 0));

				if (user_factors.dim2 != item_factors.dim2)
					throw new IOException(
								  string.Format(
									  "Number of user and item factors must match: {0} != {1}",
									  user_factors.dim2, item_factors.dim2));
				if (user_bias.Count != user_factors.dim1)
					throw new IOException(
								  string.Format(
									  "Number of users must be the same for biases and factors: {0} != {1}",
									  user_bias.Count, user_factors.dim1));
				if (item_bias.Count != item_factors.dim1)
					throw new IOException(
								  string.Format(
									  "Number of items must be the same for biases and factors: {0} != {1}",
									  item_bias.Count, item_factors.dim1));

				this.MaxUserID = user_factors.dim1 - 1;
				this.MaxItemID = item_factors.dim1 - 1;

				// assign new model
				this.global_bias = bias;
				if (this.num_factors != user_factors.dim2)
				{
					Console.Error.WriteLine("Set num_factors to {0}", user_factors.dim1);
					this.num_factors = user_factors.dim2;
				}
				this.user_factors = user_factors;
				this.item_factors = item_factors;
				this.user_bias = new double[user_factors.dim1];
				user_bias.CopyTo(this.user_bias, 0);
				this.item_bias = new double[item_factors.dim1];
				item_bias.CopyTo(this.item_bias, 0);
			}
		}

		/// <inheritdoc/>
		public override void AddUser(int user_id)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void AddItem(int item_id)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void RetrainUser(int user_id)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void RetrainItem(int item_id)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void RemoveUser(int user_id)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void RemoveItem(int item_id)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			return string.Format(ni,
								 "SharedFactorsMatrixFactorization num_factors={0} bias_regularization={1} regularization={2} learn_rate={3} num_iter={4} init_mean={5} init_stdev={6}",
								 NumFactors, BiasRegularization, Regularization, LearnRate, NumIter, InitMean, InitStdev);
		}
	}
}
