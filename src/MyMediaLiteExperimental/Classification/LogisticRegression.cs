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
using MyMediaLite.DataType;

namespace MyMediaLite.Classification
{
	/// <summary>Regularized logistic regression trained by stochastic gradient descent</summary>
	/// <remarks>
	/// Implementation for dense feature vectors.
	/// Predictor variables are implemented transposed to their normal layout, due to being used in
	/// for the KDD Cup 2011 ensembles.
	/// </remarks>
	public class LogisticRegression
	{
		/// <summary>the predictor (input) variables</summary>
		public Matrix<double> PredictorVariables { get; set; }
		/// <summary>the target (output) variables</summary>
		public IList<byte>  TargetVariables { get; set; }
		/// <summary>number of training iterations</summary>
		public uint NumIter { get; set; }
		/// <summary>the learn rate, the step size for training updates</summary>
		public double LearnRate { get; set; }
		/// <summary>the regularization parameter, higher values lead to more shrinkage</summary>
		public double Regularization { get; set; }
		
		double bias;
		public IList<double> parameters;
		
		/// <summary>Default constructor</summary>
		public LogisticRegression()
		{
			NumIter = 10;
			LearnRate = 0.0001;
			//Regularization = 0.0001;
		}
		
		public void InitModel()
		{
			bias = 0;
			parameters = new double[PredictorVariables.NumberOfRows];
			for (int i = 0; i < parameters.Count; i++)
				parameters[i] = 1;
		}
		
		/// <summary>Predict probability for given features</summary>
		/// <param name="input">the input</param>
		/// <returns>the probability of the input belonging to class 1</returns>
		public double PredictProbability(IList<double> input)
		{
			// TODO assert features and parameters have same length
			
			double score = bias;
			for (int i = 0; i < input.Count; i++)
				score += input[i] * parameters[i];
			
			return (double) 1 / (1 + Math.Exp(-score));
		}
		
		/// <summary>Train using stochastic gradient descent</summary>
		public void Train()
		{
			InitModel();
			
			for (int i = 0; i < NumIter; i++)
				for (int j = 0; j < PredictorVariables.NumberOfColumns; j++) // TODO shuffle to have really stochastic gradient ascent
				{
					double t_minus_p = TargetVariables[j] - PredictProbability(PredictorVariables.GetColumn(j));
								
					// TODO perform bias update (not really necessary for our use case)
					for (int k = 0; k < parameters.Count; k++)
						parameters[k] += LearnRate * (t_minus_p * PredictorVariables[k, j] - Regularization * parameters[k]);
				}
		}
	}
}

