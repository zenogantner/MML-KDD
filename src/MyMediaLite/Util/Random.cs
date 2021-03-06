// Copyright (C) 2010 Steffen Rendle, Zeno Gantner
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

namespace MyMediaLite.Util
{
	/// <summary>Draws random values from a normal distibuted using a simple rejection method</summary>
	public class Random : System.Random
	{
		private static Random instance = null;

		/// <summary>Default constructor</summary>
		public Random() : base() { }

		/// <summary>Creates a Random object initialized with a seed</summary>
		/// <param name="seed">An integer for initializing the random number generator</param>
		public Random(int seed) : base(seed) { }

		/// <summary>Initializes the instance with a given random seed</summary>
		/// <param name="seed">a seed value</param>
		public static void InitInstance(int seed)
		{
			Console.Error.WriteLine("Set random seed to {0}.", seed);
			instance = new Random(seed);
		}

		/// <summary>Gets the instance. If it does not exist yet, it will be created.</summary>
		/// <returns>the singleton instance</returns>
		public static Random GetInstance()
		{
			if (instance == null)
				instance = new Random();
			return instance;
		}

		private double sqrt_e_div_2_pi = Math.Sqrt(Math.E / (2 * Math.PI));

		/// <summary>Nexts the exp</summary>
		/// <param name="lambda"></param>
		/// <returns></returns>
		public double NextExp(double lambda)
		{
			double u = this.NextDouble();
			return -(1 / lambda) * Math.Log(1 - u, Math.E);
		}

		/// <summary>Get the next number from the standard normal distribution</summary>
		/// <returns>a random number drawn from the standard normal distribution</returns>
		public double NextNormal()
		{
			double y;
			double x;
			do
			{
				double u = this.NextDouble();
				x = this.NextExp(1);
				y = 2 * u * sqrt_e_div_2_pi * Math.Exp(-x);
			} while ( y < (2 / (2 * Math.PI)) * Math.Exp(-0.5 * x * x));
			if (this.NextDouble() < 0.5)
				return x;
			else
				return -x;
		}

		/// <summary>Draw the next number from a normal distribution</summary>
		/// <param name="mean">mean of the Gaussian</param>
		/// <param name="stdev">standard deviation of the Gaussian</param>
		/// <returns>a random number drawn from a normal distribution with the given mean and standard deviation</returns>
		public double NextNormal(double mean, double stdev)
		{
			return mean + stdev * NextNormal();
		}
	}
}
