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
using System.IO.Compression;
using MyMediaLite.Eval;
using MyMediaLite.IO.KDDCup2011;
using MyMediaLite.Util;

class MergeTrack2
{
	const int NUM_CANDIDATES = 6;

	static bool read_compressed = false;

	/// <summary>Parameters: num_files weight_1 .. weight_n file_1 .. file_n output_file</summary>
	/// <param name="args">the command-line arguments</param>
	public static void Main(string[] args)
	{
		var ni = new NumberFormatInfo();
		ni.NumberDecimalDigits = '.';

		AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Handlers.UnhandledExceptionHandler);		
		
		// parse command-line parameters
		string data_dir      = null;
		string output_file   = null;
		bool greedy_forward  = false;
	   	var p = new OptionSet() {
   			{ "data-dir=",       v => data_dir = v },
			{ "output-file=",    v => output_file = v },
			{ "greedy-forward",  v => greedy_forward = v != null },
			{ "read-compressed", v => read_compressed = v != null },
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
				weights.Add(weights.Count == 0 ? 1.1 : 1);

			files.Add(tokens[0]);
		}

		if (greedy_forward)
		{
			GreedyForwardSearch(files, data_dir); // ignore the weights for now
		}
		else
		{
			if (data_dir != null)
			{
				IList<byte> validation_predictions = MergeValidationFiles(files, weights);
				Eval(validation_predictions, data_dir);
				WritePredictions(validation_predictions, output_file + "-validation");
			}
		}
		
		Console.WriteLine("{0} files", files.Count);
		IList<byte> final_prediction = MergeFiles(files, weights);
		WritePredictions(final_prediction, output_file);
	}

	// TODO more structure
	static void GreedyForwardSearch(IList<string> candidate_files, string data_dir)
	{
		int k = 20;
		
		var candidate_items = Track2Items.Read(data_dir + "/mml-track2/validationCandidatesIdx2.txt");
		var item_hits       = Track2Items.Read(data_dir + "/mml-track2/validationHitsIdx2.txt");

		// get eval results for all predictions
		var candidate_error = new Dictionary<string, double>();
		foreach (string file in candidate_files)
			candidate_error[file] = Eval(file, candidate_items, item_hits);
		
		// select top 3 for the starting ensemble
		var top3 = (
			from file in candidate_error.Keys
			orderby candidate_error[file]
			select file).Take(3);
		
		// the ensemble
		var ensemble = new List<string>(top3); // add the top 3
		foreach (string file in top3)
			candidate_error.Remove(file); // remove from candidates
		
		IList<byte> validation_predictions = MergeValidationFiles(ensemble);
		double best_result = Eval(validation_predictions, candidate_items, item_hits);

		var files_by_error = new List<string>(
			from file in candidate_error.Keys
			orderby candidate_error[file]
			select file);
		
		while (files_by_error.Count() > 0)
		{
			/*
			// get the K best candidates
			var top_k =
				from file in candidate_error.Keys
				orderby candidate_error[file]
				select file.Take(k);

			// TODO load top_k predictions into memory to save IO
			
			// compute difference
			var difference = new Dictionary<string, double>();
			foreach (string file in top_k)
				difference[file] = ComputeDifference(validation_predictions, file);
			*/
			
			// remove from candidates, add to ensemble
			var next_candidate = files_by_error.First();
			files_by_error.Remove(next_candidate);
			ensemble.Add(next_candidate);
			
			Console.WriteLine("candidate {0}: {1}", next_candidate, candidate_error[next_candidate]);
			
			double result = Eval(validation_predictions, candidate_items, item_hits);
			if (result >= best_result) // if no improvement
			{
				ensemble.RemoveAt(ensemble.Count - 1); // remove last
				Console.WriteLine("Do not keep.");
			}
			else
			{
				best_result = result;
				Console.WriteLine("Keep.");
			}
		}
	}

	/*
	static int ComputeDifference(IList<byte> predictions, string file)
	{
		
	}
	*/
	
	// TODO make data_dir global
	static double Eval(IList<byte> predictions, string data_dir)
	{
		var candidates = Track2Items.Read(data_dir + "/mml-track2/validationCandidatesIdx2.txt");
		var hits       = Track2Items.Read(data_dir + "/mml-track2/validationHitsIdx2.txt");

		return Eval(predictions, candidates, hits);
	}

	static double Eval(string file, Dictionary<int, IList<int>> candidates, Dictionary<int, IList<int>> hits)
	{
		var files   = new List<string>();
		var weights = new List<double>();
				
		files.Add(file);
		weights.Add(1);
		
		weights[0] = 1.1; // tie-breaker
				
		return Eval(files, weights, candidates, hits);
	}			
			
	static double Eval(IList<string> files, IList<double> weights, string data_dir)
	{
		var candidates = Track2Items.Read(data_dir + "/mml-track2/validationCandidatesIdx2.txt");
		var hits       = Track2Items.Read(data_dir + "/mml-track2/validationHitsIdx2.txt");

		return Eval(files, weights, candidates, hits);
	}

	static double Eval(IList<string> files, IList<double> weights, Dictionary<int, IList<int>> candidates, Dictionary<int, IList<int>> hits)
	{
		IList<byte> validation_predictions = MergeValidationFiles(files, weights);

		return Eval(validation_predictions, candidates, hits);
	}

	static double Eval(IList<byte> predictions, Dictionary<int, IList<int>> candidates, Dictionary<int, IList<int>> hits)
	{
		double result = KDDCup.EvaluateTrack2(predictions, candidates, hits);
		Console.WriteLine("ERR {0} memory {1}", result, Memory.Usage);
		return result;
	}

	static IList<byte> MergeValidationFiles(IList<string> files)
	{
		var weights = new double[files.Count];
		for (int i = 0; i < weights.Length; i++)
			weights[i] = 1;		
		
		return MergeValidationFiles(files, weights);
	}
	
	static IList<byte> MergeValidationFiles(IList<string> files, IList<double> weights)
	{
		IList<string> validation_files = new List<string>();
		foreach (string filename in files)
			validation_files.Add(ValidationFilename(filename));

		return MergeFiles(validation_files, weights);
	}

	static string ValidationFilename(string filename)
	{
		string[] tokens = filename.Split(new string[] { "-it-" }, StringSplitOptions.None);
		if (tokens.Length == 2)
			return tokens[0] + "-validate-it-" + tokens[1];
		else if (tokens.Length == 1)
			return filename + "-validate";
		else
			throw new Exception();
	}		
	
	static IList<byte> MergeFiles(IList<string> files)
	{
		var weights = new double[files.Count];
		for (int i = 0; i < weights.Length; i++)
			weights[i] = 1;
		
		weights[0] = 1.1; // tie-breaker
		
		return MergeFiles(files, weights);
	}
	
	static IList<byte> ReadFile(string file)
	{
		var reader = read_compressed
			? new BinaryReader(new GZipStream(new FileStream(file + ".gz", FileMode.Open, FileAccess.Read), CompressionMode.Decompress))
			: new BinaryReader(               new FileStream(file,         FileMode.Open, FileAccess.Read));
		
		var predictions = new List<byte>();
		char[] chunk;
		while ( (chunk = reader.ReadChars(NUM_CANDIDATES)).Length > 0 )
			foreach (char c in chunk)
				if (c == '1')
					predictions.Add(1);
				else if (c == '0')
					predictions.Add(0);
				else
					throw new IOException("Unknown value: " + c);
		
		return predictions;
	}
	
	static IList<byte> MergeFiles(IList<string> files, IList<double> weights)
	{
		// open files
		var readers = new BinaryReader[files.Count];
		for (int i = 0; i < files.Count; i++)
			if (read_compressed)
				readers[i] = new BinaryReader(new GZipStream(new FileStream(files[i] + ".gz", FileMode.Open, FileAccess.Read), CompressionMode.Decompress));
			else
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