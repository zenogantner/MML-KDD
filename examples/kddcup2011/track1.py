#!/usr/bin/env ipy

import clr
clr.AddReference("MyMediaLite.dll")
clr.AddReference("MyMediaLiteExperimental.dll")
from MyMediaLite import *

min_rating = 0;
max_rating = 100;

train_file      = "trainIdx1.firstLines.txt"
validation_file = "validationIdx1.firstLines.txt"
test_file       = "testIdx1.firstLines.txt"

# load the data
training_data   = IO.KDDCup2011.Ratings.Read(train_file)
validation_data = IO.KDDCup2011.Ratings.Read(validation_file)
# TODO load test data
item_relations = IO.KDDCup2011.Items.ReadTrack1("trackData1.txt", "albumData1.txt", "artistData1.txt", "genreData1.txt");
print item_relations

# set up the recommender
recommender = RatingPrediction.UserItemBaseline()
recommender.MinRating = min_rating
recommender.MaxRating = max_rating
recommender.Ratings = training_data
print "Training ..."
recommender.Train()
print "done."

# measure the accuracy on the validation set
# TODO

# predict on the test set
# TODO