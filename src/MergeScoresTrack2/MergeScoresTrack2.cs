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
using MyMediaLite.Classification;
using MyMediaLite.Eval;
using MyMediaLite.IO.KDDCup2011;
using MyMediaLite.Util;

class MergeScoresTrack2
{
	const int NUM_CANDIDATES = 6;
	const int FILE_SIZE      = 607032;

	static string data_dir = null;
	static bool log_reg = false;
	static bool bpr     = false;

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
		bool prob80plus       = false;
		int diversification_k = 1;
		double err_threshold  = 1;
	   	var p = new OptionSet() {
   			{ "data-dir=",                 v => data_dir = v },
			{ "prediction-file=",          v => output_file = v },
			{ "greedy-forward",            v => greedy_forward = v != null },
			{ "logistic-regression",       v => log_reg = v != null },
			{ "bpr",                       v => bpr = v != null },
			{ "greedy-forward",            v => greedy_forward = v != null },
			{ "prob-80-plus",              v => prob80plus = v != null },
			{ "error-threshold=",          (double v) => err_threshold = v },
			{ "k|pick-most-diverse-from=", (int v) => diversification_k = v },
   	  	};
   		IList<string> extra_args = p.Parse(args);

		var files = new List<string>();
		var weights = new List<double>();
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
			var greedy_files = GreedyForwardSearch(files, diversification_k, err_threshold); // ignore the weights for now

			IList<double> final_scores = MergeFiles(greedy_files);
			IList<double> valid_scores = MergeFiles(ValidationFilenames(greedy_files));
			IList<byte> final_predictions = Scores2Predictions(final_scores);

			if (output_file != null)
			{
				WritePredictions(final_predictions, output_file);
				WriteScores(final_scores, output_file + "-scores");
				WriteScores(valid_scores, ValidationFilename(output_file + "-scores"));
			}
		}
		else if (prob80plus)
		{
			Console.Write("Computing >80 probabilities for {0} files ...", files.Count);
			IList<double> test_probs = ComputeProb80Plus(files);
			IList<double> valid_probs = ComputeProb80Plus(ValidationFilenames(files));
			Console.WriteLine(" done");

			WriteScores(test_probs, output_file); // maybe change to different param
			WriteScores(valid_probs, ValidationFilename(output_file)); // maybe change to different param
		}
		else if (output_file != null)
		{
			if (data_dir != null)
			{
				IList<byte> validation_predictions = Scores2Predictions(MergeValidationFiles(files, weights));
				double result = Eval(validation_predictions);
				Console.WriteLine("ERR {0:0.#######}", result);
				WritePredictions(validation_predictions, output_file + "-validation");
			}

			Console.WriteLine("{0} files", files.Count);
			IList<byte> final_prediction = Scores2Predictions(MergeFiles(files, weights));
			WritePredictions(final_prediction, output_file);
		}
	}

	static IList<double> ComputeProb80Plus(IList<string> filenames)
	{
		var scores = new List<IList<double>>();
		foreach (string f in filenames)
			scores.Add(ReadFile(f));

		var probs = new double [FILE_SIZE];
		for (int i = 0; i < FILE_SIZE; i++)
		{
			for (int j = 0; j < filenames.Count; j++)
				probs[i] += scores[j][i];
			probs[i] /= filenames.Count;
		}

		return probs;
	}

	static IList<string> GreedyForwardSearch(IList<string> candidate_files, int k, double err_threshold)
	{
		var candidate_items = Track2Items.Read(data_dir + "/mml-track2/validationCandidatesIdx2.txt");
		var item_hits       = Track2Items.Read(data_dir + "/mml-track2/validationHitsIdx2.txt");

		// prediction cache (to save IO)
		var score_cache = new Dictionary<string, IList<double>>();

		// get eval results for all predictions
		Console.Write("Calculating the errors of {0} candidates ... ", candidate_files.Count);
		var error = new Dictionary<string, double>();
		foreach (string file in candidate_files)
		{
			score_cache[file] = ReadFile(ValidationFilename(file));
			double err = Eval(score_cache[file], candidate_items, item_hits);

			// only keep if error is below threshold
			if (err < err_threshold)
			{
			    error[file] = err;
				Console.Error.Write(".");
			}
			else
			{
				score_cache.Remove(file);
				Console.Error.Write("_");
			}
		}
		Console.WriteLine("done: candidates {0} memory {1}", error.Count, Memory.Usage);

		// the ensemble
		var ensemble = new List<string>();
		var ensemble_validation_scores = new List<IList<double>>();

		double best_result = double.MaxValue;

		var files_by_error = new List<string>(
			from file in error.Keys
			orderby error[file]
			select file);

		// add the top model as a start
		var top_file = files_by_error.First();
		files_by_error.Remove(top_file);
		ensemble.Add(top_file);
		ensemble_validation_scores.Add(ReadFile(ValidationFilename(top_file)));

		// init merged predictions
		IList<double> ensemble_merged_scores = ensemble_validation_scores.First();

		while (files_by_error.Count() > 0)
		{
			// get the K best candidates
			var top_k = files_by_error.Take(k);

			var difference = new Dictionary<string, double>();
			if (k > 1)
			{
				// compute difference
				foreach (string file in top_k) // TODO optimize for speed
					difference[file] = ComputeDifference(Scores2Predictions(score_cache[file]), Scores2Predictions(ensemble_merged_scores)); // TODO maybe compare scores and merged_scores
			}
			else
			{
				var file = top_k.First();
				difference[file] = 0;
			}

			var files_by_difference =
				from file in difference.Keys
				orderby difference[file] descending
				select file;

			// remove from candidates, add to ensemble
			var next_candidate = files_by_difference.First();
			files_by_error.Remove(next_candidate);
			ensemble.Add(next_candidate);
			ensemble_validation_scores.Add(score_cache[next_candidate]);
			Console.Write("({0}/{1}) {2}: {3:F7} ... ", error.Count - files_by_error.Count, error.Count, next_candidate, error[next_candidate]);

			// cache entry not needed any more
			score_cache.Remove(next_candidate);

			ensemble_merged_scores = MergeScores(ensemble_validation_scores);
			double result = Eval(ensemble_merged_scores, candidate_items, item_hits);
			Console.Write("ERR {0:F7} ... ", result);
			if (result > best_result) // if no improvement
			{
				ensemble.RemoveAt(ensemble.Count - 1); // remove last
				ensemble_validation_scores.RemoveAt(ensemble_validation_scores.Count - 1); // remove last
				Console.WriteLine(".");
			}
			else
			{
				best_result = result;
				Console.WriteLine("keep ({0}).", ensemble.Count);
			}
		}

		// show results
		foreach (var file in ensemble)
			Console.WriteLine("{0} ({1})", file, error[file]);

		return ensemble;
	}

	static double ComputeDifference(IList<byte> predictions1, IList<byte> predictions2)
	{
		if (predictions1.Count != predictions2.Count)
			throw new ArgumentException("predictions must have equal lengths.");

		double diff = 0;

		for (int i = 0; i < predictions1.Count; i++)
			if (predictions1[i] != predictions2[i])
				diff += 1.0;

		return diff;
	}

	static IList<byte> Scores2Predictions(IList<double> scores)
	{
		var predictions = new byte[scores.Count];

		for (int offset = 0; offset < scores.Count; offset += NUM_CANDIDATES)
		{
			var positions = new List<int>(new int[] { 0, 1, 2, 3, 4, 5 }); // TODO

			positions.Sort(delegate(int pos1, int pos2) { return scores[offset + pos2].CompareTo(scores[offset + pos1]); } );

			for (int i = 0; i < positions.Count; i++)
				if (positions.IndexOf(i) < 3)
					predictions[offset + i] = 1;
				else
					predictions[offset + i] = 0;
		}

		return predictions;
	}

	static double Eval(IList<double> scores)
	{
		return Eval(Scores2Predictions(scores));
	}

	static double Eval(IList<byte> predictions)
	{
		var candidates = Track2Items.Read(data_dir + "/mml-track2/validationCandidatesIdx2.txt");
		var hits       = Track2Items.Read(data_dir + "/mml-track2/validationHitsIdx2.txt");

		return Eval(predictions, candidates, hits);
	}

	static double Eval(IList<double> scores, Dictionary<int, IList<int>> candidates, Dictionary<int, IList<int>> hits)
	{
		return KDDCup.EvaluateTrack2(Scores2Predictions(scores), candidates, hits);
	}

	static double Eval(IList<byte> predictions, Dictionary<int, IList<int>> candidates, Dictionary<int, IList<int>> hits)
	{
		return KDDCup.EvaluateTrack2(predictions, candidates, hits);
	}

	//static IList<double> MergeScores(IList<IList<double>> scores, Dictionary<int, IList<int>> candidates, Dictionary<int, IList<int>> hits)
	static IList<double> MergeScores(IList<IList<double>> scores)
	{
		double[] weights;
		
		/*if (log_reg)
		{
			var lr = new LogisticRegression();
			//lr.PredictorVariables = scores;
			//var targets = new byte[scores[0].Count];
			//foreach (int u in candidates.Keys)
				
			//lr.TargetVariables = targets;
		}
		else
		{*/
			weights = new double[scores.Count];
			for (int i = 0; i < weights.Length; i++)
				weights[i] = 1;
		//}

		return MergeScores(scores, weights);
	}

	static IList<double> MergeValidationFiles(IList<string> files)
	{
		var weights = new double[files.Count];
		for (int i = 0; i < weights.Length; i++)
			weights[i] = 1;

		return MergeValidationFiles(files, weights);
	}

	static IList<double> MergeValidationFiles(IList<string> files, IList<double> weights)
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
			throw new Exception("Could not create validation filename for " + filename);
	}

	static IList<string> ValidationFilenames(IList<string> filenames)
	{
		var validation_filenames = new string[filenames.Count];
		for (int i = 0; i < filenames.Count; i++)
			validation_filenames[i] = ValidationFilename(filenames[i]);

		return validation_filenames;
	}

	static IList<double> MergeFiles(IList<string> files)
	{
		var weights = new double[files.Count];
		for (int i = 0; i < weights.Length; i++)
			weights[i] = 1;

		return MergeFiles(files, weights);
	}

	static IList<double> ReadFile(string file)
	{
		var scores = new double[FILE_SIZE];

		using (var reader = new BinaryReader(new FileStream(file, FileMode.Open, FileAccess.Read)))
			for (int i = 0; i < FILE_SIZE; i++)
				scores[i] = reader.ReadDouble();

		return scores;
	}

	static IList<double> MergeScores(IList<IList<double>> scores, IList<double> weights)
	{
		// compute weighted sums
		var combined_scores = new double[scores[0].Count];
		for (int pos = 0; pos < combined_scores.Length; pos++)
			for (int i = 0; i < scores.Count; i++)
				combined_scores[pos] += weights[i] * scores[i][pos];
		
		// compute averages
		for (int pos = 0; pos < combined_scores.Length; pos++)
			combined_scores[pos] /= weights.Sum();
		
		return combined_scores;
	}

	static IList<double> MergeFiles(IList<string> files, IList<double> weights)
	{
		// open files
		var readers = new BinaryReader[files.Count];
		for (int i = 0; i < files.Count; i++)
				readers[i] = new BinaryReader(new FileStream(files[i], FileMode.Open, FileAccess.Read));

		// compute weighted sums
		var combined_scores = new double[FILE_SIZE];
		try
		{
			// read and merge
			for (int i = 0; i < FILE_SIZE; i++)
				for (int j = 0; j < readers.Length; j++)
					combined_scores[i] += weights[j] * readers[j].ReadDouble();
		}
		catch (EndOfStreamException) { /* do nothing */ }

		// compute averages
		for (int pos = 0; pos < combined_scores.Length; pos++)
			combined_scores[pos] /= weights.Sum();		
		
		return combined_scores;
	}

	static void WriteScores(IList<double> scores, string output_file)
	{
		using (var writer = new BinaryWriter(new FileStream(output_file, FileMode.OpenOrCreate, FileAccess.Write)))
			foreach (double p in scores)
				writer.Write(p);
	}

	static void WritePredictions(IList<byte> predictions, string output_file)
	{
		using (var writer = new StreamWriter(output_file))
			foreach (byte p in predictions)
				writer.Write(p);
	}
}