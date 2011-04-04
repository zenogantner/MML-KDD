#!/usr/bin/env ipy

import clr
clr.AddReference("MyMediaLite.dll")
clr.AddReference("MyMediaLiteExperimental.dll")
from MyMediaLite import *

num_train_ratings = 11696
num_validation_ratings = 220
num_test_ratings = 308
train_file      = "trainIdx1.firstLines.txt"
validation_file = "validationIdx1.firstLines.txt"
test_file       = "testIdx1.firstLines.txt"

# load the data
training_data   = IO.KDDCup2011.Ratings.Read(train_file, num_train_ratings)
validation_data = IO.KDDCup2011.Ratings.Read(validation_file, num_validation_ratings)
test_data       = IO.KDDCup2011.Ratings.ReadTest(test_file, num_test_ratings)

item_relations = IO.KDDCup2011.Items.Read("trackData1.txt", "albumData1.txt", "artistData1.txt", "genreData1.txt", 1);
print item_relations

# set up the recommender
recommender = RatingPrediction.UserItemBaseline()
recommender.MinRating = 0
recommender.MaxRating = 100
recommender.Ratings = training_data
print "Training ..."
recommender.Train()
print "done."

# measure the accuracy on the validation set
print Eval.RatingEval.Evaluate(recommender, validation_data)

# predict on the test set
print "Predicting ..."
Eval.KDDCup.PredictTrack1(recommender, test_data, "track1-output.txt")
print "done."
