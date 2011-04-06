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
public static class KDDCupProgram
{
	static NumberFormatInfo ni = new NumberFormatInfo();

	// data sets
	static IRatings training_ratings;
	static PosOnlyFeedback training_data_posonly;
	static IRatings validation_ratings;
	static IRatings combined_ratings;
	static IRatings track1_test_data;
	static Dictionary<int, IList<int>> track2_test_data;

	// recommenders
	static IRecommender recommender = null;

	// time statistics
	static List<double> training_time_stats = new List<double>();
	static List<double> fit_time_stats      = new List<double>();
	static List<double> eval_time_stats     = new List<double>();
	static List<double> rmse_eval_stats     = new List<double>();
	//static List<double> prec_eval_stats     = new List<double>();

	// global command line parameters
	static bool compute_fit;
	static string save_model_file;
	static string load_model_file;
	static int max_iter;
	static int find_iter;
	static int cross_validation;
	static double epsilon;
	static double rmse_cutoff;
	static double mae_cutoff;
	static bool no_eval;
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
MyMediaLite KDD Cup 2011 tool

 usage:  RatingPrediction.exe TRACK_NO METHOD [ARGUMENTS] [OPTIONS]

  use '-' for either TRAINING_FILE or TEST_FILE to read the data from STDIN

  methods (plus arguments and their defaults):");

			Console.Write("   - ");
			Console.WriteLine(string.Join("\n   - ", Recommender.List("MyMediaLite.RatingPrediction")));
			Console.Write("   - ");
			Console.WriteLine(string.Join("\n   - ", Recommender.List("MyMediaLite.ItemRecommendation")));

			Console.WriteLine(@"method ARGUMENTS have the form name=value

  general OPTIONS have the form name=value
   - option_file=FILE           read options from FILE (line format KEY: VALUE)
   - random_seed=N              set random seed to N
   - data_dir=DIR               load all files from DIR
   - save_model=FILE            save computed model to FILE
   - load_model=FILE            load model from FILE
   - no_eval=BOOL               do not evaluate
   - prediction_file=FILE       write the predictions to  FILE ('-' for STDOUT)
   - cross_validation=K         perform k-fold crossvalidation on the training data
                                 (ignores the test data)
   - sample_data=BOOL           assume the sample data set instead of the real one

  options for finding the right number of iterations (MF methods)
   - find_iter=N                give out statistics every N iterations
   - max_iter=N                 perform at most N iterations
   - epsilon=NUM                abort iterations if RMSE is more than best result plus NUM
   - rmse_cutoff=NUM            abort if RMSE is above NUM
   - mae_cutoff=NUM             abort if MAE is above NUM
   - compute_fit=BOOL           display fit on training data every find_iter iterations");

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
		if (args.Length < 2)
			Usage("Not enough arguments.");

		// read command line parameters
		uint track_no = uint.Parse(args[0]);
		string method = args[1];

		CommandLineParameters parameters = null;
		try	{ parameters = new CommandLineParameters(args, 2); }
		catch (ArgumentException e) { Usage(e.Message);	}

		// arguments for iteration search
		find_iter   = parameters.GetRemoveInt32(  "find_iter",   0);
		max_iter    = parameters.GetRemoveInt32(  "max_iter",    500);
		compute_fit = parameters.GetRemoveBool(   "compute_fit", false);
		epsilon     = parameters.GetRemoveDouble( "epsilon",     0);
		rmse_cutoff = parameters.GetRemoveDouble( "rmse_cutoff", double.MaxValue);
		mae_cutoff  = parameters.GetRemoveDouble( "mae_cutoff",  double.MaxValue);

		// data arguments
		string data_dir  = parameters.GetRemoveString( "data_dir");
		if (data_dir != string.Empty)
			data_dir = data_dir + "/mml-track-" + track_no;
		else
			data_dir = "mml-track" + track_no;
		sample_data      = parameters.GetRemoveBool(   "sample_data", false);

		// other arguments
		save_model_file  = parameters.GetRemoveString( "save_model");
		load_model_file  = parameters.GetRemoveString( "load_model");
		int random_seed  = parameters.GetRemoveInt32(  "random_seed",  -1);
		no_eval          = parameters.GetRemoveBool(   "no_eval",      false);
		prediction_file  = parameters.GetRemoveString( "prediction_file");
		cross_validation = parameters.GetRemoveInt32(  "cross_validation", 0);

		if (random_seed != -1)
			MyMediaLite.Util.Random.InitInstance(random_seed);

		recommender = Recommender.CreateRatingPredictor(method);
		if (recommender == null)
			recommender = Recommender.CreateItemRecommender(method);
		if (recommender == null)
			Usage(string.Format("Unknown method: '{0}'", method));

		Recommender.Configure(recommender, parameters, Usage);

		if (parameters.CheckForLeftovers())
			Usage(-1);

		// load all the data
		TimeSpan loading_time = Utils.MeasureTime(delegate() {
			LoadData(data_dir, track_no);
		});
		Console.WriteLine(string.Format(ni, "loading_time {0,0:0.##}", loading_time.TotalSeconds));

		if (recommender is RatingPredictor)
		{
			var rating_predictor = recommender as RatingPredictor;

			rating_predictor.Ratings = training_ratings;

			rating_predictor.MinRating = min_rating;
			rating_predictor.MaxRating = max_rating;
			Console.Error.WriteLine(string.Format(ni, "ratings range: [{0}, {1}]", rating_predictor.MinRating, rating_predictor.MaxRating));
		}

		if (recommender is ItemRecommender)
		{
			var item_recommender = recommender as ItemRecommender;
			training_data_posonly = CreateFeedback(training_ratings);
			item_recommender.Feedback = training_data_posonly;
			Console.Error.WriteLine("memory before deleting training_ratings: {0}", Memory.Usage);
			training_ratings = null;
			Console.Error.WriteLine("memory after deleting training_ratings:  {0}", Memory.Usage);
		}

		if (load_model_file != string.Empty)
			Recommender.LoadModel(recommender, load_model_file);

		if (track_no == 1)
			DoTrack1();
		else
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
		if (cross_validation > 0)
			throw new ArgumentException("k-fold crossvalidation is not supported for Track 2.");

		TimeSpan seconds;

		// do training (+ testing) on generated validation data
		if (recommender is RatingPredictor)
		{
			var rating_predictor = recommender as RatingPredictor;

			var split = new RatingsSimpleSplit(training_ratings, 0.2);
			rating_predictor.Ratings = split.Train[0];

			seconds = Utils.MeasureTime(delegate() {
				rating_predictor.Train();
			});
			Console.Write(" training_time " + seconds + " ");

			// TODO compute fit if requested

			if (!no_eval)
			{
				seconds = Utils.MeasureTime(delegate() {
					var results = RatingEval.Evaluate(rating_predictor, split.Test[0]);
					RatingEval.DisplayResults(results);
				});
				Console.Write(" evaluation_time " + seconds + " ");
			}
		}
		if (recommender is ItemRecommender)
		{
			var item_recommender = recommender as ItemRecommender;

			// create split
			var split = new PosOnlyFeedbackSimpleSplit(training_data_posonly, 0.2);
			item_recommender.Feedback = split.Train[0];

			// use candidate items intersected with test items as relevant items
			var relevant_items = new HashSet<int>();
			foreach (int user_id in track2_test_data.Keys)
				foreach (int item_id in track2_test_data[user_id])
					relevant_items.Add(item_id);
			relevant_items.IntersectWith(split.Test[0].AllItems);

			int[] relevant_users;
			if (sample_data)
			{
				relevant_users = split.Test[0].AllUsers.ToArray();
			}
			else
			{
				// use corresponding intersection for relevant users, and sample from them
				var relevant_users_set = new HashSet<int>(split.Test[0].AllUsers);
				relevant_users_set.IntersectWith(split.Test[0].AllItems);
				var relevant_users_list = new List<int>(relevant_users_set);
				relevant_users = new int[10000];

				var random = new System.Random();
				for (int i = 0; i < relevant_users.Length; i++)
					relevant_users[i] = relevant_users_list[random.Next(0, relevant_users_list.Count - 1)];

				Console.Error.WriteLine("{0} relevant users, {1} relevant items", relevant_users_list.Count, relevant_items.Count);
				Console.Error.WriteLine("Use subset of 10000 users for validation.");
			}

			if (find_iter != 0)
			{   // make this more abstract ...
				if ( !(recommender is IIterativeModel) )
					Usage("Only iterative recommenders support find_iter.");

				IIterativeModel iterative_recommender_validate = (MF) item_recommender;
				//IIterativeModel iterative_recommender_final    = (MF) item_recommender.Clone();
				Console.WriteLine(recommender.ToString() + " ");

				if (load_model_file == string.Empty)
					recommender.Train();

				if (compute_fit)
					Console.Write(string.Format(ni, "fit {0,0:0.#####} ", iterative_recommender_validate.ComputeFit()));

				ItemPredictionEval.DisplayResults(ItemPredictionEval.Evaluate(item_recommender, split.Test[0], split.Train[0], relevant_users, relevant_items));
				Console.WriteLine(" " + iterative_recommender_validate.NumIter);

				for (int i = iterative_recommender_validate.NumIter + 1; i <= max_iter; i++)
				{
					TimeSpan time = Utils.MeasureTime(delegate() {
						iterative_recommender_validate.Iterate();
					});
					training_time_stats.Add(time.TotalSeconds);

					if (i % find_iter == 0)
					{
						if (compute_fit)
						{
							double fit = 0;
							time = Utils.MeasureTime(delegate() {
								fit = iterative_recommender_validate.ComputeFit();
							});
							fit_time_stats.Add(time.TotalSeconds);
							Console.Write(string.Format(ni, "fit {0,0:0.#####} ", fit));
						}

						if (!no_eval)
						{
							time = Utils.MeasureTime(delegate() {
								var results = ItemPredictionEval.Evaluate(item_recommender, split.Test[0], split.Train[0], relevant_users, relevant_items);
								ItemPredictionEval.DisplayResults(results);
								//auc_eval_stats.Add(results["AUC"]);
								Console.WriteLine(" " + i);
							});
							eval_time_stats.Add(time.TotalSeconds);
						}
					}
				} // for

				DisplayIterationStats();
				Recommender.SaveModel(recommender, save_model_file);
			}
			else if (!no_eval) // only do this if we want to evaluate, otherwise it makes no sense
			{
				seconds = Utils.MeasureTime(delegate() {
					item_recommender.Train();
				});
				Console.Write(" training_time " + seconds + " ");

				seconds = Utils.MeasureTime(delegate() {
					var results = ItemPredictionEval.Evaluate(item_recommender, split.Test[0], split.Train[0], relevant_users, relevant_items);
					ItemPredictionEval.DisplayResults(results);
				});
				Console.Write(" evaluation_time " + seconds + " ");
			}

			// reset training data
			if (recommender is RatingPredictor)
				((RatingPredictor) recommender).Ratings = training_ratings;
			if (recommender is ItemRecommender)
				((ItemRecommender) recommender).Feedback = training_data_posonly;

			Console.WriteLine();
		}

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
					KDDCup.PredictTrack2(recommender, track2_test_data, prediction_file);
				}
			);
			Console.Write("predicting_time " + seconds);
		}

		Console.WriteLine();
	}

	static void DoTrack1()
	{
		var rating_predictor_validate  = recommender as RatingPredictor;
		var rating_predictor_final     = rating_predictor_validate.Clone() as RatingPredictor;
		rating_predictor_final.Ratings = combined_ratings;

		Console.WriteLine("Validation split:");
		Utils.DisplayDataStats(training_ratings, validation_ratings, rating_predictor_validate);
		Console.WriteLine("Test split:");
		Utils.DisplayDataStats(combined_ratings, track1_test_data, rating_predictor_final);

		if (find_iter != 0)
		{
			if ( !(recommender is IIterativeModel) )
				Usage("Only iterative recommenders support find_iter.");
			IIterativeModel iterative_recommender_validate = (MatrixFactorization) rating_predictor_validate;
			IIterativeModel iterative_recommender_final    = (MatrixFactorization) rating_predictor_final;
			Console.WriteLine(recommender.ToString() + " ");

			if (load_model_file == string.Empty)
			{
				iterative_recommender_validate.Train();
				iterative_recommender_final.Train();
			}
			else
			{
				Recommender.LoadModel(rating_predictor_final, "final-" + load_model_file);
			}

			if (compute_fit)
				Console.Write(string.Format(ni, "fit {0,0:0.#####} ", iterative_recommender_validate.ComputeFit()));

			RatingEval.DisplayResults(RatingEval.Evaluate(rating_predictor_validate, validation_ratings));
			Console.WriteLine(" " + iterative_recommender_validate.NumIter);

			for (int i = iterative_recommender_validate.NumIter + 1; i <= max_iter; i++)
			{
				TimeSpan time = Utils.MeasureTime(delegate() {
					iterative_recommender_validate.Iterate();

					iterative_recommender_final.Iterate(); // TODO parallelize this
				});
				training_time_stats.Add(time.TotalSeconds);


				if (i % find_iter == 0)
				{
					if (compute_fit)
					{
						double fit = 0;
						time = Utils.MeasureTime(delegate() {
							fit = iterative_recommender_validate.ComputeFit();
						});
						fit_time_stats.Add(time.TotalSeconds);
						Console.Write(string.Format(ni, "fit {0,0:0.#####} ", fit));
					}

					// evaluate and save stats
					// TODO parallelize
					Dictionary<string, double> results = null;
					time = Utils.MeasureTime(delegate() {
						results = RatingEval.Evaluate(rating_predictor_validate, validation_ratings);
						RatingEval.DisplayResults(results);
						rmse_eval_stats.Add(results["RMSE"]);
						Console.WriteLine(" " + i);
					});
					eval_time_stats.Add(time.TotalSeconds);

					// TODO measure time needed for this
					if (prediction_file != string.Empty)
						KDDCup.PredictTrack1(rating_predictor_validate, validation_ratings, prediction_file + "-validation");

					// if best result so far, write out model file and predictions
					if (results["RMSE"] == rmse_eval_stats.Min())
					{
						if (save_model_file != string.Empty)
						{
							Recommender.SaveModel(rating_predictor_validate, save_model_file, i);
							Recommender.SaveModel(rating_predictor_final, "final-" + save_model_file, i);
						}
						if (prediction_file != string.Empty)
							KDDCup.PredictTrack1(rating_predictor_final, track1_test_data, prediction_file + "-it-" + i);
					}

					// check whether we should abort
					if (epsilon > 0 && results["RMSE"] > rmse_eval_stats.Min() + epsilon)
					{
						Console.Error.WriteLine(string.Format(ni, "{0} >> {1}", results["RMSE"], rmse_eval_stats.Min()));
						Console.Error.WriteLine("Reached convergence on training/validation data after {0} iterations.", i);
						break;
					}
					if (results["RMSE"] > rmse_cutoff || results["MAE"] > mae_cutoff)
					{
						Console.Error.WriteLine("Reached cutoff after {0} iterations.", i);
						break;
					}
				}
			} // for

			DisplayIterationStats();
			Recommender.SaveModel(recommender, save_model_file);
		}
		else
		{
			TimeSpan seconds;

			if (!no_eval)
			{			
				if (load_model_file == string.Empty)
				{
					Console.Write(recommender.ToString());
					if (cross_validation > 0) // TODO cross-validation could also be performed on the complete dataset
					{
						Console.WriteLine();
						var split = new RatingCrossValidationSplit(training_ratings, cross_validation);
						var results = RatingEval.EvaluateOnSplit(rating_predictor_validate, split);
						RatingEval.DisplayResults(results);
						no_eval = true;
						rating_predictor_validate.Ratings = training_ratings;
					}
					else
					{
						seconds = Utils.MeasureTime( delegate() { recommender.Train(); } );
	        			Console.Write(" training_time " + seconds + " ");
						Recommender.SaveModel(recommender, save_model_file);
					}
				}
	
				Console.Write(recommender.ToString() + " ");
			
				seconds = Utils.MeasureTime(
			    	delegate() { RatingEval.DisplayResults(RatingEval.Evaluate(rating_predictor_validate, validation_ratings)); }
				);
				Console.Write(" testing_time " + seconds);
			}

			Console.WriteLine();

			if (prediction_file != string.Empty)
			{
				Console.WriteLine("Prediction for KDD Cup Track 1:");
				seconds = Utils.MeasureTime( delegate() { rating_predictor_final.Train(); } );
        		Console.Write(" training_time " + seconds + " ");
				if (save_model_file != string.Empty)
					Recommender.SaveModel(rating_predictor_final, "final-" + save_model_file);

				Console.WriteLine();
				seconds = Utils.MeasureTime( delegate() {
						KDDCup.PredictTrack1(rating_predictor_final, track1_test_data, prediction_file);
				});
				Console.Error.WriteLine("predicting_time " + seconds);
			}
		}
	}

    static void LoadData(string data_dir, uint track_no)
	{
		string training_file   = Path.Combine(data_dir, string.Format("trainIdx{0}.txt", track_no));
		string test_file       = Path.Combine(data_dir, string.Format("testIdx{0}.txt",  track_no));
		string validation_file = Path.Combine(data_dir, "validationIdx1.txt");
		string track_file      = Path.Combine(data_dir, string.Format("trackData{0}.txt",  track_no));
		string album_file      = Path.Combine(data_dir, string.Format("albumData{0}.txt",  track_no));
		string artist_file     = Path.Combine(data_dir, string.Format("artistData{0}.txt", track_no));
		string genre_file      = Path.Combine(data_dir, string.Format("genreData{0}.txt",  track_no));
		//int num_ratings            = track_no == 1 ? 262810175 : 62551438;
		int num_ratings            = track_no == 1 ? 262810175 : 62551438;
		int num_validation_ratings = 4003960;
		int num_test_ratings       = 6005940;

		if (sample_data)
		{
			num_ratings            = track_no == 1 ? 11696 : 8824; // these are not true values, just upper bounds
			num_validation_ratings = 220;                          // these are not true values, just upper bounds
			num_test_ratings       = 308;
			training_file   = Path.Combine(data_dir, string.Format("trainIdx{0}.firstLines.txt", track_no));
			test_file       = Path.Combine(data_dir, string.Format("testIdx{0}.firstLines.txt",  track_no));
			validation_file = Path.Combine(data_dir, "validationIdx1.firstLines.txt");
		}

		// read training data
		training_ratings = MyMediaLite.IO.KDDCup2011.Ratings.Read(training_file, num_ratings);

		// read validation data (track 1)
		if (track_no == 1)
		{
			validation_ratings = MyMediaLite.IO.KDDCup2011.Ratings.Read(validation_file, num_validation_ratings);
			combined_ratings = new CombinedRatings(training_ratings, validation_ratings);
		}

		// read test data
		if (track_no == 1)
			track1_test_data = MyMediaLite.IO.KDDCup2011.Ratings.ReadTest(test_file, num_test_ratings);
		else
			track2_test_data = Track2Candidates.Read(test_file);

		// read item data
		if (recommender is IKDDCupRecommender)
		{
			var kddcup_recommender = recommender as IKDDCupRecommender;
			kddcup_recommender.ItemInfo = Items.Read(track_file, album_file, artist_file, genre_file, track_no);
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
		if (compute_fit && fit_time_stats.Count > 0)
			Console.Error.WriteLine(string.Format(
			    ni,
				"fit_time: min={0,0:0.##}, max={1,0:0.##}, avg={2,0:0.##}",
            	fit_time_stats.Min(), fit_time_stats.Max(), fit_time_stats.Average()
			));
	}
}