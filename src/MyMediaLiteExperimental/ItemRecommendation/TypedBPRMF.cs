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
using MyMediaLite.Data;
using MyMediaLite.DataType;
using MyMediaLite.Taxonomy;
using MyMediaLite.Util;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>BPRMF with frequency-adjusted sampling and shared factors</summary>
	/// <remarks>prototype for KDD Cup 2011</remarks>
	public sealed class TypedBPRMF : BPRMF_KDD, IKDDCupRecommender
	{
		Matrix<double> user_artist_factors;
		Matrix<double> user_album_factors;
		Matrix<double> user_genre_factors;
		Matrix<double> user_track_factors;

		Matrix<double> item_typed_factors;

		/// <summary>Number of typed factors</summary>
		public uint NumTypedFactors { get; set; }

		/// <summary>Regularization modifier for artist factors</summary>
		public double ArtistRegModifier { get; set; }

		/// <summary>Regularization modifier for album factors</summary>
		public double AlbumRegModifier { get; set; }

		/// <summary>Regularization modifier for track factors</summary>
		public double TrackRegModifier { get; set; }

		/// <summary>Regularization modifier for genre factors</summary>
		public double GenreRegModifier { get; set; }

		/// <inheritdoc/>
		public KDDCupItems ItemInfo { get; set; }

		/// <summary>Default constructor</summary>
		public TypedBPRMF()
		{
			NumTypedFactors = NumFactors;
		}

		/// <inheritdoc/>
		protected override void InitModel()
		{
			base.InitModel();

			user_artist_factors = new Matrix<double>(MaxUserID + 1, NumTypedFactors);
			user_album_factors  = new Matrix<double>(MaxUserID + 1, NumTypedFactors);
			user_genre_factors  = new Matrix<double>(MaxUserID + 1, NumTypedFactors);
			user_track_factors  = new Matrix<double>(MaxUserID + 1, NumTypedFactors);

			item_typed_factors = new Matrix<double>(MaxItemID + 1, NumTypedFactors);

			MatrixUtils.RowInitNormal(user_artist_factors, InitMean, InitStdev);
			MatrixUtils.RowInitNormal(user_album_factors, InitMean, InitStdev);
			MatrixUtils.RowInitNormal(user_genre_factors, InitMean, InitStdev);
			MatrixUtils.RowInitNormal(user_track_factors, InitMean, InitStdev);
			MatrixUtils.RowInitNormal(item_typed_factors, InitMean, InitStdev);
		}

		/// <inheritdoc/>
		public override double Predict(int user_id, int item_id)
		{
			if ((user_id < 0) || (user_id >= user_factors.dim1))
			{
				Console.Error.WriteLine("user is unknown: " + user_id);
				return 0;
			}
			if ((item_id < 0) || (item_id >= item_factors.dim1))
			{
				Console.Error.WriteLine("item is unknown: " + item_id);
				return 0;
			}

			double score = item_bias[item_id] + MatrixUtils.RowScalarProduct(user_factors, user_id, item_factors, item_id);
			score += MatrixUtils.RowScalarProduct(GetUserTypedFactors(item_id), user_id, item_typed_factors, item_id);

			return score;
		}

		Matrix<double> GetUserTypedFactors(int item_id)
		{
			switch (ItemInfo.GetType(item_id))
			{
				case KDDCupItemType.Artist :
					return user_artist_factors;
				case KDDCupItemType.Album :
					return user_album_factors;
				case KDDCupItemType.Track :
					return user_track_factors;
				case KDDCupItemType.Genre :
					return user_genre_factors;
				default :
					throw new Exception("Unknown type for item " + item_id);
			}
		}

		double GetTypedFactorsRegModifier(int item_id)
		{
			switch (ItemInfo.GetType(item_id))
			{
				case KDDCupItemType.Artist :
					return ArtistRegModifier;
				case KDDCupItemType.Album :
					return AlbumRegModifier;
				case KDDCupItemType.Track :
					return TrackRegModifier;
				case KDDCupItemType.Genre :
					return GenreRegModifier;
				default :
					throw new Exception("Unknown type for item " + item_id);
			}
		}

		// TODO take update_x into account
		/// <inheritdoc/>
		protected override void UpdateFactors(int u, int i, int j, bool update_u, bool update_i, bool update_j)
		{
			double x_uij = Predict(u, i) - Predict(u, j);

			double one_over_one_plus_ex = 1 / (1 + Math.Exp(x_uij));

			// adjust bias terms
			if (update_i)
			{
				double bias_update = one_over_one_plus_ex - BiasReg * item_bias[i];
				item_bias[i] += learn_rate * bias_update;
			}

			if (update_j)
			{
				double bias_update = -one_over_one_plus_ex - BiasReg * item_bias[j];
				item_bias[j] += learn_rate * bias_update;
			}

			// adjust normal factors
			for (int f = 0; f < num_factors; f++)
			{
				double w_uf = user_factors[u, f];
				double h_if = item_factors[i, f];
				double h_jf = item_factors[j, f];

				// update user factors
				double uf_update = (h_if - h_jf) * one_over_one_plus_ex - reg_u * w_uf;
				user_factors[u, f] = w_uf + learn_rate * uf_update;

				// update factors of positive item
				double if_update = w_uf * one_over_one_plus_ex - reg_i * h_if;
				item_factors[i, f] = h_if + learn_rate * if_update;

				// update factors of negative item
				double jf_update = -w_uf * one_over_one_plus_ex - reg_j * h_jf;
				item_factors[j, f] = h_jf + learn_rate * jf_update;
			}

			// adjust typed factors for i
			Matrix<double> ui_factors = GetUserTypedFactors(i);
			double typed_reg_mod_i = GetTypedFactorsRegModifier(i);
			for (int f = 0; f < NumTypedFactors; f++)
			{
				double w_uf = ui_factors[u, f];
				double h_if = item_typed_factors[i, f];

				// update user factors
				double uf_update = h_if * one_over_one_plus_ex - typed_reg_mod_i * reg_u * w_uf;
				ui_factors[u, f] = w_uf + learn_rate * uf_update;

				// update factors of positive item
				double if_update = w_uf * one_over_one_plus_ex - typed_reg_mod_i * reg_i * h_if;
				item_typed_factors[i, f] = h_if + learn_rate * if_update;
			}

			// adjust typed factors for j
			Matrix<double> uj_factors = GetUserTypedFactors(j);
			double typed_reg_mod_j = GetTypedFactorsRegModifier(j);
			for (int f = 0; f < NumTypedFactors; f++)
			{
				double w_uf = uj_factors[u, f];
				double h_jf = item_typed_factors[j, f];

				// update user factors
				double uf_update = -h_jf * one_over_one_plus_ex - typed_reg_mod_j * reg_u * w_uf;
				uj_factors[u, f] = w_uf + learn_rate * uf_update;

				// update factors of negative item
				double jf_update = -w_uf * one_over_one_plus_ex - typed_reg_mod_j * reg_j * h_jf;
				item_typed_factors[j, f] = h_jf + learn_rate * jf_update;
			}
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			return string.Format(ni, "BPR_SMF_KDD num_factors={0} num_typed_factors={1} bias_reg={2} reg_u={3} reg_i={4} reg_j={5} album_reg_modifier={6} artist_reg_modifier={7} genre_reg_modifier={8} track_reg_modifier={9} num_iter={6} learn_rate={10} init_mean={11} init_stdev={12}",
								 num_factors, NumTypedFactors, BiasReg, reg_u, reg_i, reg_j, AlbumRegModifier, ArtistRegModifier, GenreRegModifier, TrackRegModifier, NumIter, learn_rate, InitMean, InitStdev);
		}
	}
}

