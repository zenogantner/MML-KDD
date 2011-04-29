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

namespace MyMediaLite.RatingPrediction
{
	/// <summary>Biased matrix factorization with bold driver heuristic</summary>
	/// <remarks>
	/// See
	/// Rainer Gemulla, Peter J. Haas, Erik Nijkamp, Yannis Sismanis:
	/// Large-Scale Matrix Factorization with Distributed Stochastic Gradient Descent
	/// </remarks>
	public class BoldDriverBiasedMatrixFactorization : BiasedMatrixFactorization
	{
		double last_loss = double.NegativeInfinity;
		
		/// <summary>Default constructor</summary>
		public BoldDriverBiasedMatrixFactorization()
		{
			LearnRate = 0.1;
		}
		
		/// <inheritdoc/>		
		protected override void InitModel()
		{
			base.InitModel();
			last_loss = ComputeLoss();
		}
		
		/// <inheritdoc/>		
		public override void Iterate()
		{
			base.Iterate();
			
			double loss = ComputeLoss();
					
			if (loss > last_loss)
				LearnRate *= 0.5;
			else if (loss < last_loss)
				LearnRate *= 1.05;
			
			last_loss = loss;
			
			Console.Error.Write("loss {0} learn_rate {1} ", loss, LearnRate);			
		}
		
		/// <inheritdoc/>
		public override string ToString()
		{
			var ni = new NumberFormatInfo();
			ni.NumberDecimalDigits = '.';

			return string.Format(ni,
								 "BoldDriverBiasedMatrixFactorization num_factors={0} bias_reg={1} reg_u={2} reg_i={3} learn_rate={4} num_iter={5} init_mean={6} init_stdev={7}",
								 NumFactors, BiasReg, RegU, RegI, LearnRate, NumIter, InitMean, InitStdev);
		}		
	}
}

