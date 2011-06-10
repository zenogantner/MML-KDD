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
		string line;
		using ( var reader = new StreamReader(filename) )
			while ( (line = reader.ReadLine()) != null )
			{
				// do stuff
			}
		throw new Exception();
	}
}
