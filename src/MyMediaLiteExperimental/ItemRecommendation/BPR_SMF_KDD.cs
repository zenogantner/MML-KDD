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
using MyMediaLite.Util;

namespace MyMediaLite.ItemRecommendation
{
	/// <summary>BPRMF with frequency-adjusted sampling and shared factors</summary>
	/// <remarks>prototype for KDD Cup 2011</remarks>
	public sealed class BPR_SMF_KDD : BPRMF, IKDDCupRecommender
	{
		int[] users;
		int[] items;
		
		Matrix<double> user_shared_artist_factors;
		Matrix<double> user_shared_album_factors;
		//Matrix<double> user_shared_genre_factors;

		Matrix<double> item_shared_artist_factors;
		Matrix<double> item_shared_album_factors;
		//Matrix<double> item_shared_genre_factors;		
		
		/// <summary>Number of shared factors for common artist</summary>
		public int NumSharedArtistFactors { get; set; }
		/// <summary>Number of shared factors for common records</summary>
		public int NumSharedAlbumFactors { get; set; }
		/// <summary>Number of shared factors for common genres</summary>
		public int NumSharedGenreFactors { get; set; }

		/// <inheritdoc/>
		public KDDCupItems ItemInfo { get; set; }

		/// <summary>Default constructor</summary>
		public BPR_SMF_KDD()
		{
			NumSharedArtistFactors = 2;
			NumSharedAlbumFactors  = 2;
			NumSharedGenreFactors  = 0;
		}		
				
		/// <inheritdoc/>
		protected override void InitModel()
		{
			FastSamplingMemoryLimit = 0;
			
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
		protected override void SampleTriple(out int u, out int i, out int j)
		{
			int random_index = random.Next(0, users.Length - 1);
			u = users[random_index];
			i = items[random_index];
			
			do
				j = items[random.Next(0, items.Length - 1)];
			while (Feedback.UserMatrix[u, j]);
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

			double score = 0;
			for (int f = 0; f < num_factors; f++)
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
						
			return score;
		}		
		
		// TODO take update_x into account
		/// <inheritdoc/>
		protected override void UpdateFactors(int u, int i, int j, bool update_u, bool update_i, bool update_j)
		{
			double x_uij = Predict(u, i) - Predict(u, j);

			// TODO update factors
			
			for (int f = 0; f < num_factors; f++)
			{
				double w_uf = user_factors[u, f];
				double h_if = item_factors[i, f];
				double h_jf = item_factors[j, f];

				// update user factors
				double uf_update = (h_if - h_jf) / (1 + Math.Exp(x_uij)) - reg_u * w_uf;
				user_factors[u, f] = w_uf + learn_rate * uf_update;

				// update factors of positive item
				double if_update = w_uf / (1 + Math.Exp(x_uij)) - reg_i * h_if;
				item_factors[i, f] = h_if + learn_rate * if_update;

				// update factors of negative item
				double jf_update = -w_uf / (1 + Math.Exp(x_uij)) - reg_j * h_jf;
				item_factors[j, f] = h_jf + learn_rate * jf_update;				
			}

			UpdateSharedFactors(u, ItemInfo.GetArtist(i), ItemInfo.GetArtist(j), NumSharedArtistFactors, user_shared_artist_factors, item_shared_artist_factors, 1 + Math.Exp(x_uij));
			UpdateSharedFactors(u, ItemInfo.GetAlbum(i),  ItemInfo.GetAlbum(j),  NumSharedAlbumFactors,  user_shared_album_factors,  item_shared_album_factors,  1 + Math.Exp(x_uij));
		}		

		void UpdateSharedFactors(int u, int category_i, int category_j, int num_factors, Matrix<double> user_shared_factors, Matrix<double> item_shared_factors, double common_gradient)
		{
			// adjust shared latent factors for artists
			for (int f = 0; f < num_factors; f++) // TODO specific regularization, or maybe a constant factor?
			{
				double w_uf = user_shared_factors[u, f];
				double h_if = category_i != -1 ? item_shared_factors[category_i, f] : 0;
				double h_jf = category_j != -1 ? item_shared_factors[category_j, f] : 0;
				
				// user factors
				double uf_update = (h_if - h_jf) / common_gradient - reg_u * w_uf;
				user_shared_factors[u, f] = w_uf + learn_rate * uf_update;
				
				// positive item factors
				if (category_i != -1)
				{
					double if_update = w_uf / common_gradient - reg_i * h_if;
					item_shared_factors[category_i, f] = h_if + learn_rate * if_update;
				}
				
				// negative item factors
				if (category_j != -1)
				{
					double jf_update = w_uf / common_gradient - reg_j * h_jf;
					item_shared_factors[category_j, f] = h_jf + learn_rate * jf_update;
				}				
			}			
		}	                        
		
		/// <inheritdoc/>
		public override string ToString()
		{
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			// TODO
			return string.Format(ni, "BPR_SMF_KDD num_factors={0} num_shared_artist_factors={1} num_shared_album_factors={2} num_shared_genre_factors={3} item_bias={4} reg_u={5} reg_i={6} reg_j={7} num_iter={8} learn_rate={9} init_mean={10} init_stdev={11}",
								 num_factors, NumSharedArtistFactors, NumSharedAlbumFactors, NumSharedGenreFactors, item_bias, reg_u, reg_i, reg_j, NumIter, learn_rate, InitMean, InitStdev);
		}		
	}
}

