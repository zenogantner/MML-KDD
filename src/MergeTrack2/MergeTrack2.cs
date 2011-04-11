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

using Mono.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using MyMediaLite.Eval;
using MyMediaLite.IO.KDDCup2011;
using MyMediaLite.Util;

class MergeTrack2
{
	const int NUM_CANDIDATES = 6;
	
	/// <summary>Parameters: num_files weight_1 .. weight_n file_1 .. file_n output_file</summary>
	/// <param name="args">the command-line arguments</param>
	public static void Main(string[] args)
	{
		var ni = new NumberFormatInfo();
		ni.NumberDecimalDigits = '.';

		// parse command-line parameters
		string data_dir     = null;
		string output_file  = null;
		bool greedy_forward = false;
	   	var p = new OptionSet() {
   			{ "data-dir=",       v => data_dir = v },
			{ "output-file=",    v => output_file = v },
			{ "greedy-forward",  v => greedy_forward = v != null },
   	  	};
   		IList<string> extra_args = p.Parse(args);

		List<string> files = new List<string>();
		List<double> weights = new List<double>();
		foreach (string arg in extra_args)
		{
			string[] tokens = arg.Split(':');
						
			if (tokens.Length == 2)
				weights.Add(double.Parse(tokens[1]));
			else
				weights.Add(1);
			
			files.Add(tokens[0]);
		}
		
		if (greedy_forward)
		{
			GreedyForwardSearch(files, weights, data_dir);
		}
		else
		{
			if (data_dir != null)
				Eval(files, weights, data_dir);
			
			IList<byte> final_prediction = MergeFiles(files, weights);
			WritePredictions(final_prediction, output_file);
		}
	}
	
	static void GreedyForwardSearch(IList<string> files, IList<double> weights, string data_dir)
	{
		var candidates = Track2Items.Read(data_dir + "/mml-track2/validationCandidatesIdx2.txt");
		var hits       = Track2Items.Read(data_dir + "/mml-track2/validationHitsIdx2.txt");		

		var results = new List<double>();

		var current_files   = new List<string>();
		var current_weights = new List<double>();
		
		for (int i = 0; i < files.Count; i++)
		{
			current_files.Add(files[i]);
			current_weights.Add(weights[i]);
			
			results.Add(Eval(current_files, current_weights, candidates, hits));
			
			if (results.Last() != results.Min())
			{
				// no improvement, remove from list
				current_files.RemoveRange(current_files.Count - 1, 1);
				current_weights.RemoveRange(current_weights.Count - 1, 1);
			}
			else
			{
				Console.WriteLine("keep {0}", files[i]);
			}
		}
	}
	
	static double Eval(IList<string> files, IList<double> weights, string data_dir)
	{
		var candidates = Track2Items.Read(data_dir + "/mml-track2/validationCandidatesIdx2.txt");
		var hits       = Track2Items.Read(data_dir + "/mml-track2/validationHitsIdx2.txt");
		
		return Eval(files, weights, candidates, hits);
	}
	
	static double Eval(IList<string> files, IList<double> weights, Dictionary<int, IList<int>> candidates, Dictionary<int, IList<int>> hits)
	{
		IList<string> validation_files = new List<string>();
		foreach (string filename in files)
		{
			string[] tokens = filename.Split(new string[] { "-it-" }, StringSplitOptions.None);
			if (tokens.Length != 2)
				throw new Exception(string.Format("Could not parse filename '{0}'", filename));
			validation_files.Add(tokens[0] + "-validate-it-" + tokens[1]);
		}
		
		IList<byte> validation_predictions = MergeFiles(validation_files, weights);
		
		double result = KDDCup.EvaluateTrack2(validation_predictions, candidates, hits);
		Console.WriteLine("{0} files ERR {1} memory {2}", files.Count, result, Memory.Usage);
		return result;
	}
	
	static IList<byte> MergeFiles(IList<string> files, IList<double> weights)
	{
		// open files
		var readers = new BinaryReader[files.Count];
		for (int i = 0; i < files.Count; i++)
			readers[i] = new BinaryReader(new FileStream(files[i], FileMode.Open, FileAccess.Read));
		
		var combined_predictions = new List<byte>();
		
		try
		{
			// read and merge
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
						combined_predictions.Add(1);
					else
						combined_predictions.Add(0);
			}
		}
		catch (EndOfStreamException)
		{
			// do nothing
		}
		
		return combined_predictions;
	}
	
	static void WritePredictions(IList<byte> predictions, string output_file)
	{
		using (var writer = new StreamWriter(output_file))
			foreach (byte p in predictions)
				writer.Write(p);
	}	
}