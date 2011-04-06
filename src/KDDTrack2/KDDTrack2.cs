// Copyright (C) 2010, 2011 Zeno Gantner
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MyMediaLite;
using MyMediaLite.Data;
using MyMediaLite.DataType;
using MyMediaLite.Eval;
using MyMediaLite.IO;
using MyMediaLite.IO.KDDCup2011;
using MyMediaLite.ItemRecommendation;
using MyMediaLite.RatingPrediction;
using MyMediaLite.Util;

/// <summary>Rating prediction program, see Usage() method for more information</summary>
public static class KDDTrack2
{
	static NumberFormatInfo ni = new NumberFormatInfo();

	// data sets
	//  training
	static IRatings training_ratings;
	static PosOnlyFeedback training_posonly;
	static IRatings complete_ratings;
	static PosOnlyFeedback complete_posonly;
	
	//  validation
	static IRatings validation_ratings;
	static Dictionary<int, IList<int>> validation_candidates;
	static Dictionary<int, IList<int>> validation_hits;
	
	//  test
	static Dictionary<int, IList<int>> test_candidates;

	// recommenders
	static ItemRecommender recommender = null;

	// time statistics
	static List<double> training_time_stats = new List<double>();
	static List<double> eval_time_stats     = new List<double>();
	static List<double> acc_eval_stats      = new List<double>();

	// global command line parameters
	static string save_model_file;
	static string load_model_file;
	static int max_iter;
	static int find_iter;
	static double epsilon;
	static double acc_cutoff;
	static string prediction_file;
	static bool sample_data;

	static void Usage(string message)
	{
		Console.WriteLine(message);
		Console.WriteLine();
		Usage(-1);
	}

	static void Usage(int exit_code)
	{
		Console.WriteLine(@"
MyMediaLite KDD Cup 2011 Track 2 tool

 usage:  KDDTrack2.exe METHOD [ARGUMENTS] [OPTIONS]

  use '-' for either TRAINING_FILE or TEST_FILE to read the data from STDIN

  methods (plus arguments and their defaults):");

			Console.Write("   - ");
			Console.WriteLine(string.Join("\n   - ", Recommender.List("MyMediaLite.ItemRecommendation")));

			Console.WriteLine(@"method ARGUMENTS have the form name=value

  general OPTIONS have the form name=value
   - option_file=FILE           read options from FILE (line format KEY: VALUE)
   - random_seed=N              set random seed to N
   - data_dir=DIR               load all files from DIR
   - save_model=FILE            save computed model to FILE
   - load_model=FILE            load model from FILE
   - prediction_file=FILE       write the predictions to  FILE ('-' for STDOUT)
   - sample_data=BOOL           assume the sample data set instead of the real one

  options for finding the right number of iterations (MF methods)
   - find_iter=N                give out statistics every N iterations
   - max_iter=N                 perform at most N iterations
   - epsilon=NUM                abort iterations if accuracy is less than best result plus NUM
   - acc_cutoff=NUM             abort if accuracy is above NUM");

		Environment.Exit(exit_code);
	}

    static void Main(string[] args)
    {
		Assembly assembly = Assembly.GetExecutingAssembly();
		Assembly.LoadFile(Path.GetDirectoryName(assembly.Location) + Path.DirectorySeparatorChar + "MyMediaLiteExperimental.dll");

		double min_rating = 0;
		double max_rating = 100;

		AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(Handlers.UnhandledExceptionHandler);
		Console.CancelKeyPress += new ConsoleCancelEventHandler(AbortHandler);
		ni.NumberDecimalDigits = '.';

		// check number of command line parameters
		if (args.Length < 1)
			Usage("Not enough arguments.");

		// read command line parameters
		string method = args[0];

		CommandLineParameters parameters = null;
		try	{ parameters = new CommandLineParameters(args, 1); }
		catch (ArgumentException e) { Usage(e.Message);	}

		// arguments for iteration search
		find_iter   = parameters.GetRemoveInt32(  "find_iter",   0);
		max_iter    = parameters.GetRemoveInt32(  "max_iter",    500);
		epsilon     = parameters.GetRemoveDouble( "epsilon",     0);
		acc_cutoff  = parameters.GetRemoveDouble( "acc_cutoff",  double.NegativeInfinity);

		// data arguments
		string data_dir  = parameters.GetRemoveString( "data_dir");
		if (data_dir != string.Empty)
			data_dir = data_dir + "/mml-track-2";
		else
			data_dir = "mml-track2";
		sample_data      = parameters.GetRemoveBool(   "sample_data", false);

		// other arguments
		save_model_file  = parameters.GetRemoveString( "save_model");
		load_model_file  = parameters.GetRemoveString( "load_model");
		int random_seed  = parameters.GetRemoveInt32(  "random_seed",  -1);
		prediction_file  = parameters.GetRemoveString( "prediction_file");

		if (random_seed != -1)
			MyMediaLite.Util.Random.InitInstance(random_seed);

		recommender = Recommender.CreateItemRecommender(method);
		if (recommender == null)
			Usage(string.Format("Unknown method: '{0}'", method));

		Recommender.Configure(recommender, parameters, Usage);

		if (parameters.CheckForLeftovers())
			Usage(-1);

		// load all the data
		TimeSpan loading_time = Utils.MeasureTime(delegate() {
			LoadData(data_dir);
		});
		Console.WriteLine(string.Format(ni, "loading_time {0,0:0.##}", loading_time.TotalSeconds));

		// prepare recommenders
		training_posonly = CreateFeedback(training_ratings);
		recommender.Feedback = training_posonly;
		Console.Error.WriteLine("memory before deleting training_ratings: {0}", Memory.Usage);
		training_ratings = null;
		Console.Error.WriteLine("memory after deleting training_ratings:  {0}", Memory.Usage);

		if (load_model_file != string.Empty)
			Recommender.LoadModel(recommender, load_model_file);

		DoTrack2();

		Console.Error.WriteLine("memory {0}", Memory.Usage);
	}

	static PosOnlyFeedback CreateFeedback(IRatings ratings)
	{
		var feedback = new PosOnlyFeedback();

		for (int i = 0; i < ratings.Count; i++)
			if (ratings[i] >= 80)
				feedback.Add(ratings.Users[i], ratings.Items[i]);

		Console.Error.WriteLine("{0} ratings > 80", feedback.Count);
		
		return feedback;
	}

	static void DoTrack2()
	{
		TimeSpan seconds;

		var item_recommender = recommender as ItemRecommender;

		// TODO have two different recommenders
		
		if (find_iter != 0)
		{
			if ( !(recommender is IIterativeModel) )
				Usage("Only iterative recommenders support find_iter.");

			IIterativeModel iterative_recommender_validate = (MF) item_recommender;
			//IIterativeModel iterative_recommender_final    = (MF) item_recommender.Clone();
			Console.WriteLine(recommender.ToString() + " ");

			if (load_model_file == string.Empty)
				recommender.Train();

			// TODO evaluate and display results
			Console.WriteLine(" " + iterative_recommender_validate.NumIter);

			for (int i = iterative_recommender_validate.NumIter + 1; i <= max_iter; i++)
			{
				TimeSpan time = Utils.MeasureTime(delegate() {
					iterative_recommender_validate.Iterate();
				});
				training_time_stats.Add(time.TotalSeconds);

				if (i % find_iter == 0)
				{
					time = Utils.MeasureTime(delegate() {
						// TODO evaluate + output + add to stats
						Console.WriteLine(" " + i);
					});
					eval_time_stats.Add(time.TotalSeconds);
				}
			} // for

			DisplayIterationStats();
			Recommender.SaveModel(recommender, save_model_file);
		}
		else
		{
			seconds = Utils.MeasureTime(delegate() {
				item_recommender.Train();
			});
			Console.Write(" training_time " + seconds + " ");

			seconds = Utils.MeasureTime(delegate() {
				// TODO evaluate + output
			});
			Console.Write(" evaluation_time " + seconds + " ");
		}

		// reset training data
		if (recommender is RatingPredictor)
			((RatingPredictor) recommender).Ratings = training_ratings;
		if (recommender is ItemRecommender)
			((ItemRecommender) recommender).Feedback = training_posonly;

		Console.WriteLine();

		if (prediction_file != string.Empty)
		{
			// do complete training + testing
			if (load_model_file != string.Empty)
			{
				Console.Write(recommender.ToString());

				seconds = Utils.MeasureTime( delegate() { recommender.Train(); } );
	   			Console.Write(" training_time " + seconds + " ");
				Recommender.SaveModel(recommender, save_model_file);
			}

			Console.Write(recommender.ToString() + " ");

			seconds = Utils.MeasureTime(
		    	delegate() {
					Console.WriteLine();
					KDDCup.PredictTrack2(recommender, test_candidates, prediction_file);
				}
			);
			Console.Write("predicting_time " + seconds);
		}

		Console.WriteLine();
	}

    static void LoadData(string data_dir)
	{
		string training_file              = Path.Combine(data_dir, "trainIdx2.txt");
		string test_file                  = Path.Combine(data_dir, "testIdx2.txt");
		string validation_candidates_file = Path.Combine(data_dir, "validationIdxCandidates2.txt");
		string validation_ratings_file    = Path.Combine(data_dir, "validationIdxRatings2.txt");
		string validation_hits_file       = Path.Combine(data_dir, "validationIdxHits2.txt");
		string track_file                 = Path.Combine(data_dir, "trackData2.txt");
		string album_file                 = Path.Combine(data_dir, "albumData2.txt");
		string artist_file                = Path.Combine(data_dir, "artistData2.txt");
		string genre_file                 = Path.Combine(data_dir, "genreData2.txt");
		int num_ratings                   = 61943733;

		if (sample_data)
		{
			num_ratings                = 8824; // these are not true values, just upper bounds // TODO add correct numbers
			training_file              = Path.Combine(data_dir, "trainIdx2.firstLines.txt");
			test_file                  = Path.Combine(data_dir, "testIdx2.firstLines.txt");
			validation_candidates_file = Path.Combine(data_dir, "validationIdxCandidates2.txt");
			validation_ratings_file    = Path.Combine(data_dir, "validationIdxRatings2.txt");
			validation_hits_file       = Path.Combine(data_dir, "validationIdxHits2.txt");
		}

		// read training data
		training_ratings = MyMediaLite.IO.KDDCup2011.Ratings.Read(training_file, num_ratings);

		// read validation data
		validation_candidates = Track2Items.Read(validation_candidates_file);
		validation_hits       = Track2Items.Read(validation_hits_file);
		
		if (validation_hits.Count != validation_candidates.Count)
			throw new Exception("inconsistent number of users in hits and candidates");
		int num_validation_users = validation_hits.Count;
		int num_validation_ratings = 3 * num_validation_users;
		validation_ratings = MyMediaLite.IO.KDDCup2011.Ratings.Read(validation_ratings_file, num_validation_ratings);
		complete_ratings = new CombinedRatings(training_ratings, validation_ratings);	
		
		// read test data
		test_candidates = Track2Items.Read(test_file);

		// read item data
		if (recommender is IKDDCupRecommender)
		{
			var kddcup_recommender = recommender as IKDDCupRecommender;
			kddcup_recommender.ItemInfo = Items.Read(track_file, album_file, artist_file, genre_file, 2);
		}
	}

	static void AbortHandler(object sender, ConsoleCancelEventArgs args)
	{
		DisplayIterationStats();
	}

	static void DisplayIterationStats()
	{
		if (training_time_stats.Count > 0)
			Console.Error.WriteLine(string.Format(
			    ni,
				"iteration_time: min={0,0:0.##}, max={1,0:0.##}, avg={2,0:0.##}",
	            training_time_stats.Min(), training_time_stats.Max(), training_time_stats.Average()
			));
		if (eval_time_stats.Count > 0)
			Console.Error.WriteLine(string.Format(
			    ni,
				"eval_time: min={0,0:0.##}, max={1,0:0.##}, avg={2,0:0.##}",
	            eval_time_stats.Min(), eval_time_stats.Max(), eval_time_stats.Average()
			));
	}
}