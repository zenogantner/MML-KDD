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
	/// <summary>BPRMF with frequency-adjusted sampling, prototype for KDD Cup 2011</summary>
	public sealed class BPRMF_KDD : BPRMF
	{
		int[] users;
		int[] items;
		
		/// <inheritdoc/>
		protected override void InitModel()
		{
			FastSamplingMemoryLimit = 0;
			
			base.InitModel();
			
			//Console.Error.WriteLine("memory before adding data structure: {0}", Memory.Usage);
			
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
			
			//Console.Error.WriteLine("memory after adding data structure: {0}", Memory.Usage);
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
		public override string ToString()
		{
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			return string.Format(ni, "BPRMF_KDD num_factors={0} item_bias={1} reg_u={2} reg_i={3} reg_j={4} num_iter={5} learn_rate={6} init_mean={7} init_stdev={8}",
								 num_factors, item_bias, reg_u, reg_i, reg_j, NumIter, learn_rate, InitMean, InitStdev);
		}		
	}
}

