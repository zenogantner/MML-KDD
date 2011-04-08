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
using System.IO;

class MegeTrack2
{
	/// <summary>Parameters: num_files weight_1 .. weight_n file_1 .. file_n output_file</summary>
	/// <param name="args">the command-line arguments</param>
	public static void Main(string[] args)
	{
		int NUM_CANDIDATES = 6;

		var ni = new NumberFormatInfo();
		ni.NumberDecimalDigits = '.';

		double weight_sum = 0;

		// parse command-line parameters
		var weights = new List<double>();
		var files   = new List<string>();
		for (int i = 0; i < args.Length - 1; i++)
		{
			string[] tokens = args[i].Split(':');
						
			if (tokens.Length == 2)
				weights.Add(double.Parse(tokens[1]));
			else
				weights.Add(1);
			weight_sum += weights.Last();
			
			files.Add(tokens[0]);
		}
		string output_filename = args.Last();
		
		// open files
		var readers = new BinaryReader[files.Count];
		for (int i = 0; i < files.Count; i++)
			readers[i] = new BinaryReader(new FileStream(files[i], FileMode.Open, FileAccess.Read));

		using (var writer = new StreamWriter(output_filename))
			try
			{
				// read, merge, and write
				var votes = new char[files.Count][];
				while ( (votes[0] = readers[0].ReadChars(NUM_CANDIDATES)).Length > 0 )
				{
					for (int i = 1; i < readers.Length; i++)
						votes[i] = readers[i].ReadChars(NUM_CANDIDATES);

					var weighted_votes = new double[NUM_CANDIDATES];
					for (int i = 0; i < readers.Length; i++)
						for (int j = 0; j < NUM_CANDIDATES; j++)
							if (votes[i][j] == '1')
								weighted_votes[j] += weights[i];

					var positions = new List<int>(new int[] { 0, 1, 2, 3, 4, 5 });
					positions.Sort(delegate(int pos1, int pos2) { return weighted_votes[pos2].CompareTo(weighted_votes[pos1]); } );

					for (int i = 0; i < NUM_CANDIDATES; i++)
						if (positions.IndexOf(i) < 3)
							writer.Write("1");
						else
							writer.Write("0");
				}
			}
			catch (EndOfStreamException)
			{
				// do nothing
			}
	}
}