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

class KDDTrack2Composite
{
	const int NUM_CANDIDATES = 6;
	const int FILE_SIZE = 607032;

	static string data_dir = null;
	// TODO the following should be a enum
	static bool sigmoid       = false;
	static bool pairwise_prob = false;
	static bool pairwise_wins = false;
	static bool rated_prob    = false;

	/// <summary>Parameters: num_files weight_1 .. weight_n file_1 .. file_n output_file</summary>
	/// <param name="args">the command-line arguments</param>
	public static void Main(string[] args)
	{
		var ni = new NumberFormatInfo();
		ni.NumberDecimalDigits = '.';

		AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Handlers.UnhandledExceptionHandler);

		// parse command-line parameters

		string prediction_file = null;
		//string score_file      = null;
	   	var p = new OptionSet() {
   			{ "data-dir=",              v => data_dir = v         },
			{ "prediction-file=",       v => prediction_file = v  },
			{ "sigmoid",                v => sigmoid = v != null  },
			{ "pairwise-probability",   v => pairwise_prob = v != null },
			{ "pairwise-wins",          v => pairwise_wins = v != null },
			{ "rated-probability",      v => rated_prob = v != null    },
			//{ "score-file=",            v => score_file = v },			
   	  	};
   		IList<string> extra_args = p.Parse(args);

		string rated_file  = extra_args[0];
		string rating_file = extra_args[1];
		
		// combine files
		IList<double> test_scores       = CombineFiles(rated_file, rating_file);
		IList<double> validation_scores = CombineFiles(ValidationFilename(rated_file), ValidationFilename(rating_file));

		// compute error on validation set
		string validation_candidates_file = Path.Combine(data_dir, "mml-track2/validationCandidatesIdx2.txt");
		string validation_hits_file       = Path.Combine(data_dir, "mml-track2/validationHitsIdx2.txt");				
		var candidates = Track2Items.Read(validation_candidates_file);
		var hits       = Track2Items.Read(validation_hits_file);
		double error = KDDCup.EvaluateTrack2(Decide(validation_scores), candidates, hits);
		Console.WriteLine("ERR {0:F7}", error);
		
		if (prediction_file != null)
		{
			WritePredictions(Decide(test_scores), prediction_file);
			WritePredictions(Decide(validation_scores), ValidationFilename(prediction_file));
		}
		
		/*
		if (score_file != null)
		{
			WriteScores(test_scores, score_file);
			WriteScores(test_scores, ValidationFilename(score_file));
		}
		*/
	}

	static IList<byte> Decide(IList<double> scores)
	{
		var decisions = new byte[scores.Count];
		
		for (int i = 0; i < scores.Count; i += 6)
		{
			var candidate_scores = new double[NUM_CANDIDATES];
			for (int j = 0; j < NUM_CANDIDATES; j++)
				candidate_scores[j] = scores[i + j];

			var positions = new List<int>(new int[] { 0, 1, 2, 3, 4, 5 }); // TODO should depend on NUM_CANDIDATES
			positions.Sort(delegate(int pos1, int pos2) { return candidate_scores[pos2].CompareTo(candidate_scores[pos1]); } );

			for (int j = 0; j < NUM_CANDIDATES; j++)
				if (positions.IndexOf(j) < 3)
					decisions[i + j] = 1;
				else
					decisions[i + j] = 0;
		}
		
		return decisions;
	}
	
	static IList<double> CombineFiles(string file1, string file2)
	{
		IList<double> content1 = ReadFile(file1);
		IList<double> content2 = ReadFile(file2);
		
		if (content1.Count != content2.Count)
			throw new Exception();
		
		var combined_content = new double[FILE_SIZE];
		
		for (int i = 0; i < content1.Count; i += 6)
		{
			var candidate_scores = new double[NUM_CANDIDATES];

			// do something with the candidates
			for (int j = 0; j < NUM_CANDIDATES; j++)
				if (sigmoid)
					candidate_scores[j] = Sigmoid(content1[i + j]) * content2[i + j];
				else if (pairwise_prob)
				{
					double prob = 0;
					for (int k = 0; k < NUM_CANDIDATES; k++)
						if (k != j)
							prob += Sigmoid(content1[i + j] - content2[i + k]);
					candidate_scores[j] = prob; // not normalized
				}
				else if (pairwise_wins)
				{
					int wins = 0;
					for (int k = 0; k < NUM_CANDIDATES; k++)
						if (k != j && content1[i + j] > content2[i + k])
							wins++;
					candidate_scores[j] = wins;
				}
				else if (rated_prob)
					candidate_scores[j] = ComputeRatedProbability(i, j, content1) * content2[i + j];
				else
					candidate_scores[j] = content1[i + j] * content2[i + j];
				
			
			// move candidates to the output
			for (int j = 0; j < candidate_scores.Length; j++)
				combined_content[i + j] = candidate_scores[j];
		}
		
		return combined_content;
	}
	
	static double ComputeRatedProbability(int offset, int index, IList<double> all_scores)
	{
		double prob = 0;
		
		double score = all_scores[offset + index];
		
		var other_scores = new double[NUM_CANDIDATES - 1];
		for (int i = 0; i < NUM_CANDIDATES; i++)
			if (i < index)
				other_scores[i] = all_scores[offset + i];
			else if (i > index)
				other_scores[i - 1] = all_scores[offset + i];
		
		for (int i = 0; i < other_scores.Length; i++)
			for (int j = 1; j < other_scores.Length; j++)
				for (int k = 2; k < other_scores.Length; k++)
					prob += Sigmoid(score - other_scores[i]) * Sigmoid(score - other_scores[j]) * Sigmoid(score - other_scores[k]);
		
		return prob;
	}
	
	static double Sigmoid(double x)
	{
		return (double) 1 / (1 + Math.Exp(-x));
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

	static IList<double> ReadFile(string file)
	{
		var content = new double[FILE_SIZE];
		
		using (var reader = new BinaryReader(new FileStream(file, FileMode.Open, FileAccess.Read)))
			for (int i = 0; i < FILE_SIZE; i++)
				content[i] = reader.ReadDouble();
			
		return content;
	}

	static void WritePredictions(IList<byte> predictions, string output_file)
	{
		using (var writer = new StreamWriter(output_file))
			foreach (byte p in predictions)
				writer.Write(p);
	}
}