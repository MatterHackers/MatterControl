/*
Copyright (c) 2019, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using ClipperLib;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
    public class PlaneNormalXSorter : IComparer<Plane>
	{
		private readonly List<Plane> planes;

		public PlaneNormalXSorter(IEnumerable<Plane> inputPlanes)
		{
			planes = new List<Plane>(inputPlanes);
			planes.Sort(this);
		}

		public int Compare(Plane a, Plane b)
		{
			return a.Normal.X.CompareTo(b.Normal.X);
		}

		public Plane? FindPlane(Plane searchPlane,
			double distanceErrorValue = .01,
			double normalErrorValue = .0001)
		{
			Plane testPlane = searchPlane;
			int index = planes.BinarySearch(testPlane, this);
			if (index < 0)
			{
				index = ~index;
			}
			// we have the starting index now get all the vertices that are close enough starting from here
			for (int i = index; i < planes.Count; i++)
			{
				if (Math.Abs(planes[i].Normal.X - searchPlane.Normal.X) > normalErrorValue)
				{
					// we are too far away in x, we are done with this direction
					break;
				}

				if (planes[i].Equals(searchPlane, distanceErrorValue, normalErrorValue))
				{
					return planes[i];
				}
			}
			for (int i = index - 1; i >= 0; i--)
			{
				if (Math.Abs(planes[i].Normal.X - searchPlane.Normal.X) > normalErrorValue)
				{
					// we are too far away in x, we are done with this direction
					break;
				}

				if (planes[i].Equals(searchPlane, distanceErrorValue, normalErrorValue))
				{
					return planes[i];
				}
			}

			return null;
		}
	}
}