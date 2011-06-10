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
using System;

using System.Collections.Generic;
using System.IO;
using MyMediaLite.Classification;
using MyMediaLite.DataType;
using MyMediaLite.Util;

class Classification
{
	// TODO integrate logistic regression and LIBSVM, maybe later LIBLINEAR
	
	public static void Main (string[] args)
	{
		var lr = new LogisticRegression();
	}
	
	// TODO put into library
	/// <summary>Read in data in LIBSVM format</summary>
	/// <param name="filename">filename</param>
	/// <returns>the predictor and target variables</returns>
	static Pair<Matrix<double>, IList<byte>> ReadData(string filename)
	{
		var targets    = new List<byte>();
		var predictors = new List<IList<double>>();
		
		string line;
		using ( var reader = new StreamReader(filename) )
			while ( (line = reader.ReadLine()) != null )
			{
				var fields = line.Split(' ');
				if (fields[0] == "+1" || fields[0] == "1")
					targets.Add(1);
				else if (fields[0] == "-1" || fields[0] == "0")
					targets.Add(0);
				else
					throw new IOException("Unknown target label " + fields[0]);
				
				var features = new Dictionary<uint, double>();
				for (int i = 1; i < fields.Length; i++)
				{
					var pair = fields[i].Split(':');
					features[uint.Parse(pair[0])] = double.Parse(pair[1]);
				}
			}
		throw new Exception();
	}
}
