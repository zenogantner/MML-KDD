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
	/// <summary>Matrix factorization with explicit user and item bias that takes item relations into account</summary>
	public class SharedFactorsMatrixFactorization : BiasedMatrixFactorization, IKDDCupRecommender
	{
		Matrix<double> user_shared_artist_factors;
		Matrix<double> user_shared_album_factors;
		//Matrix<double> user_shared_genre_factors;

		Matrix<double> item_shared_artist_factors;
		Matrix<double> item_shared_album_factors;
		//Matrix<double> item_shared_genre_factors;

		//double[] artist_biases;
		//double[] album_biases;
		//double[] genre_biases;

		// TODO add specific regularization term

		/// <summary>Number of shared factors for common artist</summary>
		public int NumSharedArtistFactors { get; set; }
		/// <summary>Number of shared factors for common records</summary>
		public int NumSharedAlbumFactors { get; set; }
		/// <summary>Number of shared factors for common genres</summary>
		public int NumSharedGenreFactors { get; set; }
		/// <summary>Regularization constant for shared factors - multiple of the normal regularization constants</summary>
		public double SharedReg { get; set; }
		
		/// <inheritdoc/>
		public KDDCupItems ItemInfo { get; set; }

		/// <summary>Default constructor</summary>
		public SharedFactorsMatrixFactorization()
		{
			NumSharedArtistFactors = 2;
			NumSharedAlbumFactors  = 2;
			NumSharedGenreFactors  = 0;
			SharedReg = 2;
		}

		/// <inheritdoc/>
		protected override void InitModel()
		{
			base.InitModel();

			user_shared_artist_factors = new Matrix<double>(MaxUserID + 1, NumSharedArtistFactors);
			user_shared_album_factors  = new Matrix<double>(MaxUserID + 1, NumSharedAlbumFactors);
			//user_shared_genre_factors  = new Matrix<double>(MaxUserID + 1, NumSharedGenreFactors);

			// TODO this is a bit of a waste of memory, consider using sparse matrices ...
			item_shared_artist_factors = new Matrix<double>(MaxItemID + 1, NumSharedArtistFactors);
			item_shared_album_factors  = new Matrix<double>(MaxItemID + 1, NumSharedAlbumFactors);
			//item_shared_genre_factors  = new Matrix<double>(MaxItemID + 1, NumSharedGenreFactors);

			// init biases
			/*
			artist_biases = new double[MaxItemID + 1];
			album_biases  = new double[MaxItemID + 1];
			genre_biases  = new double[MaxItemID + 1];
			*/

			MatrixUtils.InitNormal(user_shared_artist_factors, InitMean, InitStdev);
			MatrixUtils.InitNormal(item_shared_artist_factors, InitMean, InitStdev);
			MatrixUtils.InitNormal(user_shared_album_factors, InitMean, InitStdev);
			MatrixUtils.InitNormal(item_shared_album_factors, InitMean, InitStdev);
			//MatrixUtils.InitNormal(user_shared_genre_factors, InitMean, InitStdev);
			//MatrixUtils.InitNormal(item_shared_genre_factors, InitMean, InitStdev);
		}

		/// <inheritdoc/>
		protected override void Iterate(IList<int> rating_indices, bool update_user, bool update_item)
		{
			// TODO take update_user and update_item into account

			double rating_range_size = MaxRating - MinRating;

			foreach (int index in rating_indices)
			{
				int u = ratings.Users[index];
				int i = ratings.Items[index];
				int album_id =  -1;
				int artist_id = -1;

				double score = global_bias + user_bias[u] + item_bias[i];
				for (int f = 0; f < NumFactors; f++)
					score += user_factors[u, f] * item_factors[i, f];
				
				// shared factors
				if (ItemInfo.HasArtist(i))
				{
					artist_id = ItemInfo.GetArtist(i);
					for (int f = 0; f < NumSharedArtistFactors; f++)
						score += user_shared_artist_factors[u, f] * item_shared_artist_factors[artist_id, f];
				}
				if (ItemInfo.HasAlbum(i))
				{
					album_id = ItemInfo.GetAlbum(i);
					for (int f = 0; f < NumSharedAlbumFactors; f++)
						score += user_shared_album_factors[u, f] * item_shared_album_factors[album_id, f];
				}

				double sig_dot = 1 / (1 + Math.Exp(-score));

				double p = MinRating + sig_dot * rating_range_size;
				double err = ratings[index] - p;

				double gradient_common = err * sig_dot * (1 - sig_dot) * rating_range_size;

				// adjust biases
				user_bias[u] += LearnRate * (gradient_common - BiasReg * user_bias[u]);
				item_bias[i] += LearnRate * (gradient_common - BiasReg * item_bias[i]);

				// adjust latent factors
				AdjustFactors(u, i, NumFactors, user_factors, item_factors, gradient_common, 1);

				// adjust shared latent factors
				if (ItemInfo.HasArtist(i))
					AdjustFactors(u, artist_id, NumSharedArtistFactors, user_shared_artist_factors, item_shared_artist_factors, gradient_common, SharedReg);
				if (ItemInfo.HasAlbum(i))
					AdjustFactors(u, album_id, NumSharedAlbumFactors, user_shared_album_factors, item_shared_album_factors, gradient_common, SharedReg);
				// TODO genres

			}
		}

		void AdjustFactors(int u, int i, int num_factors, Matrix<double> u_factors, Matrix<double> i_factors, double gradient_common, double reg_mod)
		{
			for (int f = 0; f < num_factors; f++)
			{
			 	double u_f = u_factors[u, f];
				double i_f = i_factors[i, f];

				double delta_u = gradient_common * i_f - RegU * reg_mod * u_f;
				MatrixUtils.Inc(u_factors, u, f, LearnRate * delta_u);

				double delta_i = gradient_common * u_f - RegI * reg_mod * i_f;
				MatrixUtils.Inc(i_factors, i, f, LearnRate * delta_i);
			}
		}

		/// <inheritdoc/>
		public override double Predict(int user_id, int item_id)
		{
			if (user_id >= user_factors.dim1 || item_id >= item_factors.dim1)
				return MinRating + ( 1 / (1 + Math.Exp(-global_bias)) ) * (MaxRating - MinRating);

			double score = global_bias + user_bias[user_id] + item_bias[item_id];

			// U*V
			for (int f = 0; f < NumFactors; f++)
				score += user_factors[user_id, f] * item_factors[item_id, f];

			// shared factors
			if (ItemInfo.HasArtist(item_id))
			{
				int artist_id = ItemInfo.GetArtist(item_id);
				for (int f = 0; f < NumSharedArtistFactors; f++)
					score += user_shared_artist_factors[user_id, f] * item_shared_artist_factors[artist_id, f];
			}
			if (ItemInfo.HasAlbum(item_id))
			{
				int album_id = ItemInfo.GetAlbum(item_id);
				for (int f = 0; f < NumSharedAlbumFactors; f++)
					score += user_shared_album_factors[user_id, f] * item_shared_album_factors[album_id, f];
			}
			// TODO genres
			// TODO biases

			return MinRating + ( 1 / (1 + Math.Exp(-score)) ) * (MaxRating - MinRating);
		}

		/// <inheritdoc/>
		public override void SaveModel(string filename)
		{
			throw new NotImplementedException();
		}

		/// <inheritdoc/>
		public override void LoadModel(string filename)
		{
			throw new NotImplementedException();
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
								 "SharedFactorsMatrixFactorization num_factors={0} num_shared_artist_factors={1} num_shared_album_factors={2} num_shared_genre_factors={3} bias_regularization={4} reg_user={5} reg_item={6} shared_reg={7} learn_rate={8} num_iter={9} init_mean={10} init_stdev={11}",
								 NumFactors, NumSharedArtistFactors, NumSharedAlbumFactors, NumSharedGenreFactors, BiasReg, RegU, RegI, SharedReg, LearnRate, NumIter, InitMean, InitStdev);
		}
	}
}
