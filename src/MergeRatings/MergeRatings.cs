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
using System.Globalization;
using System.IO;

class MergeRatings
{
	/// <summary>Parameters: num_files weight_1 .. weight_n file_1 .. file_n output_file</summary>
	/// <param name="args">the command-line arguments</param>
	public static void Main(string[] args)
	{
		var ni = new NumberFormatInfo();
		ni.NumberDecimalDigits = '.';
		
		int chunk_size = 1000;
		
		// TODO check args.Length
		
		double weight_sum = 0;
		
		// parse command-line parameters
		int num_files = int.Parse(args[0]);
		var weights = new double[num_files];
		var files   = new string[num_files];
		for (int i = 0; i < num_files; i++)
		{
			weights[i] = double.Parse(args[i + 1]);
			weight_sum += weights[i];
			files[i]   = args[i + num_files + 1];
		}
		string output_filename = args[2 * num_files + 1];
		
		// open files
		var readers = new BinaryReader[num_files];
		for (int i = 0; i < num_files; i++)
			readers[i] = new BinaryReader(new FileStream(files[i], FileMode.Open, FileAccess.Read));
		
		// creater writer
		var writer = new BinaryWriter(new FileStream(output_filename, FileMode.Create));
		byte[][] ratings = new byte[num_files][];
		while ( (ratings[0] = readers[0].ReadBytes(chunk_size)).Length > 0)
		{
			for (int i = 1; i < num_files; i++)
				ratings[i] = readers[i].ReadBytes(chunk_size);
			
			
			for (int i = 0; i < ratings[0].Length; i++)
			{
				double weighted_sum = 0;
				for (int j = 0; j < num_files; j++)
					weighted_sum += weights[j] * ratings[j][i];
				byte weighted_average = (byte) ((weighted_sum / weight_sum) + 0.5);
				writer.Write(weighted_average);
			}
		}
	}
}