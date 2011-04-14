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
	static string data_dir      = null;

	/// <summary>Parameters: num_files weight_1 .. weight_n file_1 .. file_n output_file</summary>
	/// <param name="args">the command-line arguments</param>
	public static void Main(string[] args)
	{
		var ni = new NumberFormatInfo();
		ni.NumberDecimalDigits = '.';

		AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Handlers.UnhandledExceptionHandler);

		// parse command-line parameters

		string output_file    = null;
		bool greedy_forward   = false;
		int diversification_k = 1;
	   	var p = new OptionSet() {
   			{ "data-dir=",               v => data_dir = v },
			{ "output-file=",            v => output_file = v },
			{ "greedy-forward",          v => greedy_forward = v != null },
			{ "k|pick-most-diverse-from=", (int v) => diversification_k = v },
			{ "read-compressed",         v => read_compressed = v != null },
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
			var greedy_files = GreedyForwardSearch(files, diversification_k); // ignore the weights for now

			Console.WriteLine("{0} files", greedy_files.Count);
			IList<byte> final_prediction = MergeFiles(greedy_files);
			WritePredictions(final_prediction, output_file);
		}
		else
		{
			if (data_dir != null)
			{
				IList<byte> validation_predictions = MergeValidationFiles(files, weights);
				double result = Eval(validation_predictions);
				Console.WriteLine("ERR {0:0.#######}", result);
				WritePredictions(validation_predictions, output_file + "-validation");
			}

			Console.WriteLine("{0} files", files.Count);
			IList<byte> final_prediction = MergeFiles(files, weights);
			WritePredictions(final_prediction, output_file);
		}
	}

	// TODO more structure
	static IList<string> GreedyForwardSearch(IList<string> candidate_files, int k)
	{
		var candidate_items = Track2Items.Read(data_dir + "/mml-track2/validationCandidatesIdx2.txt");
		var item_hits       = Track2Items.Read(data_dir + "/mml-track2/validationHitsIdx2.txt");

		// get eval results for all predictions
		Console.Write("Calculating the quality of {0} candidates ... ", candidate_files.Count);
		var candidate_error = new Dictionary<string, double>();
		foreach (string file in candidate_files)
		{
			candidate_error[file] = Eval(file, candidate_items, item_hits);
			Console.Error.Write(".");
		}
		Console.WriteLine("done.");

		// the ensemble
		var ensemble = new List<string>();
		var ensemble_validation_predictions = new List<IList<byte>>();

		double best_result = 10;

		var files_by_error = new List<string>(
			from file in candidate_error.Keys
			orderby candidate_error[file]
			select file);

		// add the top model as a start
		var top_file = files_by_error.First();
		files_by_error.Remove(top_file);
		ensemble.Add(top_file);
		ensemble_validation_predictions.Add(ReadFile(ValidationFilename(top_file)));

		// init merged predictions
		IList<byte> ensemble_merged_predictions = ensemble_validation_predictions.First();

		// prediction cache (to save IO)
		var prediction_cache = new Dictionary<string, IList<byte>>();

		while (files_by_error.Count() > 0)
		{
			// get the K best candidates
			var top_k = files_by_error.Take(k);

			var difference = new Dictionary<string, double>();
			if (k > 1)
			{
				// compute difference
				foreach (string file in top_k)
				{
					if (!prediction_cache.ContainsKey(file))
						prediction_cache[file] = ReadFile(ValidationFilename(file));

					difference[file] = ComputeDifference(prediction_cache[file], ensemble_merged_predictions);
				}
			}
			else
			{
				difference[top_k.First()] = 0;
			}

			var files_by_difference =
				from file in difference.Keys
				orderby difference[file] descending
				select file;

			// remove from candidates, add to ensemble
			var next_candidate = files_by_difference.First();
			files_by_error.Remove(next_candidate);
			ensemble.Add(next_candidate);
			ensemble_validation_predictions.Add(prediction_cache[next_candidate]);
			Console.Write("candidate {0}: {1:F7} ... ", next_candidate, candidate_error[next_candidate]);

			// cache entry not needed any more
			prediction_cache.Remove(next_candidate);

			ensemble_merged_predictions = MergePredictions(ensemble_validation_predictions);
			double result = Eval(ensemble_merged_predictions, candidate_items, item_hits);
			Console.Write("ERR {0:F7} ... ", result);
			if (result > best_result) // if no improvement
			{
				ensemble.RemoveAt(ensemble.Count - 1); // remove last
				ensemble_validation_predictions.RemoveAt(ensemble_validation_predictions.Count - 1); // remove last
				Console.WriteLine("do not keep.");
			}
			else
			{
				best_result = result;
				Console.WriteLine("keep ({0}).", ensemble.Count);
			}
		}

		Console.WriteLine("files {0} ERR {1} memory {2}", ensemble.Count, best_result, Memory.Usage);

		return ensemble;
	}

	static int ComputeDifference(IList<byte> predictions1, IList<byte> predictions2)
	{
		if (predictions1.Count != predictions2.Count)
			throw new ArgumentException("predictions must have equal lengths.");

		int diffs = 0;

		for (int i = 0; i < predictions1.Count; i++)
			if (predictions1[i] != predictions2[i])
				diffs++;

		return diffs;
	}

	static double Eval(IList<byte> predictions)
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

	static double Eval(IList<string> files, IList<double> weights)
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
		return result;
	}

	static IList<byte> MergePredictions(IList<IList<byte>> predictions)
	{
		var weights = new double[predictions.Count];
		for (int i = 0; i < weights.Length; i++)
			weights[i] = 1;

		weights[0] = 1.1; // tie-breaker

		return MergePredictions(predictions, weights);
	}

	static IList<byte> MergeValidationFiles(IList<string> files)
	{
		var weights = new double[files.Count];
		for (int i = 0; i < weights.Length; i++)
			weights[i] = 1;

		weights[0] = 1.1; // tie-breaker

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

		try
		{
			while ( (chunk = reader.ReadChars(NUM_CANDIDATES)).Length > 0 )
				foreach (char c in chunk)
					if (c == '1')
						predictions.Add(1);
					else if (c == '0')
						predictions.Add(0);
					else
						throw new IOException("Unknown value: " + c);
		}
		catch (EndOfStreamException) { /* do nothing */ }

		return predictions;
	}

	static IList<byte> MergePredictions(IList<IList<byte>> predictions, IList<double> weights)
	{
		var combined_predictions = new byte[predictions[0].Count];

		for (int pos = 0; pos < combined_predictions.Length; pos += NUM_CANDIDATES)
		{
			var weighted_votes = new double[NUM_CANDIDATES];
			for (int i = 0; i < predictions.Count; i++)
				for (int j = 0; j < weighted_votes.Length; j++)
					weighted_votes[j] += weights[i] * predictions[i][pos + j];

			var positions = new List<int>(new int[] { 0, 1, 2, 3, 4, 5 });
			positions.Sort(delegate(int pos1, int pos2) { return weighted_votes[pos2].CompareTo(weighted_votes[pos1]); } );

			for (int i = 0; i < positions.Count; i++)
				if (positions.IndexOf(i) < 3)
					combined_predictions[pos + i] = 1;
				else
					combined_predictions[pos + i] = 0;
		}

		return combined_predictions;
	}

	// TODO get rid of this?
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
		catch (EndOfStreamException) { /* do nothing */ }

		return combined_predictions;
	}

	static void WritePredictions(IList<byte> predictions, string output_file)
	{
		using (var writer = new StreamWriter(output_file))
			foreach (byte p in predictions)
				writer.Write(p);
	}
}