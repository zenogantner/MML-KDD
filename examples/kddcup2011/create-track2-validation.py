#!/usr/bin/env ipy

import clr
clr.AddReference("MyMediaLite.dll")
clr.AddReference("MyMediaLiteExperimental.dll")
from MyMediaLite import *

num_ratings = 62551438
train_file = "trainIdx2.firstLines.txt"
test_file  = "testIdx2.firstLines.txt"

# load the data
train_data = IO.KDDCup2011.Ratings.Read(train_file, num_ratings)
test_data = IO.KDDCup2011.Track2Candidates.Read(test_file)
item_relations = IO.KDDCup2011.Items.Read("trackData2.txt", "albumData2.txt", "artistData2.txt", "genreData2.txt", 2);
print item_relations

validation_split = Eval.Track2Validation(train_data, test_data)